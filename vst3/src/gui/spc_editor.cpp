#include "spc_editor.h"
#include "waveform_view.h"
#include "spectrum_view.h"
#include "keyboard_handler.h"
#include "preset_browser.h"
#include "view_switcher.h"
#include "../spc_params.h"
#include <algorithm>
#include <cctype>
#include <cstdlib>

namespace SnesSpc {

//------------------------------------------------------------------------
SpcEditor::SpcEditor(SpcController* controller, VSTGUI::UTF8StringPtr templateName, VSTGUI::UTF8StringPtr xmlFile)
	: VST3Editor(controller, templateName, xmlFile)
	, controller_(controller) {
}

//------------------------------------------------------------------------
SpcEditor::~SpcEditor() {
	if (updateTimer_) {
		updateTimer_->stop();
		updateTimer_ = nullptr;
	}
}

//------------------------------------------------------------------------
bool SpcEditor::open(void* parent, const VSTGUI::PlatformType& platformType) {
	bool result = VST3Editor::open(parent, platformType);

	if (result) {
		// Find visualization views and panels
		if (auto* frame = getFrame()) {
			findVisualizationViews(frame);
			findPanelViews(frame);

			// Register keyboard handler
			keyboardHandler_ = std::make_unique<KeyboardHandler>(controller_);
			frame->registerKeyboardHook(keyboardHandler_.get());
		}

		// Initialize preset browser with paths and callbacks
		initializePresetBrowser();

		// Start update timer (60 FPS for smooth visualization)
		updateTimer_ = VSTGUI::makeOwned<VSTGUI::CVSTGUITimer>(
			[this](VSTGUI::CVSTGUITimer*) { onTimer(); },
			16 // ~60 FPS
		);
		updateTimer_->start();

		// Set initial panel visibility based on ViewMode parameter
		updatePanelVisibility();
	}

	return result;
}

//------------------------------------------------------------------------
void SpcEditor::close() {
	if (updateTimer_) {
		updateTimer_->stop();
		updateTimer_ = nullptr;
	}

	// Unregister keyboard handler
	if (keyboardHandler_ && getFrame()) {
		getFrame()->unregisterKeyboardHook(keyboardHandler_.get());
	}
	keyboardHandler_.reset();

	waveformView_ = nullptr;
	spectrumView_ = nullptr;
	presetBrowser_ = nullptr;
	viewSwitcher_ = nullptr;
	mixerPanel_ = nullptr;
	samplesPanel_ = nullptr;
	browserPanel_ = nullptr;
	VST3Editor::close();
}

//------------------------------------------------------------------------
void SpcEditor::findVisualizationViews(VSTGUI::CViewContainer* container) {
	if (!container) return;

	container->forEachChild([this](VSTGUI::CView* child) {
		// Check if it's a WaveformView
		if (auto* wv = dynamic_cast<WaveformView*>(child)) {
			waveformView_ = wv;
		}
		// Check if it's a SpectrumView
		if (auto* sv = dynamic_cast<SpectrumView*>(child)) {
			spectrumView_ = sv;
		}
		// Check if it's a PresetBrowser
		if (auto* pb = dynamic_cast<PresetBrowser*>(child)) {
			presetBrowser_ = pb;
		}
		// Check if it's a ViewSwitcher
		if (auto* vs = dynamic_cast<ViewSwitcher*>(child)) {
			viewSwitcher_ = vs;
		}
		// Recursively search containers
		if (auto* container = dynamic_cast<VSTGUI::CViewContainer*>(child)) {
			findVisualizationViews(container);
		}
	});
}

//------------------------------------------------------------------------
void SpcEditor::findPanelViews(VSTGUI::CViewContainer* container) {
	if (!container) return;

	container->forEachChild([this](VSTGUI::CView* child) {
		// Check for custom view names to identify panels
		if (auto* viewContainer = dynamic_cast<VSTGUI::CViewContainer*>(child)) {
			auto viewName = viewContainer->getAttributeID();
			if (viewName) {
				std::string name(viewName);
				if (name == "MixerPanel") {
					mixerPanel_ = viewContainer;
				} else if (name == "SamplesPanel") {
					samplesPanel_ = viewContainer;
				} else if (name == "BrowserPanel") {
					browserPanel_ = viewContainer;
				}
			}
			// Recursively search containers
			findPanelViews(viewContainer);
		}
	});
}

//------------------------------------------------------------------------
void SpcEditor::updatePanelVisibility() {
	// Get current view mode from parameter
	int viewMode = 0;
	if (controller_) {
		auto value = controller_->getParamNormalized(kParamViewMode);
		viewMode = static_cast<int>(value * 2 + 0.5); // 0, 1, or 2
	}

	// Set visibility based on view mode
	if (mixerPanel_) {
		mixerPanel_->setVisible(viewMode == 0);
	}
	if (samplesPanel_) {
		samplesPanel_->setVisible(viewMode == 1);
	}
	if (browserPanel_) {
		browserPanel_->setVisible(viewMode == 2);
	}

	// Invalidate the frame to redraw
	if (auto* frame = getFrame()) {
		frame->invalid();
	}
}

//------------------------------------------------------------------------
void SpcEditor::initializePresetBrowser() {
	if (!presetBrowser_) return;

	// Set up load callback to use controller
	presetBrowser_->setLoadCallback([this](const PresetInfo& preset) {
		if (controller_) {
			controller_->loadSpcFile(preset.path.c_str());
		}
	});

	// Add common SPC search paths
#ifdef _WIN32
	// Windows paths
	presetBrowser_->addSearchPath("C:\\SPC\\");
	presetBrowser_->addSearchPath("C:\\Music\\SPC\\");
	// User's Music folder
	if (const char* userProfile = std::getenv("USERPROFILE")) {
		std::string musicPath = std::string(userProfile) + "\\Music\\SPC";
		presetBrowser_->addSearchPath(musicPath);
	}
#else
	// macOS/Linux paths
	presetBrowser_->addSearchPath("/Users/Shared/SPC/");
	if (const char* home = std::getenv("HOME")) {
		std::string musicPath = std::string(home) + "/Music/SPC";
		presetBrowser_->addSearchPath(musicPath);
	}
#endif

	// Scan for presets
	presetBrowser_->refreshList();
	presetBrowser_->sortByName(true);
}

//------------------------------------------------------------------------
void SpcEditor::onTimer() {
	// Request waveform data from processor
	if (controller_) {
		controller_->requestWaveformData();
	}

	// Get waveform data and update visualizations
	std::vector<float> waveformLeft, waveformRight;
	bool hasRealData = controller_ && controller_->getWaveformData(waveformLeft, waveformRight);

	if (waveformView_) {
		if (hasRealData && !waveformLeft.empty()) {
			// Use real audio data from engine
			waveformView_->setWaveformData(waveformLeft.data(), waveformRight.data(),
				static_cast<int>(waveformLeft.size()));
		} else {
			// Fallback: generate simple test waveform when no data
			static float phase = 0.0f;
			std::vector<float> testLeft(512), testRight(512);
			for (size_t i = 0; i < 512; i++) {
				testLeft[i] = std::sin(phase + i * 0.1f) * 0.5f;
				testRight[i] = std::sin(phase + i * 0.1f + 1.57f) * 0.5f;
			}
			phase += 0.05f;
			waveformView_->setWaveformData(testLeft.data(), testRight.data(), 512);
		}
	}

	if (spectrumView_) {
		if (hasRealData && !waveformLeft.empty()) {
			// Use left channel (mono) for spectrum analysis
			spectrumView_->pushSamples(waveformLeft.data(), static_cast<int>(waveformLeft.size()));
		} else {
			// Fallback: feed test data to spectrum
			static float phase = 0.0f;
			std::vector<float> testSamples(1024);
			for (size_t i = 0; i < 1024; i++) {
				// Generate multi-frequency test signal
				testSamples[i] = std::sin(phase + i * 0.05f) * 0.3f +
								 std::sin(phase * 2 + i * 0.1f) * 0.2f +
								 std::sin(phase * 4 + i * 0.2f) * 0.1f;
			}
			phase += 0.1f;
			spectrumView_->pushSamples(testSamples.data(), 1024);
		}
	}
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
