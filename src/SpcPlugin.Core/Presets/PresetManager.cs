using System.Text.Json;
using SpcPlugin.Core.Audio;
using SpcPlugin.Core.Midi;

namespace SpcPlugin.Core.Presets;

/// <summary>
/// Manages loading, saving, and applying presets for the SPC plugin.
/// </summary>
public class PresetManager {
	private readonly SpcEngine _engine;
	private readonly MidiProcessor? _midiProcessor;

	private static readonly JsonSerializerOptions JsonOptions = new() {
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
	};

	public PresetManager(SpcEngine engine, MidiProcessor? midiProcessor = null) {
		_engine = engine;
		_midiProcessor = midiProcessor;
	}

	/// <summary>
	/// Gets the current state as a preset.
	/// </summary>
	public SpcPreset CaptureCurrentState(string name = "Untitled") {
		var preset = new SpcPreset {
			Name = name,
			CreatedDate = DateTime.UtcNow,
			MasterVolume = _engine.MasterVolume,
			LoopEnabled = _engine.LoopEnabled
		};

		// Capture voice states
		for (int i = 0; i < 8; i++) {
			preset.Voices[i] = new VoicePreset {
				VoiceIndex = i,
				Muted = _engine.GetVoiceMuted(i),
				Solo = _engine.GetVoiceSolo(i),
				Volume = _engine.GetVoiceVolume(i)
			};
		}

		// Capture MIDI settings
		if (_midiProcessor != null) {
			preset.Midi.PitchBendRange = _midiProcessor.PitchBendRange;
		}

		return preset;
	}

	/// <summary>
	/// Captures current state with embedded SPC data for portable presets.
	/// </summary>
	public SpcPreset CaptureWithEmbeddedSpc(string name = "Untitled") {
		var preset = CaptureCurrentState(name);

		// Embed the SPC data
		if (_engine.Editor != null) {
			byte[] spcData = _engine.Editor.ExportSpc();
			preset.EmbeddedSpcData = Convert.ToBase64String(spcData);
		}

		return preset;
	}

	/// <summary>
	/// Applies a preset to the engine.
	/// </summary>
	public void ApplyPreset(SpcPreset preset) {
		// Load SPC if embedded
		if (!string.IsNullOrEmpty(preset.EmbeddedSpcData)) {
			byte[] spcData = Convert.FromBase64String(preset.EmbeddedSpcData);
			_engine.LoadSpc(spcData);
		} else if (!string.IsNullOrEmpty(preset.SpcFilePath) && File.Exists(preset.SpcFilePath)) {
			_engine.LoadSpcFile(preset.SpcFilePath);
		}

		// Apply master settings
		_engine.MasterVolume = preset.MasterVolume;
		_engine.LoopEnabled = preset.LoopEnabled;

		// Apply voice settings
		foreach (var voice in preset.Voices) {
			_engine.SetVoiceMuted(voice.VoiceIndex, voice.Muted);
			_engine.SetVoiceSolo(voice.VoiceIndex, voice.Solo);
			_engine.SetVoiceVolume(voice.VoiceIndex, voice.Volume);

			// Apply overrides via editor
			if (_engine.Editor != null) {
				if (voice.SourceOverride >= 0) {
					_engine.Editor.SetVoiceSource(voice.VoiceIndex, (byte)voice.SourceOverride);
				}
				if (voice.AdsrOverride != null) {
					_engine.Editor.SetVoiceAdsr(
						voice.VoiceIndex,
						voice.AdsrOverride.Attack,
						voice.AdsrOverride.Decay,
						voice.AdsrOverride.Sustain,
						voice.AdsrOverride.Release
					);
				}
			}
		}

		// Apply DSP settings
		if (_engine.Editor != null) {
			_engine.Editor.SetEchoEnabled(preset.Dsp.EchoVoiceMask);
			_engine.Editor.SetEchoFeedback(preset.Dsp.EchoFeedback);
			_engine.Editor.SetEchoDelay(preset.Dsp.EchoDelay);
			_engine.Editor.SetEchoVolume(preset.Dsp.EchoVolumeLeft, preset.Dsp.EchoVolumeRight);
			_engine.Editor.SetNoiseEnabled(preset.Dsp.NoiseVoiceMask);
			_engine.Editor.SetPitchModEnabled(preset.Dsp.PitchModVoiceMask);

			if (preset.Dsp.FirCoefficients is { Length: 8 }) {
				var coeffs = preset.Dsp.FirCoefficients.Select(c => (sbyte)c).ToArray();
				_engine.Editor.SetFirCoefficients(coeffs);
			}
		}

		// Apply MIDI settings
		if (_midiProcessor != null) {
			_midiProcessor.PitchBendRange = preset.Midi.PitchBendRange;
		}
	}

