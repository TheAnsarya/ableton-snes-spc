#pragma once

#include "vstgui/plugin-bindings/vst3editor.h"
#include "../spc_controller.h"

namespace SnesSpc {

//------------------------------------------------------------------------
// SpcEditor - Main plugin editor view
//------------------------------------------------------------------------
class SpcEditor : public VSTGUI::VST3Editor {
public:
	SpcEditor(SpcController* controller, VSTGUI::UTF8StringPtr templateName, VSTGUI::UTF8StringPtr xmlFile);
	~SpcEditor() override = default;

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

	// Check if drag contains SPC file
	bool containsSpcFile(VSTGUI::IDataPackage* drag);
	std::string extractFilePath(VSTGUI::IDataPackage* drag);
};

} // namespace SnesSpc
