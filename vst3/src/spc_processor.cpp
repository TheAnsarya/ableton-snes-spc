#include "spc_processor.h"
#include "spc_ids.h"
#include "spc_messages.h"
#include "pluginterfaces/vst/ivstparameterchanges.h"
#include "pluginterfaces/vst/ivstprocesscontext.h"
#include <filesystem>
#include <cstring>

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
						// Handle voice volume
						case kParamVoiceVol0: case kParamVoiceVol1: case kParamVoiceVol2: case kParamVoiceVol3:
						case kParamVoiceVol4: case kParamVoiceVol5: case kParamVoiceVol6: case kParamVoiceVol7: {
							int voice = paramId - kParamVoiceVol0;
							voiceVolume_[voice] = static_cast<float>(value);
							if (dotnetHost_ && engineHandle_) {
								dotnetHost_->setVoiceVolume(engineHandle_, voice, voiceVolume_[voice]);
							}
							break;
						}
						// Handle pitch bend (per channel)
						case kParamPitchBend0: case kParamPitchBend1: case kParamPitchBend2: case kParamPitchBend3:
						case kParamPitchBend4: case kParamPitchBend5: case kParamPitchBend6: case kParamPitchBend7: {
							int channel = paramId - kParamPitchBend0;
							// Convert 0-1 to MIDI pitch bend (0-16383, center at 8192)
							int pitchBendValue = static_cast<int>(value * 16383);
							if (dotnetHost_ && engineHandle_) {
								dotnetHost_->midiPitchBend(engineHandle_, channel, pitchBendValue);
							}
							break;
						}
						// Handle pitch bend range
						case kParamPitchBendRange: {
							// Map 0-1 to 1-24 semitones
							int semitones = 1 + static_cast<int>(value * 23);
							if (dotnetHost_ && engineHandle_) {
								dotnetHost_->midiSetPitchBendRange(engineHandle_, semitones);
							}
							break;
						}
					}
				}
			}
		}
	}

	// Process MIDI events
	if (data.inputEvents) {
		processMidiEvents(data.inputEvents);
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

Steinberg::tresult PLUGIN_API SpcProcessor::notify(Steinberg::Vst::IMessage* message) {
	if (!message) {
		return Steinberg::kResultFalse;
	}

	const char* msgId = message->getMessageID();

	if (strcmp(msgId, kMsgLoadSpcFile) == 0) {
		// Load SPC from file path
		const void* data = nullptr;
		Steinberg::uint32 size = 0;
		if (message->getAttributes()->getBinary(kAttrFilePath, data, size) == Steinberg::kResultOk) {
			std::string filePath(static_cast<const char*>(data), size);
			if (loadSpcFile(filePath.c_str())) {
				// Send success notification back
				if (auto* reply = allocateMessage()) {
					reply->setMessageID(kMsgSpcLoaded);
					sendMessage(reply);
					reply->release();
				}
			}
		}
		return Steinberg::kResultOk;
	}

	if (strcmp(msgId, kMsgLoadSpcData) == 0) {
		// Load SPC from binary data
		const void* data = nullptr;
		Steinberg::uint32 size = 0;
		if (message->getAttributes()->getBinary(kAttrSpcData, data, size) == Steinberg::kResultOk) {
			if (loadSpcData(static_cast<const uint8_t*>(data), static_cast<int>(size))) {
				// Send success notification back
				if (auto* reply = allocateMessage()) {
					reply->setMessageID(kMsgSpcLoaded);
					sendMessage(reply);
					reply->release();
				}
			}
		}
		return Steinberg::kResultOk;
	}

	return AudioEffect::notify(message);
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

	// Version 2+: Voice volumes and embedded SPC data
	if (version >= 2) {
		// Read voice volumes
		for (int i = 0; i < 8; i++) {
			state->read(&voiceVolume_[i], sizeof(float));
		}

		// Read embedded SPC data length
		Steinberg::int32 spcDataLength = 0;
		state->read(&spcDataLength, sizeof(spcDataLength));

		if (spcDataLength > 0 && spcDataLength < 0x20000) { // Max 128KB
			embeddedSpcData_.resize(spcDataLength);
			state->read(embeddedSpcData_.data(), spcDataLength);

			// Load into engine if available
			if (dotnetHost_ && engineHandle_) {
				dotnetHost_->loadSpcData(engineHandle_, embeddedSpcData_.data(), spcDataLength);
			}
		}
	}

	// Sync to engine
	syncParametersToEngine();

	return Steinberg::kResultOk;
}

Steinberg::tresult PLUGIN_API SpcProcessor::getState(Steinberg::IBStream* state) {
	if (!state) {
		return Steinberg::kResultFalse;
	}

	// Write state version (2 = with voice volumes and SPC data)
	Steinberg::int32 version = 2;
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

	// Version 2: Voice volumes
	for (int i = 0; i < 8; i++) {
		state->write(&voiceVolume_[i], sizeof(float));
	}

	// Version 2: Embedded SPC data
	Steinberg::int32 spcDataLength = static_cast<Steinberg::int32>(embeddedSpcData_.size());
	state->write(&spcDataLength, sizeof(spcDataLength));
	if (spcDataLength > 0) {
		state->write(embeddedSpcData_.data(), spcDataLength);
	}

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
	bool result = dotnetHost_->loadSpcData(engineHandle_, data, length);
	if (result) {
		// Store for state save
		embeddedSpcData_.assign(data, data + length);
	}
	return result;
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
		dotnetHost_->setVoiceVolume(engineHandle_, i, voiceVolume_[i]);
	}
}

void SpcProcessor::processMidiEvents(Steinberg::Vst::IEventList* events) {
	if (!events || !dotnetHost_ || !engineHandle_) {
		return;
	}

	Steinberg::int32 eventCount = events->getEventCount();
	for (Steinberg::int32 i = 0; i < eventCount; i++) {
		Steinberg::Vst::Event event;
		if (events->getEvent(i, event) == Steinberg::kResultOk) {
			switch (event.type) {
				case Steinberg::Vst::Event::kNoteOnEvent:
					dotnetHost_->midiNoteOn(
						engineHandle_,
						event.noteOn.channel,
						event.noteOn.pitch,
						static_cast<int>(event.noteOn.velocity * 127)
					);
					break;

				case Steinberg::Vst::Event::kNoteOffEvent:
					dotnetHost_->midiNoteOff(
						engineHandle_,
						event.noteOff.channel,
						event.noteOff.pitch,
						static_cast<int>(event.noteOff.velocity * 127)
					);
					break;

				case Steinberg::Vst::Event::kLegacyMIDICCOutEvent:
					// Handle CC messages
					dotnetHost_->midiControlChange(
						engineHandle_,
						event.midiCCOut.channel,
						event.midiCCOut.controlNumber,
						event.midiCCOut.value
					);
					break;

				case Steinberg::Vst::Event::kPolyPressureEvent:
					// Aftertouch - map to CC
					dotnetHost_->midiControlChange(
						engineHandle_,
						event.polyPressure.channel,
						1, // Modulation wheel
						static_cast<int>(event.polyPressure.pressure * 127)
					);
					break;

				default:
					// Ignore other event types for now
					break;
			}
		}
	}
}

} // namespace SnesSpc
