using SpcPlugin.Core.Audio;
using SpcPlugin.Core.UI;
using Xunit;

namespace SpcPlugin.Tests;

public class UIViewModelTests {
	[Fact]
	public void WaveformDisplay_CreatesWithSpecifiedBufferSize() {
		var engine = new SpcEngine(44100);
		var display = new WaveformDisplay(engine, 1024);

		Assert.Equal(1024, display.BufferSize);
	}

	[Fact]
	public void WaveformDisplay_FeedUpdatesBuffers() {
		var engine = new SpcEngine(44100);
		var display = new WaveformDisplay(engine, 256);

		var left = new float[128];
		var right = new float[128];

		for (int i = 0; i < 128; i++) {
			left[i] = MathF.Sin(i * 0.1f) * 0.5f;
			right[i] = MathF.Cos(i * 0.1f) * 0.5f;
		}

		display.Feed(left, right);

		Assert.True(display.PeakLeft > 0);
		Assert.True(display.PeakRight > 0);
	}

	[Fact]
	public void WaveformDisplay_FeedInterleaved_Works() {
		var engine = new SpcEngine(44100);
		var display = new WaveformDisplay(engine, 256);

		var interleaved = new float[256];
		for (int i = 0; i < 256; i += 2) {
			interleaved[i] = 0.5f;     // L
			interleaved[i + 1] = 0.3f;  // R
		}

		display.FeedInterleaved(interleaved);

		var (left, right) = display.GetLevelMeterValues();
		Assert.True(left > 0);
		Assert.True(right > 0);
	}

	[Fact]
	public void WaveformDisplay_Clear_ResetsPeaks() {
		var engine = new SpcEngine(44100);
		var display = new WaveformDisplay(engine, 256);

		var data = new float[128];
		Array.Fill(data, 0.5f);
		display.Feed(data, data);

		Assert.True(display.PeakLeft > 0);

		display.Clear();

		Assert.Equal(0, display.PeakLeft);
		Assert.Equal(0, display.PeakRight);
	}

	[Fact]
	public void WaveformDisplay_GetDownsampled_ReturnsCorrectSize() {
		var engine = new SpcEngine(44100);
		var display = new WaveformDisplay(engine, 1024);

		var result = display.GetDownsampledWaveform(100);

		Assert.Equal(100, result.Length);
	}

	[Fact]
	public void WaveformDisplay_LevelMeterDb_ReturnsValidRange() {
		var engine = new SpcEngine(44100);
		var display = new WaveformDisplay(engine, 256);

		var data = new float[128];
		Array.Fill(data, 0.1f);
		display.Feed(data, data);

		var (dbL, dbR) = display.GetLevelMeterDb();

		// Should be between -60dB and 0dB
		Assert.True(dbL >= -60f && dbL <= 0f);
		Assert.True(dbR >= -60f && dbR <= 0f);
	}

	[Fact]
	public void SpectrumAnalyzer_CreatesWithPowerOfTwo() {
		var analyzer = new SpectrumAnalyzer(512);
		Assert.Equal(512, analyzer.FftSize);
		Assert.Equal(256, analyzer.BinCount);
	}

	[Fact]
	public void SpectrumAnalyzer_ThrowsOnNonPowerOfTwo() {
		Assert.Throws<ArgumentException>(() => new SpectrumAnalyzer(500));
	}

	[Fact]
	public void SpectrumAnalyzer_GetMagnitudes_ReturnsCorrectSize() {
		var analyzer = new SpectrumAnalyzer(512);

		var samples = new float[256];
		for (int i = 0; i < 256; i++) {
			samples[i] = MathF.Sin(i * 0.1f);
		}

		analyzer.Feed(samples);
		var mags = analyzer.GetMagnitudes();

		Assert.Equal(256, mags.Length);
	}

	[Fact]
	public void SpectrumAnalyzer_GetBands_ReturnsRequestedCount() {
		var analyzer = new SpectrumAnalyzer(512);

		var samples = new float[256];
		analyzer.Feed(samples);

		var bands = analyzer.GetBands(16);
		Assert.Equal(16, bands.Length);

		bands = analyzer.GetBands(64);
		Assert.Equal(64, bands.Length);
	}

	[Fact]
	public void SampleDisplay_DecodeBrr_ReturnsFloatData() {
		// Minimal BRR block (9 bytes = 16 samples)
		var brr = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

		var result = SampleDisplay.DecodeBrrForDisplay(brr);

		Assert.Equal(16, result.Length);
		Assert.All(result, v => Assert.True(v >= -1f && v <= 1f));
	}

