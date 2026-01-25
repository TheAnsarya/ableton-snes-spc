using SpcPlugin.Core.Audio;

namespace SpcPlugin.Tests.Audio;

/// <summary>
/// Tests for sample import functionality.
/// </summary>
public class SampleImporterTests {
	[Fact]
	public void ImportSamples_BasicConversion_Succeeds() {
		// Arrange
		var samples = new short[320]; // 20 blocks worth
		for (int i = 0; i < samples.Length; i++) {
			samples[i] = (short)(Math.Sin(i * 0.1) * 16000);
		}

		// Act
		var result = SampleImporter.ImportSamples(samples, 32000);

		// Assert
		Assert.True(result.Success);
		Assert.NotEmpty(result.BrrData);
		Assert.Equal(0, result.BrrData.Length % 9); // Must be multiple of 9
		Assert.False(result.HasLoop);
		Assert.Equal(320, result.OriginalSamples);
	}

	[Fact]
	public void ImportSamples_WithLoop_SetsLoopPoint() {
		// Arrange
		var samples = new short[320];
		for (int i = 0; i < samples.Length; i++) {
			samples[i] = (short)(Math.Sin(i * 0.1) * 16000);
		}
		var options = new SampleImporter.ImportOptions {
			LoopStart = 160, // Loop at sample 160
		};

		// Act
		var result = SampleImporter.ImportSamples(samples, 32000, options);

		// Assert
		Assert.True(result.Success);
		Assert.True(result.HasLoop);
		Assert.True(result.LoopBlock > 0);
	}

	[Fact]
	public void ImportSamples_Resampling_ChangesRate() {
		// Arrange
		var samples = new short[44100]; // 1 second at 44.1kHz

		// Act
		var result = SampleImporter.ImportSamples(samples, 44100);

		// Assert
		Assert.True(result.Success);
		Assert.Equal(44100, result.OriginalSampleRate);
		Assert.Equal(32000, result.FinalSampleRate); // Should resample to SNES rate
	}

	[Fact]
	public void ImportWavData_ValidWav_Succeeds() {
		// Arrange
		var samples = new short[320];
		for (int i = 0; i < samples.Length; i++) {
			samples[i] = (short)(Math.Sin(i * 0.1) * 16000);
		}
		var wavData = WavFile.Create(samples, 32000, 1);

		// Act
		var result = SampleImporter.ImportWavData(wavData);

		// Assert
		Assert.True(result.Success);
		Assert.NotEmpty(result.BrrData);
	}

	[Fact]
	public void ImportWavData_InvalidData_ReturnsFailed() {
		// Arrange
		var invalidData = new byte[] { 1, 2, 3, 4, 5 };

		// Act
		var result = SampleImporter.ImportWavData(invalidData);

		// Assert
		Assert.False(result.Success);
		Assert.NotNull(result.Error);
	}

	[Fact]
	public void ExportBrrToWavData_RoundTrip_PreservesContent() {
		// Arrange
		var original = new short[160];
		for (int i = 0; i < original.Length; i++) {
			original[i] = (short)(Math.Sin(i * 0.1) * 16000);
		}
		var importResult = SampleImporter.ImportSamples(original, 32000, new() { Normalize = false });

		// Act
		var wavData = SampleImporter.ExportBrrToWavData(importResult.BrrData);
		var parsed = WavFile.Parse(wavData);

		// Assert
		Assert.Equal(importResult.FinalSamples, parsed.SampleCount);
	}

	[Fact]
	public void CalculateBlocksForDuration_CorrectCalculation() {
		// Arrange & Act
		int blocks1sec = SampleImporter.CalculateBlocksForDuration(1.0, 32000);
		int blocks500ms = SampleImporter.CalculateBlocksForDuration(0.5, 32000);

		// Assert
		Assert.Equal(2000, blocks1sec);  // 32000 samples / 16 samples per block
		Assert.Equal(1000, blocks500ms); // 16000 samples / 16 samples per block
	}

	[Fact]
	public void CalculateDurationForBlocks_CorrectCalculation() {
		// Arrange & Act
		double duration = SampleImporter.CalculateDurationForBlocks(2000, 32000);

		// Assert
		Assert.Equal(1.0, duration, 3); // 2000 blocks * 16 samples / 32000 rate = 1 second
	}

	[Fact]
	public void ImportSamples_MaxBlocks_TruncatesOutput() {
		// Arrange
		var samples = new short[16000]; // 1000 blocks worth
		var options = new SampleImporter.ImportOptions {
			MaxBlocks = 100,
			Normalize = false,
			ApplyFadeOut = false,
		};

		// Act
		var result = SampleImporter.ImportSamples(samples, 32000, options);

		// Assert
		Assert.True(result.Success);
		Assert.True(result.BlockCount <= 100);
	}
}
