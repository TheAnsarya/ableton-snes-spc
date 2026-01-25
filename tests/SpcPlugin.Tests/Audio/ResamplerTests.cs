using SpcPlugin.Core.Audio;

namespace SpcPlugin.Tests.Audio;

/// <summary>
/// Tests for audio resampling functionality.
/// </summary>
public class ResamplerTests {
	[Fact]
	public void Resample_SameRate_ReturnsCopy() {
		// Arrange
		var samples = new short[] { 100, 200, 300, 400, 500 };

		// Act
		var result = Resampler.Resample(samples, 32000, 32000);

		// Assert
		Assert.Equal(samples.Length, result.Length);
		Assert.Equal(samples, result);
		Assert.NotSame(samples, result); // Should be a copy
	}

	[Fact]
	public void Resample_Downsample_ReducesLength() {
		// Arrange
		var samples = new short[1000];
		for (int i = 0; i < samples.Length; i++) {
			samples[i] = (short)(Math.Sin(i * 0.1) * 10000);
		}

		// Act - Downsample from 44100 to 22050 (half)
		var result = Resampler.Resample(samples, 44100, 22050);

		// Assert
		Assert.Equal(500, result.Length); // Should be half
	}

	[Fact]
	public void Resample_Upsample_IncreasesLength() {
		// Arrange
		var samples = new short[1000];
		for (int i = 0; i < samples.Length; i++) {
			samples[i] = (short)(Math.Sin(i * 0.1) * 10000);
		}

		// Act - Upsample from 22050 to 44100 (double)
		var result = Resampler.Resample(samples, 22050, 44100);

		// Assert
		Assert.Equal(2000, result.Length); // Should be double
	}

	[Fact]
	public void ResampleToSnes_UsesCorrectRate() {
		// Arrange
		var samples = new short[44100]; // 1 second at 44.1kHz

		// Act
		var result = Resampler.ResampleToSnes(samples, 44100);

		// Assert
		Assert.Equal(32000, result.Length); // 1 second at 32kHz
	}

	[Fact]
	public void Normalize_ScalesToPeak() {
		// Arrange
		var samples = new short[] { 100, 200, 300, 400, 500 };

		// Act - Normalize to 0dB (full scale)
		var result = Resampler.Normalize(samples, 0);

		// Assert
		int maxAbs = result.Max(Math.Abs);
		Assert.True(maxAbs > 30000); // Should be close to full scale
	}

	[Fact]
	public void Normalize_ZeroInput_ReturnsZero() {
		// Arrange
		var samples = new short[] { 0, 0, 0, 0, 0 };

		// Act
		var result = Resampler.Normalize(samples);

		// Assert
		Assert.All(result, s => Assert.Equal(0, s));
	}

	[Fact]
	public void AlignToBrrBlocks_PadsToBlockBoundary() {
		// Arrange
		var samples = new short[30]; // Not aligned (16 samples per block)

		// Act
		var result = Resampler.AlignToBrrBlocks(samples);

		// Assert
		Assert.Equal(32, result.Length); // Should be 2 blocks (32 samples)
	}

	[Fact]
	public void AlignToBrrBlocks_RespectsMaxBlocks() {
		// Arrange
		var samples = new short[1000];

		// Act
		var result = Resampler.AlignToBrrBlocks(samples, maxBlocks: 10);

		// Assert
		Assert.Equal(160, result.Length); // 10 blocks * 16 samples
	}

	[Fact]
	public void ApplyFadeOut_FadesEndOfSample() {
		// Arrange
		var samples = new short[100];
		Array.Fill(samples, (short)10000);

		// Act
		var result = Resampler.ApplyFadeOut(samples, fadeSamples: 32);

		// Assert
		Assert.Equal(10000, result[0]); // Start unchanged
		Assert.Equal(10000, result[60]); // Before fade
		Assert.True(result[^1] < 1000); // End faded
	}

	[Fact]
	public void ExtractLoopRegion_SplitsCorrectly() {
		// Arrange
		var samples = new short[160]; // 10 blocks
		for (int i = 0; i < samples.Length; i++) {
			samples[i] = (short)(i * 100);
		}

		// Act - Loop starts at block 3 (sample 48)
		var (preLoop, loop) = Resampler.ExtractLoopRegion(samples, 48);

		// Assert
		Assert.Equal(48, preLoop.Length); // 3 blocks
		Assert.Equal(112, loop.Length);   // 7 blocks (remaining, aligned)
	}
}
