#pragma once

// .NET Runtime Host for loading and calling SpcPlugin.Core
// This uses the .NET Native AOT or hostfxr API to load the managed assembly
// Function signatures match NativeExports.cs UnmanagedCallersOnly methods

#include <cstdint>
#include <string>

namespace SnesSpc {

class DotNetHost {
public:
	DotNetHost();
	~DotNetHost();

	// Initialize by loading the native library
	bool initialize(const char* libraryPath);

	// Shutdown and unload
	void shutdown();

	// Check if initialized
	bool isInitialized() const { return libraryHandle_ != nullptr; }

	// === Engine Lifecycle ===
	// Returns engine handle (0 on failure)
	intptr_t createEngine(int sampleRate);
	void destroyEngine(intptr_t engine);

	// === SPC Loading ===
	bool loadSpcData(intptr_t engine, const uint8_t* data, int length);
	bool loadSpcFile(intptr_t engine, const char* filePath);

	// === Playback Control ===
	void play(intptr_t engine);
	void pause(intptr_t engine);
	void stop(intptr_t engine);
	bool isPlaying(intptr_t engine);
	void seek(intptr_t engine, double seconds);
	double getPosition(intptr_t engine);

	// === Audio Generation ===
	void process(intptr_t engine, float* output, int sampleCount);

	// === Master Controls ===
	void setMasterVolume(intptr_t engine, float volume);
	float getMasterVolume(intptr_t engine);
	void setLoopEnabled(intptr_t engine, bool enabled);
	bool getLoopEnabled(intptr_t engine);

	// === Voice Control ===
	void setVoiceMuted(intptr_t engine, int voice, bool muted);
	bool getVoiceMuted(intptr_t engine, int voice);
	void setVoiceSolo(intptr_t engine, int voice, bool solo);
	bool getVoiceSolo(intptr_t engine, int voice);
	void setVoiceVolume(intptr_t engine, int voice, float volume);
	float getVoiceVolume(intptr_t engine, int voice);
	void muteAll(intptr_t engine);
	void unmuteAll(intptr_t engine);
	void clearSolo(intptr_t engine);

	// === DAW Sync ===
	void setHostTempo(intptr_t engine, double bpm);
	void setTimeSignature(intptr_t engine, double numerator, double denominator);
	double getPositionBeats(intptr_t engine);
	double getPositionBars(intptr_t engine);

	// === Info ===
	int64_t getTotalCycles(intptr_t engine);
	int getSampleRate(intptr_t engine);
	void setSampleRate(intptr_t engine, int sampleRate);

	// === MIDI ===
	void midiNoteOn(intptr_t engine, int channel, int note, int velocity);
	void midiNoteOff(intptr_t engine, int channel, int note, int velocity);
	void midiControlChange(intptr_t engine, int channel, int controller, int value);
	void midiPitchBend(intptr_t engine, int channel, int value);
	void midiSetPitchBendRange(intptr_t engine, int semitones);
	void midiReset(intptr_t engine);

private:
	void* libraryHandle_ = nullptr;

