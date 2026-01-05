#pragma once

#include "public.sdk/source/vst/vsteditcontroller.h"
#include "pluginterfaces/vst/ivstmessage.h"
#include "spc_params.h"
#include "spc_messages.h"

namespace SnesSpc {

class SpcController : public Steinberg::Vst::EditController {
public:
	// Create function
	static Steinberg::FUnknown* createInstance(void* context) {
		return static_cast<Steinberg::Vst::IEditController*>(new SpcController());
	}

	// EditController overrides
	Steinberg::tresult PLUGIN_API initialize(Steinberg::FUnknown* context) override;
	Steinberg::tresult PLUGIN_API terminate() override;
	Steinberg::tresult PLUGIN_API setComponentState(Steinberg::IBStream* state) override;
	Steinberg::tresult PLUGIN_API notify(Steinberg::Vst::IMessage* message) override;

#if VSTGUI_ENABLE
	// Create editor view
	Steinberg::IPlugView* PLUGIN_API createView(Steinberg::FIDString name) override;
#endif

	// Public methods for UI to call
	bool loadSpcFile(const char* filePath);
	bool loadSpcData(const uint8_t* data, int length);

	// State accessors for UI
	bool isSpcLoaded() const { return spcLoaded_; }
	const std::string& getCurrentSpcPath() const { return currentSpcPath_; }

private:
	bool spcLoaded_ = false;
	std::string currentSpcPath_;
};

} // namespace SnesSpc
