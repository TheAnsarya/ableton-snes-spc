#include "spc_editor.h"
#include <algorithm>
#include <cctype>

namespace SnesSpc {

//------------------------------------------------------------------------
SpcEditor::SpcEditor(SpcController* controller, VSTGUI::UTF8StringPtr templateName, VSTGUI::UTF8StringPtr xmlFile)
	: VST3Editor(controller, templateName, xmlFile)
	, controller_(controller) {
}

//------------------------------------------------------------------------
bool SpcEditor::open(void* parent, const VSTGUI::PlatformType& platformType) {
	return VST3Editor::open(parent, platformType);
}

//------------------------------------------------------------------------
void SpcEditor::close() {
	VST3Editor::close();
}

//------------------------------------------------------------------------
bool SpcEditor::containsSpcFile(VSTGUI::IDataPackage* drag) {
	if (!drag) return false;

	auto count = drag->getCount();
	for (uint32_t i = 0; i < count; i++) {
		if (drag->getDataType(i) == VSTGUI::IDataPackage::kFilePath) {
			const void* data = nullptr;
			VSTGUI::IDataPackage::Type type;
			auto size = drag->getData(i, data, type);
			if (size > 0 && data) {
				std::string path(static_cast<const char*>(data), size);
				// Check extension
				std::string ext;
				auto dotPos = path.rfind('.');
				if (dotPos != std::string::npos) {
					ext = path.substr(dotPos);
					std::transform(ext.begin(), ext.end(), ext.begin(),
						[](unsigned char c) { return std::tolower(c); });
				}
				if (ext == ".spc" || ext == ".rsn" || ext == ".spcx") {
					return true;
				}
			}
		}
	}
	return false;
}

//------------------------------------------------------------------------
std::string SpcEditor::extractFilePath(VSTGUI::IDataPackage* drag) {
	if (!drag) return {};

	auto count = drag->getCount();
	for (uint32_t i = 0; i < count; i++) {
		if (drag->getDataType(i) == VSTGUI::IDataPackage::kFilePath) {
			const void* data = nullptr;
			VSTGUI::IDataPackage::Type type;
			auto size = drag->getData(i, data, type);
			if (size > 0 && data) {
				return std::string(static_cast<const char*>(data), size);
			}
		}
	}
	return {};
}

//------------------------------------------------------------------------
VSTGUI::DragOperation SpcEditor::onDragEnter(VSTGUI::IDataPackage* drag, const VSTGUI::CPoint& where) {
	isDragOver_ = containsSpcFile(drag);
	if (isDragOver_) {
		// TODO: Update visual feedback (highlight drop zone)
		return VSTGUI::DragOperation::Copy;
	}
	return VSTGUI::DragOperation::None;
}

//------------------------------------------------------------------------
void SpcEditor::onDragLeave(VSTGUI::IDataPackage* drag, const VSTGUI::CPoint& where) {
	isDragOver_ = false;
	// TODO: Remove visual feedback
}

//------------------------------------------------------------------------
VSTGUI::DragOperation SpcEditor::onDragMove(VSTGUI::IDataPackage* drag, const VSTGUI::CPoint& where) {
	if (isDragOver_) {
		return VSTGUI::DragOperation::Copy;
	}
	return VSTGUI::DragOperation::None;
}

//------------------------------------------------------------------------
bool SpcEditor::onDrop(VSTGUI::IDataPackage* drag, const VSTGUI::CPoint& where) {
	if (!containsSpcFile(drag)) {
		return false;
	}

	auto filePath = extractFilePath(drag);
	if (!filePath.empty() && controller_) {
		controller_->loadSpcFile(filePath.c_str());
		isDragOver_ = false;
		return true;
	}

	return false;
}

} // namespace SnesSpc
