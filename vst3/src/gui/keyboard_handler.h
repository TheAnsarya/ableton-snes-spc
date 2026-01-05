#pragma once

#include "vstgui/lib/cframe.h"
#include "vstgui/lib/events.h"
#include "../spc_controller.h"
#include <functional>
#include <map>

namespace SnesSpc {

//------------------------------------------------------------------------
// KeyboardHandler - Handles keyboard shortcuts for the plugin
//------------------------------------------------------------------------
class KeyboardHandler : public VSTGUI::IKeyboardHook {
public:
	explicit KeyboardHandler(SpcController* controller);
	~KeyboardHandler() override = default;

	// IKeyboardHook
	VSTGUI::KeyboardEventConsumeState onKeyboardEvent(const VSTGUI::KeyboardEvent& event, VSTGUI::CFrame* frame) override;

	// Register custom shortcuts
	using ShortcutCallback = std::function<void()>;
	void registerShortcut(VSTGUI::VirtualKey key, VSTGUI::Modifiers modifiers, ShortcutCallback callback);

private:
	SpcController* controller_;

	struct Shortcut {
		VSTGUI::VirtualKey key;
		VSTGUI::Modifiers modifiers;
		ShortcutCallback callback;
	};
	std::vector<Shortcut> shortcuts_;

	// Built-in shortcut handlers
	void togglePlayPause();
	void toggleLoop();
	void stopPlayback();
	void increaseVolume();
	void decreaseVolume();
	void muteAll();
	void soloNone();
};

} // namespace SnesSpc
