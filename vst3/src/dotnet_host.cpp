#include "dotnet_host.h"

#ifdef _WIN32
#include <Windows.h>
#define LOAD_LIBRARY(path) LoadLibraryA(path)
#define GET_PROC(lib, name) GetProcAddress((HMODULE)lib, name)
#define FREE_LIBRARY(lib) FreeLibrary((HMODULE)lib)
#else
#include <dlfcn.h>
#define LOAD_LIBRARY(path) dlopen(path, RTLD_NOW)
#define GET_PROC(lib, name) dlsym(lib, name)
#define FREE_LIBRARY(lib) dlclose(lib)
#endif

#include <cstring>

namespace SnesSpc {

DotNetHost::DotNetHost() = default;

DotNetHost::~DotNetHost() {
	shutdown();
}

template<typename T>
T DotNetHost::loadFunction(const char* name) {
	if (!libraryHandle_) return nullptr;
	return reinterpret_cast<T>(GET_PROC(libraryHandle_, name));
}

bool DotNetHost::initialize(const char* libraryPath) {
	if (isInitialized()) {
		shutdown();
	}

	libraryHandle_ = LOAD_LIBRARY(libraryPath);
	if (!libraryHandle_) {
		return false;
	}

	// Load all function pointers
	createEngineFunc_ = loadFunction<CreateEngineFunc>("spc_engine_create");
	destroyEngineFunc_ = loadFunction<DestroyEngineFunc>("spc_engine_destroy");
	loadSpcDataFunc_ = loadFunction<LoadSpcDataFunc>("spc_load_data");
	loadSpcFileFunc_ = loadFunction<LoadSpcFileFunc>("spc_load_file");
	playFunc_ = loadFunction<PlayFunc>("spc_play");
	pauseFunc_ = loadFunction<PauseFunc>("spc_pause");
	stopFunc_ = loadFunction<StopFunc>("spc_stop");
	isPlayingFunc_ = loadFunction<IsPlayingFunc>("spc_is_playing");
	seekFunc_ = loadFunction<SeekFunc>("spc_seek");
	getPositionFunc_ = loadFunction<GetPositionFunc>("spc_get_position");
	processFunc_ = loadFunction<ProcessFunc>("spc_process");
	setMasterVolumeFunc_ = loadFunction<SetMasterVolumeFunc>("spc_set_master_volume");
	getMasterVolumeFunc_ = loadFunction<GetMasterVolumeFunc>("spc_get_master_volume");
	setLoopEnabledFunc_ = loadFunction<SetLoopEnabledFunc>("spc_set_loop_enabled");
	getLoopEnabledFunc_ = loadFunction<GetLoopEnabledFunc>("spc_get_loop_enabled");
	setVoiceMutedFunc_ = loadFunction<SetVoiceMutedFunc>("spc_set_voice_muted");
	getVoiceMutedFunc_ = loadFunction<GetVoiceMutedFunc>("spc_get_voice_muted");
	setVoiceSoloFunc_ = loadFunction<SetVoiceSoloFunc>("spc_set_voice_solo");
	getVoiceSoloFunc_ = loadFunction<GetVoiceSoloFunc>("spc_get_voice_solo");
	setVoiceVolumeFunc_ = loadFunction<SetVoiceVolumeFunc>("spc_set_voice_volume");
	getVoiceVolumeFunc_ = loadFunction<GetVoiceVolumeFunc>("spc_get_voice_volume");
	muteAllFunc_ = loadFunction<MuteAllFunc>("spc_mute_all");
	unmuteAllFunc_ = loadFunction<UnmuteAllFunc>("spc_unmute_all");
	clearSoloFunc_ = loadFunction<ClearSoloFunc>("spc_clear_solo");
	setHostTempoFunc_ = loadFunction<SetHostTempoFunc>("spc_set_host_tempo");
	setTimeSignatureFunc_ = loadFunction<SetTimeSignatureFunc>("spc_set_time_signature");
	getPositionBeatsFunc_ = loadFunction<GetPositionBeatsFunc>("spc_get_position_beats");
	getPositionBarsFunc_ = loadFunction<GetPositionBarsFunc>("spc_get_position_bars");
	getTotalCyclesFunc_ = loadFunction<GetTotalCyclesFunc>("spc_get_total_cycles");
	getSampleRateFunc_ = loadFunction<GetSampleRateFunc>("spc_get_sample_rate");
	setSampleRateFunc_ = loadFunction<SetSampleRateFunc>("spc_set_sample_rate");
	midiNoteOnFunc_ = loadFunction<MidiNoteOnFunc>("spc_midi_note_on");
	midiNoteOffFunc_ = loadFunction<MidiNoteOffFunc>("spc_midi_note_off");
	midiControlChangeFunc_ = loadFunction<MidiControlChangeFunc>("spc_midi_cc");
	midiPitchBendFunc_ = loadFunction<MidiPitchBendFunc>("spc_midi_pitch_bend");
	midiSetPitchBendRangeFunc_ = loadFunction<MidiSetPitchBendRangeFunc>("spc_midi_set_pitch_bend_range");
	midiResetFunc_ = loadFunction<MidiResetFunc>("spc_midi_reset");

	// At minimum we need create, destroy, and process
	if (!createEngineFunc_ || !destroyEngineFunc_ || !processFunc_) {
		shutdown();
		return false;
	}

	return true;
}

void DotNetHost::shutdown() {
	if (libraryHandle_) {
		FREE_LIBRARY(libraryHandle_);
		libraryHandle_ = nullptr;
	}

	// Clear all function pointers
	createEngineFunc_ = nullptr;
	destroyEngineFunc_ = nullptr;
	loadSpcDataFunc_ = nullptr;
	loadSpcFileFunc_ = nullptr;
	playFunc_ = nullptr;
	pauseFunc_ = nullptr;
	stopFunc_ = nullptr;
	isPlayingFunc_ = nullptr;
	seekFunc_ = nullptr;
	getPositionFunc_ = nullptr;
	processFunc_ = nullptr;
	setMasterVolumeFunc_ = nullptr;
	getMasterVolumeFunc_ = nullptr;
	setLoopEnabledFunc_ = nullptr;
	getLoopEnabledFunc_ = nullptr;
	setVoiceMutedFunc_ = nullptr;
	getVoiceMutedFunc_ = nullptr;
	setVoiceSoloFunc_ = nullptr;
	getVoiceSoloFunc_ = nullptr;
	setVoiceVolumeFunc_ = nullptr;
	getVoiceVolumeFunc_ = nullptr;
	muteAllFunc_ = nullptr;
	unmuteAllFunc_ = nullptr;
	clearSoloFunc_ = nullptr;
	setHostTempoFunc_ = nullptr;
	setTimeSignatureFunc_ = nullptr;
	getPositionBeatsFunc_ = nullptr;
	getPositionBarsFunc_ = nullptr;
	getTotalCyclesFunc_ = nullptr;
	getSampleRateFunc_ = nullptr;
	setSampleRateFunc_ = nullptr;
	midiNoteOnFunc_ = nullptr;
	midiNoteOffFunc_ = nullptr;
	midiControlChangeFunc_ = nullptr;
	midiPitchBendFunc_ = nullptr;
	midiSetPitchBendRangeFunc_ = nullptr;
	midiResetFunc_ = nullptr;
}

// === Engine Lifecycle ===

intptr_t DotNetHost::createEngine(int sampleRate) {
	if (createEngineFunc_) {
		return createEngineFunc_(sampleRate);
	}
	return 0;
}

void DotNetHost::destroyEngine(intptr_t engine) {
	if (destroyEngineFunc_ && engine) {
		destroyEngineFunc_(engine);
	}
}

// === SPC Loading ===

bool DotNetHost::loadSpcData(intptr_t engine, const uint8_t* data, int length) {
	if (loadSpcDataFunc_ && engine && data && length > 0) {
		return loadSpcDataFunc_(engine, data, length) != 0;
	}
	return false;
}

bool DotNetHost::loadSpcFile(intptr_t engine, const char* filePath) {
	if (loadSpcFileFunc_ && engine && filePath) {
		int pathLength = static_cast<int>(strlen(filePath));
		return loadSpcFileFunc_(engine, reinterpret_cast<const uint8_t*>(filePath), pathLength) != 0;
	}
	return false;
}

// === Playback Control ===

void DotNetHost::play(intptr_t engine) {
	if (playFunc_ && engine) {
		playFunc_(engine);
	}
}

void DotNetHost::pause(intptr_t engine) {
	if (pauseFunc_ && engine) {
		pauseFunc_(engine);
	}
}

void DotNetHost::stop(intptr_t engine) {
	if (stopFunc_ && engine) {
		stopFunc_(engine);
	}
}

bool DotNetHost::isPlaying(intptr_t engine) {
	if (isPlayingFunc_ && engine) {
		return isPlayingFunc_(engine) != 0;
	}
	return false;
}

void DotNetHost::seek(intptr_t engine, double seconds) {
	if (seekFunc_ && engine) {
		seekFunc_(engine, seconds);
	}
}

double DotNetHost::getPosition(intptr_t engine) {
	if (getPositionFunc_ && engine) {
		return getPositionFunc_(engine);
	}
	return 0.0;
}

// === Audio Generation ===

void DotNetHost::process(intptr_t engine, float* output, int sampleCount) {
	if (processFunc_ && engine && output && sampleCount > 0) {
		processFunc_(engine, output, sampleCount);
	}
}

// === Master Controls ===

void DotNetHost::setMasterVolume(intptr_t engine, float volume) {
	if (setMasterVolumeFunc_ && engine) {
		setMasterVolumeFunc_(engine, volume);
	}
}

float DotNetHost::getMasterVolume(intptr_t engine) {
	if (getMasterVolumeFunc_ && engine) {
		return getMasterVolumeFunc_(engine);
	}
	return 1.0f;
}

void DotNetHost::setLoopEnabled(intptr_t engine, bool enabled) {
	if (setLoopEnabledFunc_ && engine) {
		setLoopEnabledFunc_(engine, enabled ? 1 : 0);
	}
}

bool DotNetHost::getLoopEnabled(intptr_t engine) {
	if (getLoopEnabledFunc_ && engine) {
		return getLoopEnabledFunc_(engine) != 0;
	}
	return true;
}

// === Voice Control ===

void DotNetHost::setVoiceMuted(intptr_t engine, int voice, bool muted) {
	if (setVoiceMutedFunc_ && engine && voice >= 0 && voice < 8) {
		setVoiceMutedFunc_(engine, voice, muted ? 1 : 0);
	}
}

bool DotNetHost::getVoiceMuted(intptr_t engine, int voice) {
	if (getVoiceMutedFunc_ && engine && voice >= 0 && voice < 8) {
		return getVoiceMutedFunc_(engine, voice) != 0;
	}
	return false;
}

void DotNetHost::setVoiceSolo(intptr_t engine, int voice, bool solo) {
	if (setVoiceSoloFunc_ && engine && voice >= 0 && voice < 8) {
		setVoiceSoloFunc_(engine, voice, solo ? 1 : 0);
	}
}

bool DotNetHost::getVoiceSolo(intptr_t engine, int voice) {
	if (getVoiceSoloFunc_ && engine && voice >= 0 && voice < 8) {
		return getVoiceSoloFunc_(engine, voice) != 0;
	}
	return false;
}

void DotNetHost::setVoiceVolume(intptr_t engine, int voice, float volume) {
	if (setVoiceVolumeFunc_ && engine && voice >= 0 && voice < 8) {
		setVoiceVolumeFunc_(engine, voice, volume);
	}
}

float DotNetHost::getVoiceVolume(intptr_t engine, int voice) {
	if (getVoiceVolumeFunc_ && engine && voice >= 0 && voice < 8) {
		return getVoiceVolumeFunc_(engine, voice);
	}
	return 1.0f;
}

void DotNetHost::muteAll(intptr_t engine) {
	if (muteAllFunc_ && engine) {
		muteAllFunc_(engine);
	}
}

void DotNetHost::unmuteAll(intptr_t engine) {
	if (unmuteAllFunc_ && engine) {
		unmuteAllFunc_(engine);
	}
}

void DotNetHost::clearSolo(intptr_t engine) {
	if (clearSoloFunc_ && engine) {
		clearSoloFunc_(engine);
	}
}

// === DAW Sync ===

void DotNetHost::setHostTempo(intptr_t engine, double bpm) {
	if (setHostTempoFunc_ && engine) {
		setHostTempoFunc_(engine, bpm);
	}
}

void DotNetHost::setTimeSignature(intptr_t engine, double numerator, double denominator) {
	if (setTimeSignatureFunc_ && engine) {
		setTimeSignatureFunc_(engine, numerator, denominator);
	}
}

double DotNetHost::getPositionBeats(intptr_t engine) {
	if (getPositionBeatsFunc_ && engine) {
		return getPositionBeatsFunc_(engine);
	}
	return 0.0;
}

double DotNetHost::getPositionBars(intptr_t engine) {
	if (getPositionBarsFunc_ && engine) {
		return getPositionBarsFunc_(engine);
	}
	return 0.0;
}

// === Info ===

int64_t DotNetHost::getTotalCycles(intptr_t engine) {
	if (getTotalCyclesFunc_ && engine) {
		return getTotalCyclesFunc_(engine);
	}
	return 0;
}

int DotNetHost::getSampleRate(intptr_t engine) {
	if (getSampleRateFunc_ && engine) {
		return getSampleRateFunc_(engine);
	}
	return 44100;
}

void DotNetHost::setSampleRate(intptr_t engine, int sampleRate) {
	if (setSampleRateFunc_ && engine) {
		setSampleRateFunc_(engine, sampleRate);
	}
}

// === MIDI ===

void DotNetHost::midiNoteOn(intptr_t engine, int channel, int note, int velocity) {
	if (midiNoteOnFunc_ && engine) {
		midiNoteOnFunc_(engine, channel, note, velocity);
	}
}

void DotNetHost::midiNoteOff(intptr_t engine, int channel, int note, int velocity) {
	if (midiNoteOffFunc_ && engine) {
		midiNoteOffFunc_(engine, channel, note, velocity);
	}
}

void DotNetHost::midiControlChange(intptr_t engine, int channel, int controller, int value) {
	if (midiControlChangeFunc_ && engine) {
		midiControlChangeFunc_(engine, channel, controller, value);
	}
}

void DotNetHost::midiPitchBend(intptr_t engine, int channel, int value) {
	if (midiPitchBendFunc_ && engine) {
		midiPitchBendFunc_(engine, channel, value);
	}
}

void DotNetHost::midiSetPitchBendRange(intptr_t engine, int semitones) {
	if (midiSetPitchBendRangeFunc_ && engine) {
		midiSetPitchBendRangeFunc_(engine, semitones);
	}
}

void DotNetHost::midiReset(intptr_t engine) {
	if (midiResetFunc_ && engine) {
		midiResetFunc_(engine);
	}
}

} // namespace SnesSpc
