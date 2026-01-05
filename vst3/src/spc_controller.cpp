#include "spc_controller.h"
#include "spc_ids.h"
#include "base/source/fstreamer.h"
#include "pluginterfaces/base/ibstream.h"

namespace SnesSpc {

Steinberg::tresult PLUGIN_API SpcController::initialize(Steinberg::FUnknown* context) {
	Steinberg::tresult result = EditController::initialize(context);
	if (result != Steinberg::kResultOk) {
		return result;
	}

	// Register parameters

	// Master volume (0.0 - 1.0)
	parameters.addParameter(
		STR16("Master Volume"),
		STR16("%"),
		0,
		1.0,
		Steinberg::Vst::ParameterInfo::kCanAutomate,
		kParamMasterVolume
	);

	// Play/Pause (toggle)
	parameters.addParameter(
		STR16("Play/Pause"),
		nullptr,
		1,
		0.0,
		Steinberg::Vst::ParameterInfo::kCanAutomate | Steinberg::Vst::ParameterInfo::kIsBypass,
		kParamPlayPause
	);

	// Loop (toggle)
	parameters.addParameter(
		STR16("Loop"),
		nullptr,
		1,
		1.0,
		Steinberg::Vst::ParameterInfo::kCanAutomate,
		kParamLoop
	);

	// Position
	parameters.addParameter(
		STR16("Position"),
		STR16("%"),
		0,
		0.0,
		Steinberg::Vst::ParameterInfo::kCanAutomate | Steinberg::Vst::ParameterInfo::kIsReadOnly,
		kParamPosition
	);

	// Voice enable parameters
	const char16_t* voiceNames[] = {
		STR16("Voice 1"), STR16("Voice 2"), STR16("Voice 3"), STR16("Voice 4"),
		STR16("Voice 5"), STR16("Voice 6"), STR16("Voice 7"), STR16("Voice 8")
	};

	for (int i = 0; i < 8; i++) {
		parameters.addParameter(
			voiceNames[i],
			nullptr,
			1,
			1.0,
			Steinberg::Vst::ParameterInfo::kCanAutomate,
			kParamVoice0 + i
		);
	}

	// Voice solo parameters
	const char16_t* soloNames[] = {
		STR16("Solo 1"), STR16("Solo 2"), STR16("Solo 3"), STR16("Solo 4"),
		STR16("Solo 5"), STR16("Solo 6"), STR16("Solo 7"), STR16("Solo 8")
	};

	for (int i = 0; i < 8; i++) {
		parameters.addParameter(
			soloNames[i],
			nullptr,
			1,
			0.0,
			Steinberg::Vst::ParameterInfo::kCanAutomate,
			kParamSolo0 + i
		);
	}

	// Voice volume parameters
	const char16_t* volumeNames[] = {
		STR16("Volume 1"), STR16("Volume 2"), STR16("Volume 3"), STR16("Volume 4"),
		STR16("Volume 5"), STR16("Volume 6"), STR16("Volume 7"), STR16("Volume 8")
	};

	for (int i = 0; i < 8; i++) {
		parameters.addParameter(
			volumeNames[i],
			STR16("%"),
			0,
			1.0,
			Steinberg::Vst::ParameterInfo::kCanAutomate,
			kParamVoiceVol0 + i
		);
	}

	return Steinberg::kResultOk;
}

Steinberg::tresult PLUGIN_API SpcController::terminate() {
	return EditController::terminate();
}

Steinberg::tresult PLUGIN_API SpcController::setComponentState(Steinberg::IBStream* state) {
	if (!state) {
		return Steinberg::kResultFalse;
	}

	// TODO: Sync controller state with processor state
	return Steinberg::kResultOk;
}

} // namespace SnesSpc
