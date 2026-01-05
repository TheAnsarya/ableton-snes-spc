namespace SpcPlugin.Core.Midi;

/// <summary>
/// MIDI event types supported by the SPC engine.
/// </summary>
public enum MidiEventType {
	NoteOff = 0x80,
	NoteOn = 0x90,
	ControlChange = 0xB0,
	ProgramChange = 0xC0,
	PitchBend = 0xE0
}

/// <summary>
/// Represents a MIDI event for processing by the SPC engine.
/// </summary>
public readonly record struct MidiEvent(
	MidiEventType Type,
	int Channel,
	int Data1,
	int Data2,
	int SampleOffset = 0
) {
	/// <summary>Note number for note events.</summary>
	public int Note => Data1;

	/// <summary>Velocity for note events (0-127).</summary>
	public int Velocity => Data2;

	/// <summary>Controller number for CC events.</summary>
	public int Controller => Data1;

	/// <summary>Controller value for CC events (0-127).</summary>
	public int Value => Data2;

	/// <summary>Program number for program change events.</summary>
	public int Program => Data1;

	/// <summary>Pitch bend value (-8192 to +8191).</summary>
	public int PitchBendValue => ((Data2 << 7) | Data1) - 8192;

	public static MidiEvent NoteOn(int channel, int note, int velocity, int offset = 0)
		=> new(MidiEventType.NoteOn, channel, note, velocity, offset);

	public static MidiEvent NoteOff(int channel, int note, int velocity = 0, int offset = 0)
		=> new(MidiEventType.NoteOff, channel, note, velocity, offset);

	public static MidiEvent ControlChange(int channel, int controller, int value, int offset = 0)
		=> new(MidiEventType.ControlChange, channel, controller, value, offset);

	public static MidiEvent ProgramChange(int channel, int program, int offset = 0)
		=> new(MidiEventType.ProgramChange, channel, program, 0, offset);

	public static MidiEvent PitchBend(int channel, int value, int offset = 0) {
		int centered = value + 8192;
		return new(MidiEventType.PitchBend, channel, centered & 0x7F, (centered >> 7) & 0x7F, offset);
	}
}

/// <summary>
/// MIDI CC (Continuous Controller) numbers used by the plugin.
/// </summary>
public static class MidiControllers {
	// Standard controllers
	public const int ModWheel = 1;
	public const int Volume = 7;
	public const int Pan = 10;
	public const int Expression = 11;
	public const int Sustain = 64;

	// Custom controllers for SPC control
	public const int VoiceMute = 102;      // Voice mute toggle (value: voice 0-7)
	public const int VoiceSolo = 103;      // Voice solo toggle (value: voice 0-7)
	public const int MasterVolume = 104;   // Master volume (0-127 -> 0-200%)
	public const int EchoFeedback = 105;   // Echo feedback (0-127)
	public const int EchoDelay = 106;      // Echo delay (0-15 × 16ms)
	public const int NoiseFreq = 107;      // Noise frequency (0-31)
	public const int LoopEnable = 108;     // Loop toggle (0=off, 127=on)
	public const int PlayStop = 109;       // Play/stop toggle
	public const int Reset = 110;          // Reset to beginning
}

/// <summary>
/// MIDI note mappings for SPC voices.
/// </summary>
public static class MidiNoteMap {
	/// <summary>
	/// Base note for voice triggers (C3 = 60).
	/// Notes 60-67 map to voices 0-7.
	/// </summary>
	public const int VoiceBaseNote = 60;

	/// <summary>
	/// Maps a MIDI note to a voice number, or -1 if not a voice trigger.
	/// </summary>
	public static int NoteToVoice(int note) {
		int voice = note - VoiceBaseNote;
		return voice is >= 0 and < 8 ? voice : -1;
	}

	/// <summary>
	/// Maps a voice number to its MIDI note.
	/// </summary>
	public static int VoiceToNote(int voice) {
		return VoiceBaseNote + voice;
	}
}
