#pragma once

#include "public.sdk/source/vst/vsteditcontroller.h"
#include "spc_params.h"

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
};

} // namespace SnesSpc
