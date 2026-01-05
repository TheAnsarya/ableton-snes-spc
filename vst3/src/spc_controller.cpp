#include "spc_controller.h"
#include "spc_ids.h"
#include "base/source/fstreamer.h"
#include "pluginterfaces/base/ibstream.h"
#include <cstring>

#if VSTGUI_ENABLE
#include "gui/spc_editor.h"
#include "gui/midi_learn.h"
#endif

namespace SnesSpc {

SpcController::~SpcController() {
	// unique_ptr handles cleanup
}

Steinberg::tresult PLUGIN_API SpcController::initialize(Steinberg::FUnknown* context) {
	Steinberg::tresult result = EditController::initialize(context);
	if (result != Steinberg::kResultOk) {
		return result;
	}

#if VSTGUI_ENABLE
	// Create MIDI learn handler
	midiLearnHandler_ = std::make_unique<MidiLearnHandler>(this);
#endif

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

	// Pitch bend parameters (per channel)
	const char16_t* pitchBendNames[] = {
		STR16("Pitch Bend 1"), STR16("Pitch Bend 2"), STR16("Pitch Bend 3"), STR16("Pitch Bend 4"),
		STR16("Pitch Bend 5"), STR16("Pitch Bend 6"), STR16("Pitch Bend 7"), STR16("Pitch Bend 8")
	};

	for (int i = 0; i < 8; i++) {
		parameters.addParameter(
			pitchBendNames[i],
			nullptr,
			0,
			0.5, // Center position
			Steinberg::Vst::ParameterInfo::kCanAutomate,
			kParamPitchBend0 + i
		);
	}

	// Pitch bend range
	parameters.addParameter(
		STR16("Pitch Bend Range"),
		STR16("st"),
		23, // 1-24 semitones
		(2.0 - 1.0) / 23.0, // Default 2 semitones
		Steinberg::Vst::ParameterInfo::kCanAutomate,
		kParamPitchBendRange
	);

	// Sample editor parameters
	parameters.addParameter(
		STR16("Sample Select"),
		nullptr,
		127, // Up to 128 samples
		0.0,
		Steinberg::Vst::ParameterInfo::kCanAutomate,
		kParamSampleSelect
	);

	parameters.addParameter(
		STR16("Sample Pitch"),
		STR16("st"),
		0,
		0.5, // Center = no pitch shift
		Steinberg::Vst::ParameterInfo::kCanAutomate,
		kParamSamplePitch
	);

	parameters.addParameter(
		STR16("Sample Volume"),
		STR16("%"),
		0,
		1.0,
		Steinberg::Vst::ParameterInfo::kCanAutomate,
		kParamSampleVolume
	);

	parameters.addParameter(
		STR16("Sample Attack"),
		nullptr,
		15, // ADSR attack rate 0-15
		0.0,
		Steinberg::Vst::ParameterInfo::kCanAutomate,
		kParamSampleAttack
	);

	parameters.addParameter(
		STR16("Sample Decay"),
		nullptr,
		7, // ADSR decay rate 0-7
		0.0,
		Steinberg::Vst::ParameterInfo::kCanAutomate,
		kParamSampleDecay
	);

	parameters.addParameter(
		STR16("Sample Sustain"),
		nullptr,
		7, // ADSR sustain level 0-7
		7.0 / 7.0, // Default max sustain
		Steinberg::Vst::ParameterInfo::kCanAutomate,
		kParamSampleSustain
	);

	parameters.addParameter(
		STR16("Sample Release"),
		nullptr,
		31, // ADSR release rate 0-31
		0.0,
		Steinberg::Vst::ParameterInfo::kCanAutomate,
		kParamSampleRelease
	);

	parameters.addParameter(
		STR16("Sample Trigger"),
		nullptr,
		1,
		0.0,
		Steinberg::Vst::ParameterInfo::kCanAutomate,
		kParamSampleTrigger
	);

	// View mode (0=Mixer, 1=Samples, 2=Browser)
	parameters.addParameter(
		STR16("View Mode"),
		nullptr,
		2, // 3 modes
		0.0,
		0, // Not automatable
		kParamViewMode
	);

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

Steinberg::tresult PLUGIN_API SpcController::notify(Steinberg::Vst::IMessage* message) {
	if (!message) {
		return Steinberg::kResultFalse;
	}

	const char* msgId = message->getMessageID();

	if (strcmp(msgId, kMsgSpcLoaded) == 0) {
		spcLoaded_ = true;
		// TODO: Update UI to reflect loaded state
		return Steinberg::kResultOk;
	}

	if (strcmp(msgId, kMsgSpcError) == 0) {
		spcLoaded_ = false;
		// TODO: Show error in UI
		return Steinberg::kResultOk;
	}

	if (strcmp(msgId, kMsgWaveformData) == 0) {
		// Receive waveform data from processor
		Steinberg::int64 sampleCount = 0;
		if (message->getAttributes()->getInt(kAttrWaveformSamples, sampleCount) == Steinberg::kResultOk && sampleCount > 0) {
			const void* leftData = nullptr;
			const void* rightData = nullptr;
			Steinberg::uint32 leftSize = 0, rightSize = 0;

			if (message->getAttributes()->getBinary(kAttrWaveformLeft, leftData, leftSize) == Steinberg::kResultOk &&
				message->getAttributes()->getBinary(kAttrWaveformRight, rightData, rightSize) == Steinberg::kResultOk) {

				std::lock_guard<std::mutex> lock(waveformMutex_);
				size_t count = static_cast<size_t>(sampleCount);
				waveformLeft_.resize(count);
				waveformRight_.resize(count);
				std::memcpy(waveformLeft_.data(), leftData, count * sizeof(float));
				std::memcpy(waveformRight_.data(), rightData, count * sizeof(float));
			}
		}
		return Steinberg::kResultOk;
	}

	return EditController::notify(message);
}

bool SpcController::loadSpcFile(const char* filePath) {
	if (!filePath) return false;

	currentSpcPath_ = filePath;

	// Send message to processor to load the file
	if (auto* msg = allocateMessage()) {
		msg->setMessageID(kMsgLoadSpcFile);
		msg->getAttributes()->setBinary(kAttrFilePath, filePath, static_cast<Steinberg::uint32>(strlen(filePath)));
		sendMessage(msg);
		msg->release();
		return true;
	}
	return false;
}

bool SpcController::loadSpcData(const uint8_t* data, int length) {
	if (!data || length <= 0) return false;

	// Send message to processor to load the data
	if (auto* msg = allocateMessage()) {
		msg->setMessageID(kMsgLoadSpcData);
		msg->getAttributes()->setBinary(kAttrSpcData, data, static_cast<Steinberg::uint32>(length));
		sendMessage(msg);
		msg->release();
		return true;
	}
	return false;
}

#if VSTGUI_ENABLE
Steinberg::IPlugView* PLUGIN_API SpcController::createView(Steinberg::FIDString name) {
	if (Steinberg::FIDStringsEqual(name, Steinberg::Vst::ViewType::kEditor)) {
		return new SpcEditor(this, "SpcEditorView", "spc_editor.uidesc");
	}
	return nullptr;
}
#endif

void SpcController::startMidiLearn(int paramId) {
#if VSTGUI_ENABLE
	if (midiLearnHandler_) {
		midiLearnHandler_->startLearn(paramId);
	}
#endif
}

void SpcController::cancelMidiLearn() {
#if VSTGUI_ENABLE
	if (midiLearnHandler_) {
		midiLearnHandler_->cancelLearn();
	}
#endif
}

bool SpcController::processMidiCC(int channel, int ccNumber, int value) {
#if VSTGUI_ENABLE
	if (midiLearnHandler_) {
		return midiLearnHandler_->processMidiCC(channel, ccNumber, value);
	}
#endif
	return false;
}

void SpcController::requestWaveformData() {
	if (auto* msg = allocateMessage()) {
		msg->setMessageID(kMsgRequestWaveform);
		sendMessage(msg);
		msg->release();
	}
}

bool SpcController::getWaveformData(std::vector<float>& left, std::vector<float>& right) {
	std::lock_guard<std::mutex> lock(waveformMutex_);
	if (waveformLeft_.empty()) {
		return false;
	}
	left = waveformLeft_;
	right = waveformRight_;
	return true;
}

} // namespace SnesSpc
