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

	// Voice volume (0-7)
	kParamVoiceVol0 = 300,
	kParamVoiceVol1 = 301,
	kParamVoiceVol2 = 302,
	kParamVoiceVol3 = 303,
	kParamVoiceVol4 = 304,
	kParamVoiceVol5 = 305,
	kParamVoiceVol6 = 306,
	kParamVoiceVol7 = 307,

	// Pitch bend (per channel, 0-7)
	kParamPitchBend0 = 400,
	kParamPitchBend1 = 401,
	kParamPitchBend2 = 402,
	kParamPitchBend3 = 403,
	kParamPitchBend4 = 404,
	kParamPitchBend5 = 405,
	kParamPitchBend6 = 406,
	kParamPitchBend7 = 407,

	// Pitch bend range (semitones)
	kParamPitchBendRange = 500,

	// Sample editor parameters
	kParamSampleSelect = 600,
	kParamSamplePitch = 601,
	kParamSampleVolume = 602,
	kParamSampleAttack = 603,
	kParamSampleDecay = 604,
	kParamSampleSustain = 605,
	kParamSampleRelease = 606,
	kParamSampleTrigger = 607,

	// View mode
	kParamViewMode = 700,

	kNumParams
};

} // namespace SnesSpc
