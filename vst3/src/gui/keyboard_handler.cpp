#include "keyboard_handler.h"
#include "../spc_params.h"

namespace SnesSpc {

//------------------------------------------------------------------------
KeyboardHandler::KeyboardHandler(SpcController* controller)
	: controller_(controller) {

	// Register default shortcuts
	// Space - Play/Pause
	registerShortcut(VSTGUI::VirtualKey::Space, VSTGUI::Modifiers(), [this]() { togglePlayPause(); });

	// L - Toggle Loop
	registerShortcut(VSTGUI::VirtualKey::None, VSTGUI::Modifiers(), [this]() { toggleLoop(); });

	// Escape - Stop
	registerShortcut(VSTGUI::VirtualKey::Escape, VSTGUI::Modifiers(), [this]() { stopPlayback(); });

	// Up Arrow - Increase Volume
	registerShortcut(VSTGUI::VirtualKey::Up, VSTGUI::Modifiers(), [this]() { increaseVolume(); });

	// Down Arrow - Decrease Volume
	registerShortcut(VSTGUI::VirtualKey::Down, VSTGUI::Modifiers(), [this]() { decreaseVolume(); });

	// M - Mute All
	registerShortcut(VSTGUI::VirtualKey::None, VSTGUI::Modifiers(), [this]() { muteAll(); });

	// N - Solo None (unmute all)
	registerShortcut(VSTGUI::VirtualKey::None, VSTGUI::Modifiers(), [this]() { soloNone(); });
}

//------------------------------------------------------------------------
void KeyboardHandler::registerShortcut(VSTGUI::VirtualKey key, VSTGUI::Modifiers modifiers, ShortcutCallback callback) {
	shortcuts_.push_back({ key, modifiers, callback });
}

//------------------------------------------------------------------------
VSTGUI::KeyboardEventConsumeState KeyboardHandler::onKeyboardEvent(const VSTGUI::KeyboardEvent& event, VSTGUI::CFrame* frame) {
	// Only handle key down events
	if (event.type != VSTGUI::EventType::KeyDown) {
		return VSTGUI::KeyboardEventConsumeState::NotConsumed;
	}

	// Check for character-based shortcuts
	if (event.character != 0) {
		char c = static_cast<char>(std::tolower(event.character));

		switch (c) {
			case ' ':
				togglePlayPause();
				return VSTGUI::KeyboardEventConsumeState::Consumed;
			case 'l':
				toggleLoop();
				return VSTGUI::KeyboardEventConsumeState::Consumed;
			case 'm':
				muteAll();
				return VSTGUI::KeyboardEventConsumeState::Consumed;
			case 'n':
				soloNone();
				return VSTGUI::KeyboardEventConsumeState::Consumed;
			case '1': case '2': case '3': case '4':
			case '5': case '6': case '7': case '8': {
				// Toggle voice mute (1-8)
				int voice = c - '1';
				if (controller_) {
					auto* param = controller_->getParameterObject(kParamVoice0 + voice);
					if (param) {
						double current = param->getNormalized();
						param->setNormalized(current > 0.5 ? 0.0 : 1.0);
					}
				}
				return VSTGUI::KeyboardEventConsumeState::Consumed;
			}
		}
	}

	// Check for virtual key shortcuts
	switch (event.virt) {
		case VSTGUI::VirtualKey::Escape:
			stopPlayback();
			return VSTGUI::KeyboardEventConsumeState::Consumed;

		case VSTGUI::VirtualKey::Up:
			if (event.modifiers.empty()) {
				increaseVolume();
				return VSTGUI::KeyboardEventConsumeState::Consumed;
			}
			break;

		case VSTGUI::VirtualKey::Down:
			if (event.modifiers.empty()) {
				decreaseVolume();
				return VSTGUI::KeyboardEventConsumeState::Consumed;
			}
			break;

		case VSTGUI::VirtualKey::Home:
			// Jump to start
			if (controller_) {
				auto* param = controller_->getParameterObject(kParamPosition);
				if (param) {
					param->setNormalized(0.0);
				}
			}
			return VSTGUI::KeyboardEventConsumeState::Consumed;

		default:
			break;
	}

	return VSTGUI::KeyboardEventConsumeState::NotConsumed;
}

//------------------------------------------------------------------------
void KeyboardHandler::togglePlayPause() {
	if (!controller_) return;

	auto* param = controller_->getParameterObject(kParamPlayPause);
	if (param) {
		double current = param->getNormalized();
		param->setNormalized(current > 0.5 ? 0.0 : 1.0);
	}
}

//------------------------------------------------------------------------
void KeyboardHandler::toggleLoop() {
	if (!controller_) return;

	auto* param = controller_->getParameterObject(kParamLoop);
	if (param) {
		double current = param->getNormalized();
		param->setNormalized(current > 0.5 ? 0.0 : 1.0);
	}
}

//------------------------------------------------------------------------
void KeyboardHandler::stopPlayback() {
	if (!controller_) return;

	// Stop playback
	auto* playParam = controller_->getParameterObject(kParamPlayPause);
	if (playParam) {
		playParam->setNormalized(0.0);
	}

	// Reset position
	auto* posParam = controller_->getParameterObject(kParamPosition);
	if (posParam) {
		posParam->setNormalized(0.0);
	}
}

//------------------------------------------------------------------------
void KeyboardHandler::increaseVolume() {
	if (!controller_) return;

	auto* param = controller_->getParameterObject(kParamMasterVolume);
	if (param) {
		double current = param->getNormalized();
		param->setNormalized(std::min(1.0, current + 0.05));
	}
}

//------------------------------------------------------------------------
void KeyboardHandler::decreaseVolume() {
	if (!controller_) return;

	auto* param = controller_->getParameterObject(kParamMasterVolume);
	if (param) {
		double current = param->getNormalized();
		param->setNormalized(std::max(0.0, current - 0.05));
	}
}

//------------------------------------------------------------------------
void KeyboardHandler::muteAll() {
	if (!controller_) return;

	for (int i = 0; i < 8; i++) {
		auto* param = controller_->getParameterObject(kParamVoice0 + i);
		if (param) {
			param->setNormalized(0.0);
		}
	}
}

//------------------------------------------------------------------------
void KeyboardHandler::soloNone() {
	if (!controller_) return;

	// Disable all solos
	for (int i = 0; i < 8; i++) {
		auto* param = controller_->getParameterObject(kParamSolo0 + i);
		if (param) {
			param->setNormalized(0.0);
		}
	}

	// Enable all voices
	for (int i = 0; i < 8; i++) {
		auto* param = controller_->getParameterObject(kParamVoice0 + i);
		if (param) {
			param->setNormalized(1.0);
		}
	}
}

} // namespace SnesSpc
