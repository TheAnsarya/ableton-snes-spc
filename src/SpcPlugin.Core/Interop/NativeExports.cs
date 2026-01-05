using System.Runtime.InteropServices;
using SpcPlugin.Core.Audio;
using SpcPlugin.Core.Midi;

namespace SpcPlugin.Core.Interop;

/// <summary>
/// Native exports for VST3/C++ interop using UnmanagedCallersOnly.
/// These functions can be called directly from native code without marshaling overhead.
/// </summary>
public static unsafe class NativeExports {
	// Engine instance registry (GCHandle to prevent collection)
	private static readonly Dictionary<nint, GCHandle> _engines = [];
	private static readonly Dictionary<nint, MidiProcessor> _midiProcessors = [];
	private static int _nextId = 1;

	#region Engine Lifecycle

	/// <summary>
	/// Creates a new SPC engine instance.
	/// </summary>
	/// <returns>Handle to the engine (0 on failure).</returns>
	[UnmanagedCallersOnly(EntryPoint = "spc_engine_create")]
	public static nint CreateEngine(int sampleRate) {
		try {
			var engine = new SpcEngine(sampleRate);
			var handle = GCHandle.Alloc(engine);
			nint id = _nextId++;
			_engines[id] = handle;
			_midiProcessors[id] = new MidiProcessor(engine);
			return id;
		} catch {
			return 0;
		}
	}

	/// <summary>
	/// Destroys an SPC engine instance.
	/// </summary>
	[UnmanagedCallersOnly(EntryPoint = "spc_engine_destroy")]
	public static void DestroyEngine(nint engineId) {
		if (_engines.TryGetValue(engineId, out var handle)) {
			if (handle.Target is SpcEngine engine) {
				engine.Dispose();
			}
			handle.Free();
			_engines.Remove(engineId);
			_midiProcessors.Remove(engineId);
		}
	}

	#endregion

	#region SPC Loading

	/// <summary>
	/// Loads SPC data into the engine.
	/// </summary>
	/// <returns>1 on success, 0 on failure.</returns>
	[UnmanagedCallersOnly(EntryPoint = "spc_load_data")]
	public static int LoadSpcData(nint engineId, byte* data, int length) {
		try {
			if (GetEngine(engineId) is not { } engine) return 0;
			engine.LoadSpc(new ReadOnlySpan<byte>(data, length));
			return 1;
		} catch {
			return 0;
		}
	}

	/// <summary>
	/// Loads SPC file from path.
	/// </summary>
	[UnmanagedCallersOnly(EntryPoint = "spc_load_file")]
	public static int LoadSpcFile(nint engineId, byte* pathUtf8, int pathLength) {
		try {
			if (GetEngine(engineId) is not { } engine) return 0;
			string path = System.Text.Encoding.UTF8.GetString(pathUtf8, pathLength);
			engine.LoadSpcFile(path);
			return 1;
		} catch {
			return 0;
		}
	}

	#endregion

	#region Playback Control

	[UnmanagedCallersOnly(EntryPoint = "spc_play")]
	public static void Play(nint engineId) {
		GetEngine(engineId)?.Play();
	}

	[UnmanagedCallersOnly(EntryPoint = "spc_pause")]
	public static void Pause(nint engineId) {
		GetEngine(engineId)?.Pause();
	}

	[UnmanagedCallersOnly(EntryPoint = "spc_stop")]
	public static void Stop(nint engineId) {
		GetEngine(engineId)?.Stop();
	}

	[UnmanagedCallersOnly(EntryPoint = "spc_is_playing")]
	public static int IsPlaying(nint engineId) {
		return GetEngine(engineId)?.IsPlaying == true ? 1 : 0;
	}

	[UnmanagedCallersOnly(EntryPoint = "spc_seek")]
	public static void Seek(nint engineId, double seconds) {
		GetEngine(engineId)?.Seek(seconds);
	}

	[UnmanagedCallersOnly(EntryPoint = "spc_get_position")]
	public static double GetPosition(nint engineId) {
		return GetEngine(engineId)?.PositionSeconds ?? 0;
	}

