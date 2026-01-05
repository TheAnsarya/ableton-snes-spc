using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpcPlugin.Core.Presets;

/// <summary>
/// Represents a saved preset configuration for the SPC plugin.
/// </summary>
public class SpcPreset {
	/// <summary>Preset name.</summary>
	public string Name { get; set; } = "Untitled";

	/// <summary>Preset author.</summary>
	public string Author { get; set; } = "";

	/// <summary>Description or notes.</summary>
	public string Description { get; set; } = "";

	/// <summary>Date created.</summary>
	public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

	/// <summary>Preset version for compatibility.</summary>
	public int Version { get; set; } = 1;

	/// <summary>Path to the SPC file (relative or absolute).</summary>
	public string? SpcFilePath { get; set; }

	/// <summary>Embedded SPC data (Base64 encoded) for portable presets.</summary>
	public string? EmbeddedSpcData { get; set; }

	/// <summary>Master volume (0.0 - 2.0).</summary>
	public float MasterVolume { get; set; } = 1.0f;

	/// <summary>Loop enabled.</summary>
	public bool LoopEnabled { get; set; } = true;

	/// <summary>Per-voice settings.</summary>
	public VoicePreset[] Voices { get; set; } = CreateDefaultVoices();

	/// <summary>DSP effect settings.</summary>
	public DspPreset Dsp { get; set; } = new();

	/// <summary>MIDI configuration.</summary>
	public MidiPreset Midi { get; set; } = new();

	private static VoicePreset[] CreateDefaultVoices() {
		var voices = new VoicePreset[8];
		for (int i = 0; i < 8; i++) {
			voices[i] = new VoicePreset { VoiceIndex = i };
		}
		return voices;
	}
}

/// <summary>
/// Per-voice preset settings.
/// </summary>
public record VoicePreset {
	/// <summary>Voice index (0-7).</summary>
	public int VoiceIndex { get; set; }

	/// <summary>Voice muted.</summary>
	public bool Muted { get; set; }

	/// <summary>Voice soloed.</summary>
	public bool Solo { get; set; }

	/// <summary>Voice volume (0.0 - 1.0).</summary>
	public float Volume { get; set; } = 1.0f;

	/// <summary>Custom name for this voice.</summary>
	public string? CustomName { get; set; }

	/// <summary>Override source number (-1 = use original).</summary>
	public int SourceOverride { get; set; } = -1;

	/// <summary>Override ADSR (null = use original).</summary>
	public AdsrPreset? AdsrOverride { get; set; }
}

/// <summary>
/// ADSR envelope settings.
/// </summary>
public class AdsrPreset {
	public int Attack { get; set; }
	public int Decay { get; set; }
	public int Sustain { get; set; }
	public int Release { get; set; }
}

/// <summary>
/// DSP effect preset settings.
/// </summary>
public record DspPreset {
	/// <summary>Echo enabled for voices (bitmask).</summary>
	public int EchoVoiceMask { get; set; } = 0;

	/// <summary>Echo feedback (-128 to 127).</summary>
	public int EchoFeedback { get; set; } = 0;

	/// <summary>Echo delay (0-15, in 16ms units).</summary>
	public int EchoDelay { get; set; } = 0;

	/// <summary>Echo volume left.</summary>
	public int EchoVolumeLeft { get; set; } = 0;

	/// <summary>Echo volume right.</summary>
	public int EchoVolumeRight { get; set; } = 0;

	/// <summary>FIR filter coefficients (8 values, -128 to 127).</summary>
	public int[]? FirCoefficients { get; set; }

	/// <summary>Noise enabled for voices (bitmask).</summary>
	public int NoiseVoiceMask { get; set; } = 0;

	/// <summary>Noise clock (0-31).</summary>
	public int NoiseClock { get; set; } = 0;

	/// <summary>Pitch modulation enabled (bitmask).</summary>
	public int PitchModVoiceMask { get; set; } = 0;
}

/// <summary>
/// MIDI configuration preset.
/// </summary>
public class MidiPreset {
	/// <summary>Pitch bend range in semitones.</summary>
	public int PitchBendRange { get; set; } = 2;

	/// <summary>Base note for voice triggers.</summary>
	public int VoiceBaseNote { get; set; } = 60;

	/// <summary>MIDI channel filter (-1 = all channels).</summary>
	public int ChannelFilter { get; set; } = -1;

	/// <summary>Velocity curve type.</summary>
	public VelocityCurve VelocityCurve { get; set; } = VelocityCurve.Linear;
}

/// <summary>
/// Velocity response curve types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VelocityCurve {
	Linear,
	Soft,
	Hard,
	SCurve
}
