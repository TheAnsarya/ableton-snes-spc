#pragma once

#include "pluginterfaces/vst/vsttypes.h"

namespace SnesSpc {

// Parameter IDs
enum ParamID : Steinberg::Vst::ParamID {
	kParamMasterVolume = 0,
	kParamPlayPause = 1,
	kParamLoop = 2,
	kParamPosition = 3,

	// Voice enable/disable (0-7)
	kParamVoice0 = 100,
	kParamVoice1 = 101,
	kParamVoice2 = 102,
	kParamVoice3 = 103,
	kParamVoice4 = 104,
	kParamVoice5 = 105,
	kParamVoice6 = 106,
	kParamVoice7 = 107,

	// Voice solo (0-7)
	kParamSolo0 = 200,
	kParamSolo1 = 201,
	kParamSolo2 = 202,
	kParamSolo3 = 203,
	kParamSolo4 = 204,
	kParamSolo5 = 205,
	kParamSolo6 = 206,
	kParamSolo7 = 207,

	kNumParams
};

} // namespace SnesSpc