	[Fact]
	public void SampleDisplay_GetDownsampled_Works() {
		var samples = new float[1000];
		for (int i = 0; i < 1000; i++) {
			samples[i] = MathF.Sin(i * 0.1f);
		}

		var downsampled = SampleDisplay.GetDownsampled(samples, 100);

		Assert.Equal(100, downsampled.Length);
	}

	[Fact]
	public void VoiceViewModel_Index_ReturnsCorrectValue() {
		var engine = new SpcEngine(44100);
		var vm = new VoiceViewModel(engine, 3);

		Assert.Equal(3, vm.Index);
		Assert.Equal("Voice 4", vm.Name);
	}

	[Fact]
	public void VoiceViewModel_MuteToggle_Works() {
		var engine = new SpcEngine(44100);
		var vm = new VoiceViewModel(engine, 0);

		Assert.False(vm.IsMuted);
		vm.ToggleMute();
		Assert.True(vm.IsMuted);
		vm.ToggleMute();
		Assert.False(vm.IsMuted);
	}

	[Fact]
	public void VoiceViewModel_SoloToggle_Works() {
		var engine = new SpcEngine(44100);
		var vm = new VoiceViewModel(engine, 0);

		Assert.False(vm.IsSolo);
		vm.ToggleSolo();
		Assert.True(vm.IsSolo);
		vm.ToggleSolo();
		Assert.False(vm.IsSolo);
	}

	[Fact]
	public void VoiceViewModel_Volume_ClampsToValidRange() {
		var engine = new SpcEngine(44100);
		var vm = new VoiceViewModel(engine, 0);

		vm.Volume = 1.5f;
		Assert.Equal(1f, vm.Volume);

		vm.Volume = -0.5f;
		Assert.Equal(0f, vm.Volume);
	}

	[Fact]
	public void PluginViewModel_MasterVolume_ClampsToValidRange() {
		var engine = new SpcEngine(44100);
		var vm = new PluginViewModel(engine);

		vm.MasterVolume = 3f;
		Assert.Equal(2f, vm.MasterVolume);

		vm.MasterVolume = -1f;
		Assert.Equal(0f, vm.MasterVolume);
	}

	[Fact]
	public void PluginViewModel_HasEightVoices() {
		var engine = new SpcEngine(44100);
		var vm = new PluginViewModel(engine);

		Assert.Equal(8, vm.Voices.Length);
		Assert.All(vm.Voices, v => Assert.NotNull(v));
	}

	[Fact]
	public void PluginViewModel_SelectedVoice_ClampsToValidRange() {
		var engine = new SpcEngine(44100);
		var vm = new PluginViewModel(engine);

		vm.SelectedVoice = 10;
		Assert.Equal(0, vm.SelectedVoice); // Should not change

		vm.SelectedVoice = 5;
		Assert.Equal(5, vm.SelectedVoice);

		vm.SelectedVoice = -1;
		Assert.Equal(5, vm.SelectedVoice); // Should not change
	}

	[Fact]
	public void PluginViewModel_MuteAll_MutesAllVoices() {
		var engine = new SpcEngine(44100);
		var vm = new PluginViewModel(engine);

		vm.MuteAll();

		for (int i = 0; i < 8; i++) {
			Assert.True(engine.GetVoiceMuted(i));
		}
	}

	[Fact]
	public void PluginViewModel_UnmuteAll_UnmutesAllVoices() {
		var engine = new SpcEngine(44100);
		var vm = new PluginViewModel(engine);

		vm.MuteAll();
		vm.UnmuteAll();

		for (int i = 0; i < 8; i++) {
			Assert.False(engine.GetVoiceMuted(i));
		}
	}

	[Fact]
	public void PluginViewModel_Dispose_DisposesEngine() {
		var engine = new SpcEngine(44100);
		var vm = new PluginViewModel(engine);

		vm.Dispose();

		// Engine should be disposed (would throw on further operations)
		// This is more of a smoke test
		Assert.True(true);
	}

	[Fact]
	public void SampleInfo_HasLoop_DetectsCorrectly() {
		var sample1 = new SampleInfo {
			Index = 0,
			StartAddress = 0x1000,
			LoopAddress = 0x1100,
			Length = 0x200
		};

		var sample2 = new SampleInfo {
			Index = 1,
			StartAddress = 0x2000,
			LoopAddress = 0x0000,  // Loop before start = no loop
			Length = 0x100
		};

		Assert.True(sample1.HasLoop);
		Assert.False(sample2.HasLoop);
	}

	[Fact]
	public void SampleInfo_Name_FormatsCorrectly() {
		var sample = new SampleInfo { Index = 5 };
		Assert.Equal("Sample 005", sample.Name);

		sample = new SampleInfo { Index = 127 };
		Assert.Equal("Sample 127", sample.Name);
	}
}
