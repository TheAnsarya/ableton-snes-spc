#pragma once

#include "vstgui/lib/cview.h"
#include "vstgui/lib/cdrawcontext.h"
#include <vector>
#include <mutex>
#include <cstdint>

namespace SnesSpc {

//------------------------------------------------------------------------
// WaveformView - Displays audio waveform or sample data
//------------------------------------------------------------------------
class WaveformView : public VSTGUI::CView {
public:
	explicit WaveformView(const VSTGUI::CRect& size);
	~WaveformView() override = default;

	// CView overrides
	void draw(VSTGUI::CDrawContext* context) override;

	// Waveform data
	void setSampleData(const int16_t* data, size_t numSamples);
	void setWaveformData(const float* left, const float* right, size_t numSamples);
	void clearData();

	// Style
	void setBackgroundColor(const VSTGUI::CColor& color) { backgroundColor_ = color; }
	void setWaveformColor(const VSTGUI::CColor& color) { waveformColor_ = color; }
	void setGridColor(const VSTGUI::CColor& color) { gridColor_ = color; }
	void setCenterLineColor(const VSTGUI::CColor& color) { centerLineColor_ = color; }

	// Display mode
	enum class DisplayMode {
		Waveform,      // Time-domain waveform
		Spectrum,      // Frequency spectrum (future)
		BrrSamples     // BRR sample blocks visualization
	};
	void setDisplayMode(DisplayMode mode) { displayMode_ = mode; invalid(); }
	DisplayMode getDisplayMode() const { return displayMode_; }

	// Zoom and scroll
	void setZoom(float zoom) { zoom_ = std::max(0.1f, std::min(100.0f, zoom)); invalid(); }
	float getZoom() const { return zoom_; }
	void setOffset(float offset) { offset_ = offset; invalid(); }
	float getOffset() const { return offset_; }

	// Selection
	void setSelection(size_t start, size_t end);
	void clearSelection();
	bool hasSelection() const { return selectionStart_ != selectionEnd_; }
	size_t getSelectionStart() const { return selectionStart_; }
	size_t getSelectionEnd() const { return selectionEnd_; }

	// BRR visualization
	void setBrrBlockData(const std::vector<uint8_t>& brrData);

	CLASS_METHODS(WaveformView, CView)

protected:
	void drawWaveform(VSTGUI::CDrawContext* context, const VSTGUI::CRect& rect);
	void drawBrrBlocks(VSTGUI::CDrawContext* context, const VSTGUI::CRect& rect);
	void drawGrid(VSTGUI::CDrawContext* context, const VSTGUI::CRect& rect);
	void drawSelection(VSTGUI::CDrawContext* context, const VSTGUI::CRect& rect);

private:
	std::mutex dataMutex_;
	std::vector<float> waveformLeft_;
	std::vector<float> waveformRight_;
	std::vector<uint8_t> brrData_;

	// Colors
	VSTGUI::CColor backgroundColor_ = VSTGUI::CColor(30, 30, 30);
	VSTGUI::CColor waveformColor_ = VSTGUI::CColor(74, 159, 255);
	VSTGUI::CColor waveformRightColor_ = VSTGUI::CColor(255, 159, 74);
	VSTGUI::CColor gridColor_ = VSTGUI::CColor(60, 60, 60);
	VSTGUI::CColor centerLineColor_ = VSTGUI::CColor(80, 80, 80);
	VSTGUI::CColor selectionColor_ = VSTGUI::CColor(74, 159, 255, 64);

	// Display state
	DisplayMode displayMode_ = DisplayMode::Waveform;
	float zoom_ = 1.0f;
	float offset_ = 0.0f;
	size_t selectionStart_ = 0;
	size_t selectionEnd_ = 0;
};

//------------------------------------------------------------------------
// WaveformViewFactory - VSTGUI factory for custom view creation
//------------------------------------------------------------------------
class WaveformViewFactory : public VSTGUI::ViewCreatorAdapter {
public:
	WaveformViewFactory();

	VSTGUI::IdStringPtr getViewName() const override { return "WaveformView"; }
	VSTGUI::IdStringPtr getBaseViewName() const override { return VSTGUI::UIViewCreator::kCView; }
	VSTGUI::CView* create(const VSTGUI::UIAttributes& attributes, const VSTGUI::IUIDescription* description) const override;
	bool apply(VSTGUI::CView* view, const VSTGUI::UIAttributes& attributes, const VSTGUI::IUIDescription* description) const override;
};

} // namespace SnesSpc
