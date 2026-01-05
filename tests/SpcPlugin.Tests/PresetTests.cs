using SpcPlugin.Core.Presets;

namespace SpcPlugin.Tests;

public class PresetTests {
	[Fact]
	public void SpcPreset_DefaultValues_AreCorrect() {
		var preset = new SpcPreset();

		Assert.Equal("Untitled", preset.Name);
		Assert.Equal(1.0f, preset.MasterVolume);
		Assert.True(preset.LoopEnabled);
		Assert.NotNull(preset.Voices);
		Assert.Equal(8, preset.Voices.Length);
		Assert.NotNull(preset.Dsp);
		Assert.NotNull(preset.Midi);
	}

	[Fact]
	public void VoicePreset_DefaultValues_AreCorrect() {
		var preset = new VoicePreset { VoiceIndex = 3 };

		Assert.Equal(3, preset.VoiceIndex);
		Assert.False(preset.Muted);
		Assert.False(preset.Solo);
		Assert.Equal(1.0f, preset.Volume);
		Assert.Equal(-1, preset.SourceOverride);
		Assert.Null(preset.AdsrOverride);
	}

	[Fact]
	public void DspPreset_DefaultValues_AreZero() {
		var preset = new DspPreset();

		Assert.Equal(0, preset.EchoVoiceMask);
		Assert.Equal(0, preset.EchoFeedback);
		Assert.Equal(0, preset.EchoDelay);
		Assert.Equal(0, preset.NoiseVoiceMask);
		Assert.Equal(0, preset.PitchModVoiceMask);
	}

	[Fact]
	public void MidiPreset_DefaultValues_AreCorrect() {
		var preset = new MidiPreset();

		Assert.Equal(2, preset.PitchBendRange);
		Assert.Equal(60, preset.VoiceBaseNote);
		Assert.Equal(-1, preset.ChannelFilter);
		Assert.Equal(VelocityCurve.Linear, preset.VelocityCurve);
	}

	[Fact]
	public void PresetManager_SerializeAndDeserialize_RoundTrips() {
		var original = new SpcPreset {
			Name = "Test Preset",
			Author = "Test Author",
			MasterVolume = 0.75f,
			LoopEnabled = false
		};
		original.Voices[0] = new VoicePreset {
			VoiceIndex = 0,
			Muted = true,
			Volume = 0.5f
		};
		original.Dsp = new DspPreset {
			EchoVoiceMask = 0b11110000,
			EchoFeedback = 64
		};

		string json = PresetManager.SerializePreset(original);
		var deserialized = PresetManager.DeserializePreset(json);

		Assert.Equal(original.Name, deserialized.Name);
		Assert.Equal(original.Author, deserialized.Author);
		Assert.Equal(original.MasterVolume, deserialized.MasterVolume);
		Assert.Equal(original.LoopEnabled, deserialized.LoopEnabled);
		Assert.True(deserialized.Voices[0].Muted);
		Assert.Equal(0.5f, deserialized.Voices[0].Volume);
		Assert.Equal(0b11110000, deserialized.Dsp.EchoVoiceMask);
		Assert.Equal(64, deserialized.Dsp.EchoFeedback);
	}

	[Fact]
	public void PresetManager_GetFactoryPresets_ReturnsMultiple() {
		var presets = PresetManager.GetFactoryPresets();

		Assert.NotEmpty(presets);
		Assert.Contains(presets, p => p.Name == "Default");
		Assert.Contains(presets, p => p.Name == "Solo Lead");
		Assert.Contains(presets, p => p.Name == "No Echo");
	}

	[Fact]
	public void AdsrPreset_StoresValues() {
		var adsr = new AdsrPreset {
			Attack = 15,
			Decay = 7,
			Sustain = 7,
			Release = 31
		};

		Assert.Equal(15, adsr.Attack);
		Assert.Equal(7, adsr.Decay);
		Assert.Equal(7, adsr.Sustain);
		Assert.Equal(31, adsr.Release);
	}

	[Fact]
	public void SpcPreset_WithEmbeddedData_CanRoundTrip() {
		var original = new SpcPreset {
			Name = "Embedded Test",
			EmbeddedSpcData = Convert.ToBase64String(new byte[100])
		};

		string json = PresetManager.SerializePreset(original);
		var deserialized = PresetManager.DeserializePreset(json);

		Assert.Equal(original.EmbeddedSpcData, deserialized.EmbeddedSpcData);
	}

	[Theory]
	[InlineData(VelocityCurve.Linear)]
	[InlineData(VelocityCurve.Soft)]
	[InlineData(VelocityCurve.Hard)]
	[InlineData(VelocityCurve.SCurve)]
	public void VelocityCurve_Serializes_AsString(VelocityCurve curve) {
		var preset = new MidiPreset { VelocityCurve = curve };
		var parent = new SpcPreset { Midi = preset };

		string json = PresetManager.SerializePreset(parent);
		Assert.Contains(curve.ToString(), json);

		var deserialized = PresetManager.DeserializePreset(json);
		Assert.Equal(curve, deserialized.Midi.VelocityCurve);
	}
}