	// Function pointers matching NativeExports.cs
	using CreateEngineFunc = intptr_t (*)(int);
	using DestroyEngineFunc = void (*)(intptr_t);
	using LoadSpcDataFunc = int (*)(intptr_t, const uint8_t*, int);
	using LoadSpcFileFunc = int (*)(intptr_t, const uint8_t*, int);
	using PlayFunc = void (*)(intptr_t);
	using PauseFunc = void (*)(intptr_t);
	using StopFunc = void (*)(intptr_t);
	using IsPlayingFunc = int (*)(intptr_t);
	using SeekFunc = void (*)(intptr_t, double);
	using GetPositionFunc = double (*)(intptr_t);
	using ProcessFunc = void (*)(intptr_t, float*, int);
	using SetMasterVolumeFunc = void (*)(intptr_t, float);
	using GetMasterVolumeFunc = float (*)(intptr_t);
	using SetLoopEnabledFunc = void (*)(intptr_t, int);
	using GetLoopEnabledFunc = int (*)(intptr_t);
	using SetVoiceMutedFunc = void (*)(intptr_t, int, int);
	using GetVoiceMutedFunc = int (*)(intptr_t, int);
	using SetVoiceSoloFunc = void (*)(intptr_t, int, int);
	using GetVoiceSoloFunc = int (*)(intptr_t, int);
	using SetVoiceVolumeFunc = void (*)(intptr_t, int, float);
	using GetVoiceVolumeFunc = float (*)(intptr_t, int);
	using MuteAllFunc = void (*)(intptr_t);
	using UnmuteAllFunc = void (*)(intptr_t);
	using ClearSoloFunc = void (*)(intptr_t);
	using SetHostTempoFunc = void (*)(intptr_t, double);
	using SetTimeSignatureFunc = void (*)(intptr_t, double, double);
	using GetPositionBeatsFunc = double (*)(intptr_t);
	using GetPositionBarsFunc = double (*)(intptr_t);
	using GetTotalCyclesFunc = int64_t (*)(intptr_t);
	using GetSampleRateFunc = int (*)(intptr_t);
	using SetSampleRateFunc = void (*)(intptr_t, int);
	using MidiNoteOnFunc = void (*)(intptr_t, int, int, int);
	using MidiNoteOffFunc = void (*)(intptr_t, int, int, int);
	using MidiControlChangeFunc = void (*)(intptr_t, int, int, int);
	using MidiPitchBendFunc = void (*)(intptr_t, int, int);
	using MidiSetPitchBendRangeFunc = void (*)(intptr_t, int);
	using MidiResetFunc = void (*)(intptr_t);

	CreateEngineFunc createEngineFunc_ = nullptr;
	DestroyEngineFunc destroyEngineFunc_ = nullptr;
	LoadSpcDataFunc loadSpcDataFunc_ = nullptr;
	LoadSpcFileFunc loadSpcFileFunc_ = nullptr;
	PlayFunc playFunc_ = nullptr;
	PauseFunc pauseFunc_ = nullptr;
	StopFunc stopFunc_ = nullptr;
	IsPlayingFunc isPlayingFunc_ = nullptr;
	SeekFunc seekFunc_ = nullptr;
	GetPositionFunc getPositionFunc_ = nullptr;
	ProcessFunc processFunc_ = nullptr;
	SetMasterVolumeFunc setMasterVolumeFunc_ = nullptr;
	GetMasterVolumeFunc getMasterVolumeFunc_ = nullptr;
	SetLoopEnabledFunc setLoopEnabledFunc_ = nullptr;
	GetLoopEnabledFunc getLoopEnabledFunc_ = nullptr;
	SetVoiceMutedFunc setVoiceMutedFunc_ = nullptr;
	GetVoiceMutedFunc getVoiceMutedFunc_ = nullptr;
	SetVoiceSoloFunc setVoiceSoloFunc_ = nullptr;
	GetVoiceSoloFunc getVoiceSoloFunc_ = nullptr;
	SetVoiceVolumeFunc setVoiceVolumeFunc_ = nullptr;
	GetVoiceVolumeFunc getVoiceVolumeFunc_ = nullptr;
	MuteAllFunc muteAllFunc_ = nullptr;
	UnmuteAllFunc unmuteAllFunc_ = nullptr;
	ClearSoloFunc clearSoloFunc_ = nullptr;
	SetHostTempoFunc setHostTempoFunc_ = nullptr;
	SetTimeSignatureFunc setTimeSignatureFunc_ = nullptr;
	GetPositionBeatsFunc getPositionBeatsFunc_ = nullptr;
	GetPositionBarsFunc getPositionBarsFunc_ = nullptr;
	GetTotalCyclesFunc getTotalCyclesFunc_ = nullptr;
	GetSampleRateFunc getSampleRateFunc_ = nullptr;
	SetSampleRateFunc setSampleRateFunc_ = nullptr;
	MidiNoteOnFunc midiNoteOnFunc_ = nullptr;
	MidiNoteOffFunc midiNoteOffFunc_ = nullptr;
	MidiControlChangeFunc midiControlChangeFunc_ = nullptr;
	MidiPitchBendFunc midiPitchBendFunc_ = nullptr;
	MidiSetPitchBendRangeFunc midiSetPitchBendRangeFunc_ = nullptr;
	MidiResetFunc midiResetFunc_ = nullptr;

	// Helper to load function pointer
	template<typename T>
	T loadFunction(const char* name);
};

} // namespace SnesSpc
