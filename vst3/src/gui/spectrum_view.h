#pragma once

#include "vstgui/lib/cview.h"
#include "vstgui/lib/cdrawcontext.h"
#include <vector>
#include <mutex>
#include <cmath>
#include <complex>

namespace SnesSpc {

//------------------------------------------------------------------------
// SpectrumView - FFT-based frequency spectrum display
//------------------------------------------------------------------------
class SpectrumView : public VSTGUI::CView {
public:
	explicit SpectrumView(const VSTGUI::CRect& size);
	~SpectrumView() override = default;

	// CView overrides
	void draw(VSTGUI::CDrawContext* context) override;

	// Feed audio data for analysis
	void pushSamples(const float* samples, size_t numSamples);

	// Style
	void setBackgroundColor(const VSTGUI::CColor& color) { backgroundColor_ = color; }
	void setBarColor(const VSTGUI::CColor& color) { barColor_ = color; }
	void setPeakColor(const VSTGUI::CColor& color) { peakColor_ = color; }
	void setGridColor(const VSTGUI::CColor& color) { gridColor_ = color; }

	// Configuration
	void setNumBands(int bands) { numBands_ = std::clamp(bands, 8, 128); }
	int getNumBands() const { return numBands_; }
	void setDecayRate(float rate) { decayRate_ = std::clamp(rate, 0.01f, 1.0f); }
	void setSmoothing(float smooth) { smoothing_ = std::clamp(smooth, 0.0f, 0.99f); }

	// Display options
	void setShowPeaks(bool show) { showPeaks_ = show; }
	void setLogScale(bool log) { logScale_ = log; }

	CLASS_METHODS(SpectrumView, CView)

protected:
	void computeFFT();
	void updateBands();
	void drawBars(VSTGUI::CDrawContext* context, const VSTGUI::CRect& rect);
	void drawGrid(VSTGUI::CDrawContext* context, const VSTGUI::CRect& rect);

private:
	std::mutex dataMutex_;

	// FFT settings
	static constexpr size_t FFT_SIZE = 1024;
	std::vector<float> sampleBuffer_;
	std::vector<std::complex<float>> fftBuffer_;
	std::vector<float> magnitudes_;
	size_t sampleWriteIndex_ = 0;

	// Band data
	std::vector<float> bandValues_;
	std::vector<float> peakValues_;
	std::vector<float> peakDecay_;
	int numBands_ = 32;

	// Colors
	VSTGUI::CColor backgroundColor_ = VSTGUI::CColor(30, 30, 30);
	VSTGUI::CColor barColor_ = VSTGUI::CColor(74, 159, 255);
	VSTGUI::CColor peakColor_ = VSTGUI::CColor(255, 200, 100);
	VSTGUI::CColor gridColor_ = VSTGUI::CColor(50, 50, 50);

	// Settings
	float decayRate_ = 0.05f;
	float smoothing_ = 0.7f;
	bool showPeaks_ = true;
	bool logScale_ = true;

	// Simple in-place FFT (Cooley-Tukey radix-2)
	void fft(std::vector<std::complex<float>>& data);
	static size_t reverseBits(size_t n, size_t bits);
};

//------------------------------------------------------------------------
// SpectrumViewFactory - VSTGUI factory
//------------------------------------------------------------------------
class SpectrumViewFactory : public VSTGUI::ViewCreatorAdapter {
public:
	SpectrumViewFactory();

	VSTGUI::IdStringPtr getViewName() const override { return "SpectrumView"; }
	VSTGUI::IdStringPtr getBaseViewName() const override { return VSTGUI::UIViewCreator::kCView; }
	VSTGUI::CView* create(const VSTGUI::UIAttributes& attributes, const VSTGUI::IUIDescription* description) const override;
};

} // namespace SnesSpc
