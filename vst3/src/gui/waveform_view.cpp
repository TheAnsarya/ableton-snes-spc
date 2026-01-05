#include "waveform_view.h"
#include "vstgui/uidescription/uiviewcreator.h"
#include <algorithm>
#include <cmath>

namespace SnesSpc {

//------------------------------------------------------------------------
// WaveformView implementation
//------------------------------------------------------------------------
WaveformView::WaveformView(const VSTGUI::CRect& size)
	: CView(size) {
	setWantsFocus(true);
}

void WaveformView::setSampleData(const int16_t* data, size_t numSamples) {
	std::lock_guard<std::mutex> lock(dataMutex_);

	waveformLeft_.resize(numSamples);
	waveformRight_.clear();

	// Convert 16-bit samples to float [-1, 1]
	constexpr float scale = 1.0f / 32768.0f;
	for (size_t i = 0; i < numSamples; i++) {
		waveformLeft_[i] = data[i] * scale;
	}

	invalid();
}

void WaveformView::setWaveformData(const float* left, const float* right, size_t numSamples) {
	std::lock_guard<std::mutex> lock(dataMutex_);

	waveformLeft_.assign(left, left + numSamples);
	if (right) {
		waveformRight_.assign(right, right + numSamples);
	} else {
		waveformRight_.clear();
	}

	invalid();
}

void WaveformView::clearData() {
	std::lock_guard<std::mutex> lock(dataMutex_);
	waveformLeft_.clear();
	waveformRight_.clear();
	brrData_.clear();
	invalid();
}

void WaveformView::setSelection(size_t start, size_t end) {
	selectionStart_ = start;
	selectionEnd_ = end;
	invalid();
}

void WaveformView::clearSelection() {
	selectionStart_ = 0;
	selectionEnd_ = 0;
	invalid();
}

void WaveformView::setBrrBlockData(const std::vector<uint8_t>& brrData) {
	std::lock_guard<std::mutex> lock(dataMutex_);
	brrData_ = brrData;
	displayMode_ = DisplayMode::BrrSamples;
	invalid();
}

//------------------------------------------------------------------------
void WaveformView::draw(VSTGUI::CDrawContext* context) {
	const VSTGUI::CRect& rect = getViewSize();

	// Draw background
	context->setFillColor(backgroundColor_);
	context->drawRect(rect, VSTGUI::kDrawFilled);

	// Draw grid
	drawGrid(context, rect);

	// Draw based on mode
	switch (displayMode_) {
		case DisplayMode::Waveform:
			drawWaveform(context, rect);
			break;
		case DisplayMode::BrrSamples:
			drawBrrBlocks(context, rect);
			break;
		case DisplayMode::Spectrum:
			// Future: FFT spectrum visualization
			break;
	}

	// Draw selection overlay
	if (hasSelection()) {
		drawSelection(context, rect);
	}

	// Draw border
	context->setFrameColor(gridColor_);
	context->setLineWidth(1);
	context->drawRect(rect, VSTGUI::kDrawStroked);
}

//------------------------------------------------------------------------
void WaveformView::drawGrid(VSTGUI::CDrawContext* context, const VSTGUI::CRect& rect) {
	context->setFrameColor(gridColor_);
	context->setLineWidth(1);

	// Horizontal center line
	float centerY = rect.top + rect.getHeight() / 2;
	context->moveTo(VSTGUI::CPoint(rect.left, centerY));
	context->lineTo(VSTGUI::CPoint(rect.right, centerY));

	// Quarter lines (for +/- 0.5)
	context->setFrameColor(VSTGUI::CColor(50, 50, 50));
	float quarterY = rect.top + rect.getHeight() / 4;
	context->moveTo(VSTGUI::CPoint(rect.left, quarterY));
	context->lineTo(VSTGUI::CPoint(rect.right, quarterY));

	quarterY = rect.top + rect.getHeight() * 3 / 4;
	context->moveTo(VSTGUI::CPoint(rect.left, quarterY));
	context->lineTo(VSTGUI::CPoint(rect.right, quarterY));
}

//------------------------------------------------------------------------
void WaveformView::drawWaveform(VSTGUI::CDrawContext* context, const VSTGUI::CRect& rect) {
	std::lock_guard<std::mutex> lock(dataMutex_);

	if (waveformLeft_.empty()) {
		// Draw "No data" text
		context->setFontColor(VSTGUI::CColor(100, 100, 100));
		context->drawString("No waveform data", VSTGUI::CPoint(rect.left + 10, rect.top + rect.getHeight() / 2));
		return;
	}

	const float width = static_cast<float>(rect.getWidth());
	const float height = static_cast<float>(rect.getHeight());
	const float centerY = rect.top + height / 2;

	// Calculate visible sample range
	const size_t totalSamples = waveformLeft_.size();
	const size_t visibleSamples = static_cast<size_t>(totalSamples / zoom_);
	const size_t startSample = static_cast<size_t>(offset_ * totalSamples);
	const size_t endSample = std::min(startSample + visibleSamples, totalSamples);

	if (startSample >= endSample) return;

	// Samples per pixel
	const float samplesPerPixel = static_cast<float>(endSample - startSample) / width;

	// Draw left channel (or mono)
	context->setFrameColor(waveformColor_);
	context->setLineWidth(1);

	VSTGUI::CGraphicsPath* path = context->createGraphicsPath();
	if (path) {
		bool first = true;
		for (float x = 0; x < width; x += 1.0f) {
			// Get sample range for this pixel
			size_t sampleIdx = startSample + static_cast<size_t>(x * samplesPerPixel);
			if (sampleIdx >= endSample) break;

			// Find min/max in range (for anti-aliased look)
			float minVal = waveformLeft_[sampleIdx];
			float maxVal = minVal;

			size_t rangeEnd = std::min(sampleIdx + static_cast<size_t>(samplesPerPixel) + 1, endSample);
			for (size_t i = sampleIdx; i < rangeEnd; i++) {
				minVal = std::min(minVal, waveformLeft_[i]);
				maxVal = std::max(maxVal, waveformLeft_[i]);
			}

			// Draw vertical line from min to max
			float yMin = centerY - maxVal * (height / 2) * 0.9f;
			float yMax = centerY - minVal * (height / 2) * 0.9f;

			if (first) {
				path->beginSubpath(VSTGUI::CPoint(rect.left + x, yMin));
				first = false;
			} else {
				path->addLine(VSTGUI::CPoint(rect.left + x, yMin));
			}
		}
		context->drawGraphicsPath(path, VSTGUI::CDrawContext::kPathStroked);
		path->forget();
	}

	// Draw right channel if stereo
	if (!waveformRight_.empty()) {
		context->setFrameColor(waveformRightColor_);

		VSTGUI::CGraphicsPath* pathR = context->createGraphicsPath();
		if (pathR) {
			bool first = true;
			for (float x = 0; x < width; x += 1.0f) {
				size_t sampleIdx = startSample + static_cast<size_t>(x * samplesPerPixel);
				if (sampleIdx >= endSample) break;

				float val = waveformRight_[sampleIdx];
				float y = centerY - val * (height / 2) * 0.9f;

				if (first) {
					pathR->beginSubpath(VSTGUI::CPoint(rect.left + x, y));
					first = false;
				} else {
					pathR->addLine(VSTGUI::CPoint(rect.left + x, y));
				}
			}
			context->drawGraphicsPath(pathR, VSTGUI::CDrawContext::kPathStroked);
			pathR->forget();
		}
	}
}

//------------------------------------------------------------------------
void WaveformView::drawBrrBlocks(VSTGUI::CDrawContext* context, const VSTGUI::CRect& rect) {
	std::lock_guard<std::mutex> lock(dataMutex_);

	if (brrData_.empty()) {
		context->setFontColor(VSTGUI::CColor(100, 100, 100));
		context->drawString("No BRR data", VSTGUI::CPoint(rect.left + 10, rect.top + rect.getHeight() / 2));
		return;
	}

	const float width = static_cast<float>(rect.getWidth());
	const float height = static_cast<float>(rect.getHeight());

	// BRR blocks are 9 bytes each
	const size_t numBlocks = brrData_.size() / 9;
	if (numBlocks == 0) return;

	const float blockWidth = width / numBlocks;

	for (size_t block = 0; block < numBlocks; block++) {
		size_t offset = block * 9;
		uint8_t header = brrData_[offset];

		// Decode header
		int shift = (header >> 4) & 0x0F;
		int filter = (header >> 2) & 0x03;
		bool loop = (header & 0x02) != 0;
		bool end = (header & 0x01) != 0;

		// Color based on properties
		VSTGUI::CColor blockColor;
		if (end && loop) {
			blockColor = VSTGUI::CColor(255, 200, 0); // Yellow - loop end
		} else if (end) {
			blockColor = VSTGUI::CColor(255, 100, 100); // Red - end
		} else if (filter > 0) {
			// Different shades based on filter
			blockColor = VSTGUI::CColor(74, 159 - filter * 30, 255);
		} else {
			blockColor = waveformColor_;
		}

		// Draw block
		float x = rect.left + block * blockWidth;
		float blockHeight = height * (shift / 15.0f) * 0.8f + height * 0.1f;

		VSTGUI::CRect blockRect(x + 1, rect.top + (height - blockHeight) / 2,
							   x + blockWidth - 1, rect.top + (height + blockHeight) / 2);

		context->setFillColor(blockColor);
		context->drawRect(blockRect, VSTGUI::kDrawFilled);

		// Draw filter indicator
		if (filter > 0) {
			context->setFillColor(VSTGUI::CColor(255, 255, 255, 100));
			VSTGUI::CRect filterRect(x + 1, rect.bottom - 5 - filter * 3,
									x + blockWidth - 1, rect.bottom - 5);
			context->drawRect(filterRect, VSTGUI::kDrawFilled);
		}
	}
}

//------------------------------------------------------------------------
void WaveformView::drawSelection(VSTGUI::CDrawContext* context, const VSTGUI::CRect& rect) {
	std::lock_guard<std::mutex> lock(dataMutex_);

	if (waveformLeft_.empty() || !hasSelection()) return;

	const float width = static_cast<float>(rect.getWidth());
	const size_t totalSamples = waveformLeft_.size();

	// Convert sample indices to pixel positions
	float startX = rect.left + (selectionStart_ / static_cast<float>(totalSamples)) * width;
	float endX = rect.left + (selectionEnd_ / static_cast<float>(totalSamples)) * width;

	// Draw selection rectangle
	context->setFillColor(selectionColor_);
	VSTGUI::CRect selRect(startX, rect.top, endX, rect.bottom);
	context->drawRect(selRect, VSTGUI::kDrawFilled);

	// Draw selection borders
	context->setFrameColor(waveformColor_);
	context->setLineWidth(1);
	context->moveTo(VSTGUI::CPoint(startX, rect.top));
	context->lineTo(VSTGUI::CPoint(startX, rect.bottom));
	context->moveTo(VSTGUI::CPoint(endX, rect.top));
	context->lineTo(VSTGUI::CPoint(endX, rect.bottom));
}

//------------------------------------------------------------------------
// WaveformViewFactory implementation
//------------------------------------------------------------------------
WaveformViewFactory::WaveformViewFactory() {
	VSTGUI::UIViewFactory::registerViewCreator(*this);
}

VSTGUI::CView* WaveformViewFactory::create(const VSTGUI::UIAttributes& attributes, const VSTGUI::IUIDescription* description) const {
	VSTGUI::CRect size(0, 0, 100, 100);
	return new WaveformView(size);
}

bool WaveformViewFactory::apply(VSTGUI::CView* view, const VSTGUI::UIAttributes& attributes, const VSTGUI::IUIDescription* description) const {
	auto* waveform = dynamic_cast<WaveformView*>(view);
	if (!waveform) return false;

	// Apply custom attributes
	VSTGUI::CColor color;
	if (VSTGUI::UIViewCreator::stringToColor(attributes.getAttributeValue("background-color"), color, description)) {
		waveform->setBackgroundColor(color);
	}
	if (VSTGUI::UIViewCreator::stringToColor(attributes.getAttributeValue("waveform-color"), color, description)) {
		waveform->setWaveformColor(color);
	}
	if (VSTGUI::UIViewCreator::stringToColor(attributes.getAttributeValue("grid-color"), color, description)) {
		waveform->setGridColor(color);
	}

	return true;
}

// Global factory instance to register the view
static WaveformViewFactory gWaveformViewFactory;

} // namespace SnesSpc