	#endregion

	#region Audio Generation

	/// <summary>
	/// Generates audio samples (interleaved stereo float).
	/// </summary>
	[UnmanagedCallersOnly(EntryPoint = "spc_process")]
	public static void Process(nint engineId, float* output, int sampleCount) {
		if (GetEngine(engineId) is { } engine) {
			engine.Process(new Span<float>(output, sampleCount * 2), sampleCount);
		} else {
			// Fill with silence
			new Span<float>(output, sampleCount * 2).Clear();
		}
	}

	#endregion

	#region Master Controls

	[UnmanagedCallersOnly(EntryPoint = "spc_set_master_volume")]
	public static void SetMasterVolume(nint engineId, float volume) {
		if (GetEngine(engineId) is { } engine) {
			engine.MasterVolume = volume;
		}
	}

	[UnmanagedCallersOnly(EntryPoint = "spc_get_master_volume")]
	public static float GetMasterVolume(nint engineId) {
		return GetEngine(engineId)?.MasterVolume ?? 1.0f;
	}

	[UnmanagedCallersOnly(EntryPoint = "spc_set_loop_enabled")]
	public static void SetLoopEnabled(nint engineId, int enabled) {
		if (GetEngine(engineId) is { } engine) {
			engine.LoopEnabled = enabled != 0;
		}
	}

	[UnmanagedCallersOnly(EntryPoint = "spc_get_loop_enabled")]
	public static int GetLoopEnabled(nint engineId) {
		return GetEngine(engineId)?.LoopEnabled == true ? 1 : 0;
	}

	#endregion

	#region Voice Control

	[UnmanagedCallersOnly(EntryPoint = "spc_set_voice_muted")]
	public static void SetVoiceMuted(nint engineId, int voice, int muted) {
		GetEngine(engineId)?.SetVoiceMuted(voice, muted != 0);
	}

	[UnmanagedCallersOnly(EntryPoint = "spc_get_voice_muted")]
	public static int GetVoiceMuted(nint engineId, int voice) {
		return GetEngine(engineId)?.GetVoiceMuted(voice) == true ? 1 : 0;
	}

	[UnmanagedCallersOnly(EntryPoint = "spc_set_voice_solo")]
	public static void SetVoiceSolo(nint engineId, int voice, int solo) {
		GetEngine(engineId)?.SetVoiceSolo(voice, solo != 0);
	}

	[UnmanagedCallersOnly(EntryPoint = "spc_get_voice_solo")]
	public static int GetVoiceSolo(nint engineId, int voice) {
		return GetEngine(engineId)?.GetVoiceSolo(voice) == true ? 1 : 0;
	}

	[UnmanagedCallersOnly(EntryPoint = "spc_set_voice_volume")]
	public static void SetVoiceVolume(nint engineId, int voice, float volume) {
		GetEngine(engineId)?.SetVoiceVolume(voice, volume);
	}

	[UnmanagedCallersOnly(EntryPoint = "spc_get_voice_volume")]
	public static float GetVoiceVolume(nint engineId, int voice) {
		return GetEngine(engineId)?.GetVoiceVolume(voice) ?? 1.0f;
	}

	[UnmanagedCallersOnly(EntryPoint = "spc_mute_all")]
	public static void MuteAll(nint engineId) {
		GetEngine(engineId)?.MuteAll();
	}

	[UnmanagedCallersOnly(EntryPoint = "spc_unmute_all")]
	public static void UnmuteAll(nint engineId) {
		GetEngine(engineId)?.UnmuteAll();
	}

	[UnmanagedCallersOnly(EntryPoint = "spc_clear_solo")]
	public static void ClearSolo(nint engineId) {
		GetEngine(engineId)?.ClearSolo();
	}

	#endregion

	#region DAW Sync

	[UnmanagedCallersOnly(EntryPoint = "spc_set_host_tempo")]
	public static void SetHostTempo(nint engineId, double bpm) {
		GetEngine(engineId)?.SetHostTempo(bpm);
	}

