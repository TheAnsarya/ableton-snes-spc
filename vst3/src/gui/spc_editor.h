#pragma once

#include "vstgui/plugin-bindings/vst3editor.h"
#include "vstgui/lib/cvstguitimer.h"
#include "../spc_controller.h"
#include <memory>

namespace SnesSpc {

#if ENABLE_CUSTOM_VIEWS
// Forward declarations for custom views
class WaveformView;
class SpectrumView;
class KeyboardHandler;
class PresetBrowser;
class ViewSwitcher;
#endif

//------------------------------------------------------------------------
// SpcEditor - Main plugin editor view
//------------------------------------------------------------------------
class SpcEditor : public VSTGUI::VST3Editor {
public:
	SpcEditor(SpcController* controller, VSTGUI::UTF8StringPtr templateName, VSTGUI::UTF8StringPtr xmlFile);
	~SpcEditor() override;

	// Called when ViewMode parameter changes
	void updatePanelVisibility();

protected:
	// UI creation
	bool open(void* parent, const VSTGUI::PlatformType& platformType) override;
	void close() override;

private:
	SpcController* controller_ = nullptr;

#if ENABLE_CUSTOM_VIEWS
	bool isDragOver_ = false;

	// Visualization views (found after UI creation)
	WaveformView* waveformView_ = nullptr;
	SpectrumView* spectrumView_ = nullptr;
	PresetBrowser* presetBrowser_ = nullptr;
	ViewSwitcher* viewSwitcher_ = nullptr;

	// Keyboard handler
	std::unique_ptr<KeyboardHandler> keyboardHandler_;

	// Timer for updating visualizations
	VSTGUI::SharedPointer<VSTGUI::CVSTGUITimer> updateTimer_;

	// Timer callback
	void onTimer();

	// Find visualization views in the UI hierarchy
	void findVisualizationViews(VSTGUI::CViewContainer* container);

	// Initialize preset browser with default paths
	void initializePresetBrowser();

	// Check if drag contains SPC file
	bool containsSpcFile(VSTGUI::IDataPackage* drag);
	std::string extractFilePath(VSTGUI::IDataPackage* drag);
#endif

	// Switchable panels (found by custom-view-name)
	VSTGUI::CViewContainer* mixerPanel_ = nullptr;
	VSTGUI::CViewContainer* samplesPanel_ = nullptr;
	VSTGUI::CViewContainer* browserPanel_ = nullptr;

	// Find panel views by custom-view-name
	void findPanelViews(VSTGUI::CViewContainer* container);
};

} // namespace SnesSpc
