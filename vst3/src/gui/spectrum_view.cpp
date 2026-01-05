#include "spectrum_view.h"
#include "vstgui/uidescription/uiviewcreator.h"
#include <algorithm>
#include <numbers>

namespace SnesSpc {

//------------------------------------------------------------------------
SpectrumView::SpectrumView(const VSTGUI::CRect& size)
	: CView(size) {
	sampleBuffer_.resize(FFT_SIZE, 0.0f);
	fftBuffer_.resize(FFT_SIZE);
	magnitudes_.resize(FFT_SIZE / 2);
	bandValues_.resize(numBands_, 0.0f);
	peakValues_.resize(numBands_, 0.0f);
	peakDecay_.resize(numBands_, 0.0f);
}

//------------------------------------------------------------------------
void SpectrumView::pushSamples(const float* samples, size_t numSamples) {
	std::lock_guard<std::mutex> lock(dataMutex_);

	for (size_t i = 0; i < numSamples; i++) {
		sampleBuffer_[sampleWriteIndex_] = samples[i];
		sampleWriteIndex_ = (sampleWriteIndex_ + 1) % FFT_SIZE;
	}

	computeFFT();
	updateBands();
	invalid();
}

//------------------------------------------------------------------------
void SpectrumView::computeFFT() {
	// Apply Hann window and copy to FFT buffer
	for (size_t i = 0; i < FFT_SIZE; i++) {
		size_t idx = (sampleWriteIndex_ + i) % FFT_SIZE;
		float window = 0.5f * (1.0f - std::cos(2.0f * std::numbers::pi_v<float> * i / (FFT_SIZE - 1)));
		fftBuffer_[i] = std::complex<float>(sampleBuffer_[idx] * window, 0.0f);
	}

	// In-place FFT
	fft(fftBuffer_);

	// Calculate magnitudes
	for (size_t i = 0; i < FFT_SIZE / 2; i++) {
		magnitudes_[i] = std::abs(fftBuffer_[i]) / (FFT_SIZE / 2);
	}
}

//------------------------------------------------------------------------
void SpectrumView::updateBands() {
	if (bandValues_.size() != static_cast<size_t>(numBands_)) {
		bandValues_.resize(numBands_, 0.0f);
		peakValues_.resize(numBands_, 0.0f);
		peakDecay_.resize(numBands_, 0.0f);
	}

	size_t spectrumSize = FFT_SIZE / 2;

	for (int band = 0; band < numBands_; band++) {
		// Calculate frequency range for this band
		size_t startBin, endBin;

		if (logScale_) {
			// Logarithmic distribution for perceptually even bands
			float minLog = std::log10(1.0f);
			float maxLog = std::log10(static_cast<float>(spectrumSize));
			float logStart = minLog + (maxLog - minLog) * band / numBands_;
			float logEnd = minLog + (maxLog - minLog) * (band + 1) / numBands_;
			startBin = static_cast<size_t>(std::pow(10.0f, logStart));
			endBin = static_cast<size_t>(std::pow(10.0f, logEnd));
		} else {
			// Linear distribution
			startBin = spectrumSize * band / numBands_;
			endBin = spectrumSize * (band + 1) / numBands_;
		}

		startBin = std::max<size_t>(startBin, 1);
		endBin = std::min(endBin, spectrumSize);
		if (startBin >= endBin) endBin = startBin + 1;

		// Average magnitudes in range
		float sum = 0.0f;
		for (size_t i = startBin; i < endBin; i++) {
			sum += magnitudes_[i];
		}
		float newValue = sum / (endBin - startBin);

		// Apply smoothing
		bandValues_[band] = smoothing_ * bandValues_[band] + (1.0f - smoothing_) * newValue;

		// Update peaks
		if (bandValues_[band] > peakValues_[band]) {
			peakValues_[band] = bandValues_[band];
			peakDecay_[band] = 0.0f;
		} else {
			peakDecay_[band] += decayRate_;
			peakValues_[band] = std::max(0.0f, peakValues_[band] - peakDecay_[band]);
		}
	}
}

//------------------------------------------------------------------------
void SpectrumView::draw(VSTGUI::CDrawContext* context) {
	const VSTGUI::CRect& rect = getViewSize();

	// Draw background
	context->setFillColor(backgroundColor_);
	context->drawRect(rect, VSTGUI::kDrawFilled);

	// Draw grid
	drawGrid(context, rect);

	// Draw bars
	drawBars(context, rect);

	// Draw border
	context->setFrameColor(gridColor_);
	context->setLineWidth(1);
	context->drawRect(rect, VSTGUI::kDrawStroked);
}

//------------------------------------------------------------------------
void SpectrumView::drawGrid(VSTGUI::CDrawContext* context, const VSTGUI::CRect& rect) {
	context->setFrameColor(gridColor_);
	context->setLineWidth(1);

	// Horizontal lines at -6dB, -12dB, -18dB, -24dB
	float heights[] = { 0.5f, 0.25f, 0.125f, 0.0625f };
	for (float h : heights) {
		float y = rect.bottom - h * rect.getHeight();
		context->moveTo(VSTGUI::CPoint(rect.left, y));
		context->lineTo(VSTGUI::CPoint(rect.right, y));
	}

	// Vertical lines at octave boundaries (approximately)
	int divisions = 8;
	for (int i = 1; i < divisions; i++) {
		float x = rect.left + (rect.getWidth() * i / divisions);
		context->moveTo(VSTGUI::CPoint(x, rect.top));
		context->lineTo(VSTGUI::CPoint(x, rect.bottom));
	}
}

//------------------------------------------------------------------------
void SpectrumView::drawBars(VSTGUI::CDrawContext* context, const VSTGUI::CRect& rect) {
	std::lock_guard<std::mutex> lock(dataMutex_);

	float barWidth = rect.getWidth() / numBands_;
	float gap = std::min(2.0f, barWidth * 0.1f);

	for (int i = 0; i < numBands_; i++) {
		float x = rect.left + i * barWidth;

		// Calculate bar height (dB scale with minimum)
		float value = bandValues_[i];
		float dbValue = value > 0.0001f ? 20.0f * std::log10(value) : -80.0f;
		float normalized = std::clamp((dbValue + 60.0f) / 60.0f, 0.0f, 1.0f);
		float barHeight = normalized * rect.getHeight();

		// Draw bar with gradient-like effect
		VSTGUI::CRect barRect(x + gap / 2, rect.bottom - barHeight,
							  x + barWidth - gap / 2, rect.bottom);

		// Color gradient based on height
		uint8_t r = static_cast<uint8_t>(barColor_.red * (0.5f + 0.5f * normalized));
		uint8_t g = static_cast<uint8_t>(barColor_.green);
		uint8_t b = static_cast<uint8_t>(barColor_.blue * (1.0f - 0.3f * normalized));

		context->setFillColor(VSTGUI::CColor(r, g, b));
		context->drawRect(barRect, VSTGUI::kDrawFilled);

		// Draw peak indicator
		if (showPeaks_ && peakValues_[i] > 0.0001f) {
			float peakDb = 20.0f * std::log10(peakValues_[i]);
			float peakNorm = std::clamp((peakDb + 60.0f) / 60.0f, 0.0f, 1.0f);
			float peakY = rect.bottom - peakNorm * rect.getHeight();

			context->setFrameColor(peakColor_);
			context->setLineWidth(2);
			context->moveTo(VSTGUI::CPoint(x + gap, peakY));
			context->lineTo(VSTGUI::CPoint(x + barWidth - gap, peakY));
		}
	}
}

//------------------------------------------------------------------------
// Cooley-Tukey FFT implementation
//------------------------------------------------------------------------
void SpectrumView::fft(std::vector<std::complex<float>>& data) {
	size_t n = data.size();
	if (n <= 1) return;

	// Bit-reversal permutation
	size_t bits = 0;
	for (size_t temp = n; temp > 1; temp >>= 1) bits++;

	for (size_t i = 0; i < n; i++) {
		size_t j = reverseBits(i, bits);
		if (i < j) std::swap(data[i], data[j]);
	}

	// Cooley-Tukey iterative FFT
	for (size_t len = 2; len <= n; len *= 2) {
		float angle = -2.0f * std::numbers::pi_v<float> / len;
		std::complex<float> wn(std::cos(angle), std::sin(angle));

		for (size_t i = 0; i < n; i += len) {
			std::complex<float> w(1.0f, 0.0f);
			for (size_t j = 0; j < len / 2; j++) {
				auto u = data[i + j];
				auto t = w * data[i + j + len / 2];
				data[i + j] = u + t;
				data[i + j + len / 2] = u - t;
				w *= wn;
			}
		}
	}
}

size_t SpectrumView::reverseBits(size_t n, size_t bits) {
	size_t result = 0;
	for (size_t i = 0; i < bits; i++) {
		result = (result << 1) | (n & 1);
		n >>= 1;
	}
	return result;
}

//------------------------------------------------------------------------
// SpectrumViewFactory
//------------------------------------------------------------------------
SpectrumViewFactory::SpectrumViewFactory() {
	VSTGUI::UIViewFactory::registerViewCreator(*this);
}

VSTGUI::CView* SpectrumViewFactory::create(const VSTGUI::UIAttributes& attributes, const VSTGUI::IUIDescription* description) const {
	VSTGUI::CRect size(0, 0, 200, 100);
	return new SpectrumView(size);
}

// Global factory instance
static SpectrumViewFactory gSpectrumViewFactory;

} // namespace SnesSpc