	[UnmanagedCallersOnly(EntryPoint = "spc_set_time_signature")]
	public static void SetTimeSignature(nint engineId, double numerator, double denominator) {
		GetEngine(engineId)?.SetTimeSignature(numerator, denominator);
	}

	[UnmanagedCallersOnly(EntryPoint = "spc_get_position_beats")]
	public static double GetPositionBeats(nint engineId) {
		return GetEngine(engineId)?.PositionBeats ?? 0;
	}

	[UnmanagedCallersOnly(EntryPoint = "spc_get_position_bars")]
	public static double GetPositionBars(nint engineId) {
		return GetEngine(engineId)?.PositionBars ?? 0;
	}

	#endregion

	#region Info

	[UnmanagedCallersOnly(EntryPoint = "spc_get_total_cycles")]
	public static long GetTotalCycles(nint engineId) {
		return GetEngine(engineId)?.TotalCycles ?? 0;
	}

	[UnmanagedCallersOnly(EntryPoint = "spc_get_sample_rate")]
	public static int GetSampleRate(nint engineId) {
		return GetEngine(engineId)?.SampleRate ?? 44100;
	}

	[UnmanagedCallersOnly(EntryPoint = "spc_set_sample_rate")]
	public static void SetSampleRate(nint engineId, int sampleRate) {
		if (GetEngine(engineId) is { } engine) {
			engine.SampleRate = sampleRate;
		}
	}

	#endregion

	#region MIDI

	/// <summary>
	/// Process a MIDI note on event.
	/// </summary>
	[UnmanagedCallersOnly(EntryPoint = "spc_midi_note_on")]
	public static void MidiNoteOn(nint engineId, int channel, int note, int velocity) {
		if (_midiProcessors.TryGetValue(engineId, out var midi)) {
			midi.ProcessEvent(MidiEvent.NoteOn(channel, note, velocity));
		}
	}

	/// <summary>
	/// Process a MIDI note off event.
	/// </summary>
	[UnmanagedCallersOnly(EntryPoint = "spc_midi_note_off")]
	public static void MidiNoteOff(nint engineId, int channel, int note, int velocity) {
		if (_midiProcessors.TryGetValue(engineId, out var midi)) {
			midi.ProcessEvent(MidiEvent.NoteOff(channel, note, velocity));
		}
	}

	/// <summary>
	/// Process a MIDI control change event.
	/// </summary>
	[UnmanagedCallersOnly(EntryPoint = "spc_midi_cc")]
	public static void MidiControlChange(nint engineId, int channel, int controller, int value) {
		if (_midiProcessors.TryGetValue(engineId, out var midi)) {
			midi.ProcessEvent(MidiEvent.ControlChange(channel, controller, value));
		}
	}

	/// <summary>
	/// Process a MIDI pitch bend event.
	/// </summary>
	[UnmanagedCallersOnly(EntryPoint = "spc_midi_pitch_bend")]
	public static void MidiPitchBend(nint engineId, int channel, int value) {
		if (_midiProcessors.TryGetValue(engineId, out var midi)) {
			midi.ProcessEvent(MidiEvent.PitchBend(channel, value));
		}
	}

	/// <summary>
	/// Set the pitch bend range in semitones.
	/// </summary>
	[UnmanagedCallersOnly(EntryPoint = "spc_midi_set_pitch_bend_range")]
	public static void MidiSetPitchBendRange(nint engineId, int semitones) {
		if (_midiProcessors.TryGetValue(engineId, out var midi)) {
			midi.PitchBendRange = semitones;
		}
	}

	/// <summary>
	/// Reset all MIDI state.
	/// </summary>
	[UnmanagedCallersOnly(EntryPoint = "spc_midi_reset")]
	public static void MidiReset(nint engineId) {
		if (_midiProcessors.TryGetValue(engineId, out var midi)) {
			midi.Reset();
		}
	}

	#endregion

	#region Helpers

	private static SpcEngine? GetEngine(nint engineId) {
		if (_engines.TryGetValue(engineId, out var handle)) {
			return handle.Target as SpcEngine;
		}
		return null;
	}

	#endregion
}
