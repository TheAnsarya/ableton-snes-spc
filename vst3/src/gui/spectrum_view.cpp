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

	// Pre-compute Hann window coefficients
	hannWindow_.resize(FFT_SIZE);
	for (size_t i = 0; i < FFT_SIZE; i++) {
		hannWindow_[i] = 0.5f * (1.0f - std::cos(2.0f * std::numbers::pi_v<float> * i / (FFT_SIZE - 1)));
	}

	// Pre-compute twiddle factors for FFT
	precomputeTwiddleFactors();

	// Pre-compute bit-reversal lookup table
	bitReverseLUT_.resize(FFT_SIZE);
	size_t bits = 0;
	for (size_t temp = FFT_SIZE; temp > 1; temp >>= 1) bits++;
	for (size_t i = 0; i < FFT_SIZE; i++) {
		bitReverseLUT_[i] = reverseBits(i, bits);
	}
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
	// Apply pre-computed Hann window and copy to FFT buffer
	for (size_t i = 0; i < FFT_SIZE; i++) {
		size_t idx = (sampleWriteIndex_ + i) % FFT_SIZE;
		fftBuffer_[i] = std::complex<float>(sampleBuffer_[idx] * hannWindow_[i], 0.0f);
	}

	// In-place FFT with pre-computed twiddle factors
	fftOptimized(fftBuffer_);

	// Calculate magnitudes with SIMD-friendly loop
	const float scale = 2.0f / FFT_SIZE;
	for (size_t i = 0; i < FFT_SIZE / 2; i++) {
		magnitudes_[i] = std::abs(fftBuffer_[i]) * scale;
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

	const float barWidth = rect.getWidth() / numBands_;
	const float gap = std::min(2.0f, barWidth * 0.1f);
	const float halfGap = gap * 0.5f;
	const float rectHeight = rect.getHeight();
	const float rectBottom = rect.bottom;

	// Pre-compute dB conversion constants
	constexpr float minDb = -60.0f;
	constexpr float dbRange = 60.0f;
	constexpr float threshold = 0.0001f;
	constexpr float minDbValue = -80.0f;

	for (int i = 0; i < numBands_; i++) {
		const float x = rect.left + i * barWidth;
		const float value = bandValues_[i];

		// Calculate bar height (dB scale with minimum)
		const float dbValue = value > threshold ? 20.0f * std::log10(value) : minDbValue;
		const float normalized = std::clamp((dbValue - minDb) / dbRange, 0.0f, 1.0f);
		const float barHeight = normalized * rectHeight;

		if (barHeight > 0.5f) {  // Skip nearly invisible bars
			// Draw bar
			VSTGUI::CRect barRect(x + halfGap, rectBottom - barHeight,
								  x + barWidth - halfGap, rectBottom);

			// Color intensity based on height for visual appeal
			const uint8_t r = static_cast<uint8_t>(barColor_.red * (0.5f + 0.5f * normalized));
			const uint8_t g = barColor_.green;
			const uint8_t b = static_cast<uint8_t>(barColor_.blue * (1.0f - 0.3f * normalized));

			context->setFillColor(VSTGUI::CColor(r, g, b));
			context->drawRect(barRect, VSTGUI::kDrawFilled);
		}

		// Draw peak indicator
		if (showPeaks_) {
			const float peakValue = peakValues_[i];
			if (peakValue > threshold) {
				const float peakDb = 20.0f * std::log10(peakValue);
				const float peakNorm = std::clamp((peakDb - minDb) / dbRange, 0.0f, 1.0f);
				const float peakY = rectBottom - peakNorm * rectHeight;

				context->setFrameColor(peakColor_);
				context->setLineWidth(2);
				context->moveTo(VSTGUI::CPoint(x + gap, peakY));
				context->lineTo(VSTGUI::CPoint(x + barWidth - gap, peakY));
			}
		}
	}
}

//------------------------------------------------------------------------
// Cooley-Tukey FFT implementation with pre-computed twiddle factors
//------------------------------------------------------------------------
void SpectrumView::precomputeTwiddleFactors() {
	twiddleFactors_.clear();
	for (size_t len = 2; len <= FFT_SIZE; len *= 2) {
		std::vector<std::complex<float>> factors(len / 2);
		float angle = -2.0f * std::numbers::pi_v<float> / len;
		for (size_t j = 0; j < len / 2; j++) {
			factors[j] = std::complex<float>(
				std::cos(angle * j),
				std::sin(angle * j)
			);
		}
		twiddleFactors_.push_back(std::move(factors));
	}
}

void SpectrumView::fftOptimized(std::vector<std::complex<float>>& data) {
	size_t n = data.size();
	if (n <= 1) return;

	// Bit-reversal permutation using LUT
	for (size_t i = 0; i < n; i++) {
		size_t j = bitReverseLUT_[i];
		if (i < j) std::swap(data[i], data[j]);
	}

	// Cooley-Tukey iterative FFT with pre-computed twiddle factors
	size_t stage = 0;
	for (size_t len = 2; len <= n; len *= 2, stage++) {
		const auto& factors = twiddleFactors_[stage];
		for (size_t i = 0; i < n; i += len) {
			for (size_t j = 0; j < len / 2; j++) {
				auto u = data[i + j];
				auto t = factors[j] * data[i + j + len / 2];
				data[i + j] = u + t;
				data[i + j + len / 2] = u - t;
			}
		}
	}
}

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
