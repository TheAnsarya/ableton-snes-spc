#pragma once

#include "vstgui/plugin-bindings/vst3editor.h"
#include "vstgui/lib/cvstguitimer.h"
#include "../spc_controller.h"
#include <memory>

namespace SnesSpc {

// Forward declarations
class WaveformView;
class SpectrumView;
class KeyboardHandler;
class PresetBrowser;
class ViewSwitcher;

//------------------------------------------------------------------------
// SpcEditor - Main plugin editor view
//------------------------------------------------------------------------
class SpcEditor : public VSTGUI::VST3Editor {
public:
	SpcEditor(SpcController* controller, VSTGUI::UTF8StringPtr templateName, VSTGUI::UTF8StringPtr xmlFile);
	~SpcEditor() override;

	// IDropTarget for file drag-and-drop
	bool onDrop(VSTGUI::IDataPackage* drag, const VSTGUI::CPoint& where) override;
	VSTGUI::DragOperation onDragEnter(VSTGUI::IDataPackage* drag, const VSTGUI::CPoint& where) override;
	void onDragLeave(VSTGUI::IDataPackage* drag, const VSTGUI::CPoint& where) override;
	VSTGUI::DragOperation onDragMove(VSTGUI::IDataPackage* drag, const VSTGUI::CPoint& where) override;

protected:
	// UI creation
	bool open(void* parent, const VSTGUI::PlatformType& platformType) override;
	void close() override;

private:
	SpcController* controller_ = nullptr;
	bool isDragOver_ = false;

	// Visualization views (found after UI creation)
	WaveformView* waveformView_ = nullptr;
	SpectrumView* spectrumView_ = nullptr;
	PresetBrowser* presetBrowser_ = nullptr;
	ViewSwitcher* viewSwitcher_ = nullptr;

	// Timer for updating visualizations
	VSTGUI::SharedPointer<VSTGUI::CVSTGUITimer> updateTimer_;

	// Keyboard handler
	std::unique_ptr<KeyboardHandler> keyboardHandler_;

	// Timer callback
	void onTimer();

	// Find visualization views in the UI hierarchy
	void findVisualizationViews(VSTGUI::CViewContainer* container);

	// Initialize preset browser with default paths
	void initializePresetBrowser();

	// Check if drag contains SPC file
	bool containsSpcFile(VSTGUI::IDataPackage* drag);
	std::string extractFilePath(VSTGUI::IDataPackage* drag);
};

} // namespace SnesSpc