	/// <summary>
	/// Saves a preset to a JSON file.
	/// </summary>
	public static void SaveToFile(SpcPreset preset, string filePath) {
		string json = JsonSerializer.Serialize(preset, JsonOptions);
		File.WriteAllText(filePath, json);
	}

	/// <summary>
	/// Loads a preset from a JSON file.
	/// </summary>
	public static SpcPreset LoadFromFile(string filePath) {
		string json = File.ReadAllText(filePath);
		return JsonSerializer.Deserialize<SpcPreset>(json, JsonOptions)
			?? throw new InvalidDataException("Failed to deserialize preset");
	}

	/// <summary>
	/// Serializes a preset to JSON string.
	/// </summary>
	public static string SerializePreset(SpcPreset preset) {
		return JsonSerializer.Serialize(preset, JsonOptions);
	}

	/// <summary>
	/// Deserializes a preset from JSON string.
	/// </summary>
	public static SpcPreset DeserializePreset(string json) {
		return JsonSerializer.Deserialize<SpcPreset>(json, JsonOptions)
			?? throw new InvalidDataException("Failed to deserialize preset");
	}

	/// <summary>
	/// Creates a preset bank from multiple presets.
	/// </summary>
	public static void SavePresetBank(IEnumerable<SpcPreset> presets, string filePath) {
		var bank = new PresetBank {
			Presets = presets.ToList()
		};
		string json = JsonSerializer.Serialize(bank, JsonOptions);
		File.WriteAllText(filePath, json);
	}

	/// <summary>
	/// Loads a preset bank from file.
	/// </summary>
	public static List<SpcPreset> LoadPresetBank(string filePath) {
		string json = File.ReadAllText(filePath);
		var bank = JsonSerializer.Deserialize<PresetBank>(json, JsonOptions);
		return bank?.Presets ?? [];
	}

	/// <summary>
	/// Gets a list of built-in factory presets.
	/// </summary>
	public static List<SpcPreset> GetFactoryPresets() {
		return [
			CreateFactoryPreset("Default", "Clean default settings"),
			CreateFactoryPreset("Solo Lead", "Solo voice 0", v => v.VoiceIndex == 0 ? v with { Solo = true } : v),
			CreateFactoryPreset("Bass Only", "Solo voice 1", v => v.VoiceIndex == 1 ? v with { Solo = true } : v),
			CreateFactoryPreset("No Drums", "Mute percussive voices", v => v.VoiceIndex >= 6 ? v with { Muted = true } : v),
			CreateFactoryPreset("Heavy Echo", "Maximum echo effect", dsp: d => d with {
				EchoVoiceMask = 0xFF,
				EchoFeedback = 100,
				EchoDelay = 8,
				EchoVolumeLeft = 64,
				EchoVolumeRight = 64
			}),
			CreateFactoryPreset("No Echo", "Echo disabled", dsp: d => d with { EchoVoiceMask = 0 }),
			CreateFactoryPreset("Low Volume", "50% master volume", master: 0.5f),
		];
	}

	private static SpcPreset CreateFactoryPreset(
		string name,
		string description,
		Func<VoicePreset, VoicePreset>? voiceTransform = null,
		Func<DspPreset, DspPreset>? dsp = null,
		float master = 1.0f
	) {
		var preset = new SpcPreset {
			Name = name,
			Description = description,
			Author = "Factory",
			MasterVolume = master
		};

		if (voiceTransform != null) {
			for (int i = 0; i < 8; i++) {
				preset.Voices[i] = voiceTransform(preset.Voices[i]);
			}
		}

		if (dsp != null) {
			preset.Dsp = dsp(preset.Dsp);
		}

		return preset;
	}
}

/// <summary>
/// Container for multiple presets (preset bank).
/// </summary>
internal class PresetBank {
	public int Version { get; set; } = 1;
	public List<SpcPreset> Presets { get; set; } = [];
}
