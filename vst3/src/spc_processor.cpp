#include "spc_processor.h"
#include "spc_ids.h"
#include "pluginterfaces/vst/ivstparameterchanges.h"
#include "pluginterfaces/vst/ivstprocesscontext.h"
#include <filesystem>

namespace SnesSpc {

SpcProcessor::SpcProcessor() {
	setControllerClass(kControllerUID);
}

SpcProcessor::~SpcProcessor() {
	if (engineHandle_ && dotnetHost_) {
		dotnetHost_->destroyEngine(engineHandle_);
		engineHandle_ = 0;
	}
}

Steinberg::tresult PLUGIN_API SpcProcessor::initialize(Steinberg::FUnknown* context) {
	Steinberg::tresult result = AudioEffect::initialize(context);
	if (result != Steinberg::kResultOk) {
		return result;
	}

	// Add stereo audio output
	addAudioOutput(STR16("Stereo Output"), Steinberg::Vst::SpeakerArr::kStereo);

	// Initialize .NET host
	dotnetHost_ = std::make_unique<DotNetHost>();

	// Find the native library next to the VST3 plugin
	// The library will be built as SpcPlugin.Core.dll (Native AOT)
	// TODO: Make this path configurable or detect properly
#ifdef _WIN32
	const char* libraryName = "SpcPlugin.Core.dll";
#else
	const char* libraryName = "libSpcPlugin.Core.so";
#endif

	// For now, assume library is in same directory as plugin
	// In production, this should be properly packaged with the VST3
	if (!dotnetHost_->initialize(libraryName)) {
		// Initialization failed, but don't fail the whole plugin
		// It will just output silence until a library is found
		dotnetHost_.reset();
	}

	return Steinberg::kResultOk;
}

Steinberg::tresult PLUGIN_API SpcProcessor::terminate() {
	if (engineHandle_ && dotnetHost_) {
		dotnetHost_->destroyEngine(engineHandle_);
		engineHandle_ = 0;
	}
	dotnetHost_.reset();
	return AudioEffect::terminate();
}

Steinberg::tresult PLUGIN_API SpcProcessor::setActive(Steinberg::TBool state) {
	if (state) {
		// Plugin activated - create engine if we have a host
		if (dotnetHost_ && !engineHandle_) {
			engineHandle_ = dotnetHost_->createEngine(static_cast<int>(sampleRate_));
			if (engineHandle_) {
				syncParametersToEngine();
			}
		}
	} else {
		// Plugin deactivated - destroy engine to save resources
		if (dotnetHost_ && engineHandle_) {
			dotnetHost_->destroyEngine(engineHandle_);
			engineHandle_ = 0;
		}
	}
	return AudioEffect::setActive(state);
}

Steinberg::tresult PLUGIN_API SpcProcessor::setupProcessing(Steinberg::Vst::ProcessSetup& setup) {
	sampleRate_ = static_cast<float>(setup.sampleRate);

	// Update engine sample rate if active
	if (dotnetHost_ && engineHandle_) {
		dotnetHost_->setSampleRate(engineHandle_, static_cast<int>(sampleRate_));
	}

	return AudioEffect::setupProcessing(setup);
}

Steinberg::tresult PLUGIN_API SpcProcessor::process(Steinberg::Vst::ProcessData& data) {
	// Process parameter changes
	if (data.inputParameterChanges) {
		Steinberg::int32 numParams = data.inputParameterChanges->getParameterCount();
		for (Steinberg::int32 i = 0; i < numParams; i++) {
			auto* paramQueue = data.inputParameterChanges->getParameterData(i);
			if (paramQueue) {
				Steinberg::Vst::ParamID paramId = paramQueue->getParameterId();
				Steinberg::int32 numPoints = paramQueue->getPointCount();
				Steinberg::Vst::ParamValue value;
				Steinberg::int32 sampleOffset;
				if (paramQueue->getPoint(numPoints - 1, sampleOffset, value) == Steinberg::kResultTrue) {
					switch (paramId) {
						case kParamMasterVolume:
							masterVolume_ = static_cast<float>(value);
							if (dotnetHost_ && engineHandle_) {
								dotnetHost_->setMasterVolume(engineHandle_, masterVolume_);
							}
							break;
						case kParamPlayPause:
							playing_ = value > 0.5;
							if (dotnetHost_ && engineHandle_) {
								if (playing_) {
									dotnetHost_->play(engineHandle_);
								} else {
									dotnetHost_->pause(engineHandle_);
								}
							}
							break;
						case kParamLoop:
							looping_ = value > 0.5;
							if (dotnetHost_ && engineHandle_) {
								dotnetHost_->setLoopEnabled(engineHandle_, looping_);
							}
							break;
						// Handle voice mute (enable/disable)
						case kParamVoice0: case kParamVoice1: case kParamVoice2: case kParamVoice3:
						case kParamVoice4: case kParamVoice5: case kParamVoice6: case kParamVoice7: {
							int voice = paramId - kParamVoice0;
							voiceEnabled_[voice] = value > 0.5;
							if (dotnetHost_ && engineHandle_) {
								dotnetHost_->setVoiceMuted(engineHandle_, voice, !voiceEnabled_[voice]);
							}
							break;
						}
						// Handle voice solo
						case kParamSolo0: case kParamSolo1: case kParamSolo2: case kParamSolo3:
						case kParamSolo4: case kParamSolo5: case kParamSolo6: case kParamSolo7: {
							int voice = paramId - kParamSolo0;
							voiceSolo_[voice] = value > 0.5;
							if (dotnetHost_ && engineHandle_) {
								dotnetHost_->setVoiceSolo(engineHandle_, voice, voiceSolo_[voice]);
							}
							break;
						}
					}
				}
			}
		}
	}

	// Sync host transport info (tempo, time signature)
	if (data.processContext) {
		if (data.processContext->state & Steinberg::Vst::ProcessContext::kTempoValid) {
			if (dotnetHost_ && engineHandle_) {
				dotnetHost_->setHostTempo(engineHandle_, data.processContext->tempo);
			}
		}
		if (data.processContext->state & Steinberg::Vst::ProcessContext::kTimeSigValid) {
			if (dotnetHost_ && engineHandle_) {
				dotnetHost_->setTimeSignature(
					engineHandle_,
					static_cast<double>(data.processContext->timeSigNumerator),
					static_cast<double>(data.processContext->timeSigDenominator)
				);
			}
		}
	}

	// Generate audio output
	if (data.numOutputs == 0 || data.outputs[0].numChannels < 2) {
		return Steinberg::kResultOk;
	}

	float* leftChannel = data.outputs[0].channelBuffers32[0];
	float* rightChannel = data.outputs[0].channelBuffers32[1];
	Steinberg::int32 numSamples = data.numSamples;

	// If not playing or no engine, output silence
	if (!playing_ || !dotnetHost_ || !engineHandle_) {
		for (Steinberg::int32 i = 0; i < numSamples; i++) {
			leftChannel[i] = 0.0f;
			rightChannel[i] = 0.0f;
		}
		return Steinberg::kResultOk;
	}

	// Ensure interleaved buffer is large enough
	size_t requiredSize = static_cast<size_t>(numSamples) * 2;
	if (interleavedBuffer_.size() < requiredSize) {
		interleavedBuffer_.resize(requiredSize);
	}

	// Call .NET engine to generate interleaved stereo samples
	dotnetHost_->process(engineHandle_, interleavedBuffer_.data(), numSamples);

	// Deinterleave to VST3 separate channels
	for (Steinberg::int32 i = 0; i < numSamples; i++) {
		leftChannel[i] = interleavedBuffer_[i * 2];
		rightChannel[i] = interleavedBuffer_[i * 2 + 1];
	}

	return Steinberg::kResultOk;
}

Steinberg::tresult PLUGIN_API SpcProcessor::canProcessSampleSize(Steinberg::int32 symbolicSampleSize) {
	if (symbolicSampleSize == Steinberg::Vst::kSample32) {
		return Steinberg::kResultTrue;
	}
	return Steinberg::kResultFalse;
}

Steinberg::tresult PLUGIN_API SpcProcessor::setState(Steinberg::IBStream* state) {
	if (!state) {
		return Steinberg::kResultFalse;
	}

	// Read state version
	Steinberg::int32 version = 0;
	state->read(&version, sizeof(version));

	// Read master volume
	state->read(&masterVolume_, sizeof(masterVolume_));

	// Read playback state
	Steinberg::int8 playingByte = 0;
	state->read(&playingByte, sizeof(playingByte));
	playing_ = playingByte != 0;

	// Read loop state
	Steinberg::int8 loopingByte = 0;
	state->read(&loopingByte, sizeof(loopingByte));
	looping_ = loopingByte != 0;

	// Read voice states
	for (int i = 0; i < 8; i++) {
		Steinberg::int8 enabled = 1;
		state->read(&enabled, sizeof(enabled));
		voiceEnabled_[i] = enabled != 0;

		Steinberg::int8 solo = 0;
		state->read(&solo, sizeof(solo));
		voiceSolo_[i] = solo != 0;
	}

	// TODO: Read embedded SPC data

	// Sync to engine
	syncParametersToEngine();

	return Steinberg::kResultOk;
}

Steinberg::tresult PLUGIN_API SpcProcessor::getState(Steinberg::IBStream* state) {
	if (!state) {
		return Steinberg::kResultFalse;
	}

	// Write state version
	Steinberg::int32 version = 1;
	state->write(&version, sizeof(version));

	// Write master volume
	state->write(&masterVolume_, sizeof(masterVolume_));

	// Write playback state
	Steinberg::int8 playingByte = playing_ ? 1 : 0;
	state->write(&playingByte, sizeof(playingByte));

	// Write loop state
	Steinberg::int8 loopingByte = looping_ ? 1 : 0;
	state->write(&loopingByte, sizeof(loopingByte));

	// Write voice states
	for (int i = 0; i < 8; i++) {
		Steinberg::int8 enabled = voiceEnabled_[i] ? 1 : 0;
		state->write(&enabled, sizeof(enabled));

		Steinberg::int8 solo = voiceSolo_[i] ? 1 : 0;
		state->write(&solo, sizeof(solo));
	}

	// TODO: Write embedded SPC data

	return Steinberg::kResultOk;
}

bool SpcProcessor::loadSpcFile(const char* filePath) {
	if (!dotnetHost_ || !engineHandle_) {
		return false;
	}
	return dotnetHost_->loadSpcFile(engineHandle_, filePath);
}

bool SpcProcessor::loadSpcData(const uint8_t* data, int length) {
	if (!dotnetHost_ || !engineHandle_) {
		return false;
	}
	return dotnetHost_->loadSpcData(engineHandle_, data, length);
}

void SpcProcessor::syncParametersToEngine() {
	if (!dotnetHost_ || !engineHandle_) {
		return;
	}

	dotnetHost_->setMasterVolume(engineHandle_, masterVolume_);
	dotnetHost_->setLoopEnabled(engineHandle_, looping_);

	if (playing_) {
		dotnetHost_->play(engineHandle_);
	} else {
		dotnetHost_->pause(engineHandle_);
	}

	for (int i = 0; i < 8; i++) {
		dotnetHost_->setVoiceMuted(engineHandle_, i, !voiceEnabled_[i]);
		dotnetHost_->setVoiceSolo(engineHandle_, i, voiceSolo_[i]);
	}
}

} // namespace SnesSpc
