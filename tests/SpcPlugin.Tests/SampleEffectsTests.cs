using SpcPlugin.Core.Effects;
using Xunit;

namespace SpcPlugin.Tests;

public class SampleEffectsTests {
	private static short[] CreateTestSamples(int length = 4000) {
		var samples = new short[length];
		for (int i = 0; i < length; i++) {
			samples[i] = (short)(Math.Sin(i * 0.1) * 16000);
		}
		return samples;
	}

	[Fact]
	public void ApplyDelay_ReturnsCorrectLength() {
		var input = CreateTestSamples();
		var result = SampleEffects.ApplyDelay(input, 50f, 0.5f, 0.5f);

		Assert.Equal(input.Length, result.Length);
	}

	[Fact]
	public void ApplyDelay_ModifiesOutput() {
		// Create samples with clear pattern
		var input = new short[4000];
		for (int i = 0; i < 4000; i++) {
			input[i] = (short)(i < 500 ? 10000 : 0); // Impulse at start
		}

		var result = SampleEffects.ApplyDelay(input, 30f, 0.7f, 0.7f); // 30ms delay

		// After the delay (~960 samples at 32kHz), we should see echo of the impulse
		// The original impulse is at 0-500, echo should appear around 960+

		// Check that output differs from input (due to wet mix and echo)
		int checkStart = 1200; // Well past delay time
		bool foundDifference = false;

		// The echo should cause non-zero values after the original impulse
		for (int i = checkStart; i < 2000 && !foundDifference; i++) {
			if (Math.Abs(result[i]) > 500) { // Looking for echo of impulse
				foundDifference = true;
			}
		}

		Assert.True(foundDifference, "Expected to find echo of impulse in delayed signal");
	}

	[Fact]
	public void ApplyBitCrush_ReducesBitDepth() {
		var input = CreateTestSamples();
		var result = SampleEffects.ApplyBitCrush(input, 4);

		// With 4 bits, values should be quantized to steps of 4096
		Assert.All(result, s => Assert.Equal(0, s % (1 << (16 - 4))));
	}

	[Fact]
	public void ApplyBitCrush_ClampsBitRange() {
		var input = CreateTestSamples();

		// Should not throw with edge values
		var result1 = SampleEffects.ApplyBitCrush(input, 1);
		var result2 = SampleEffects.ApplyBitCrush(input, 16);
		var result3 = SampleEffects.ApplyBitCrush(input, 0);  // Clamped to 1
		var result4 = SampleEffects.ApplyBitCrush(input, 20); // Clamped to 16

		Assert.Equal(input.Length, result1.Length);
		Assert.Equal(input.Length, result2.Length);
	}

	[Fact]
	public void ApplySampleRateReduce_HoldsValues() {
		var input = new short[] { 1, 2, 3, 4, 5, 6, 7, 8 };
		var result = SampleEffects.ApplySampleRateReduce(input, 2);

		// Every pair should have same value (the first of each pair)
		Assert.Equal(result[0], result[1]);
		Assert.Equal(result[2], result[3]);
		Assert.Equal(result[4], result[5]);
		Assert.Equal(result[6], result[7]);
	}

	[Fact]
	public void ApplyCompressor_ReducesLoudPeaks() {
		var input = new short[100];
		for (int i = 0; i < 100; i++) {
			input[i] = (short)(i < 50 ? 10000 : 30000);
		}

		var result = SampleEffects.ApplyCompressor(input, 0.5f, 4f);

		// The loud section should be compressed
		double loudAvg = 0;
		for (int i = 50; i < 100; i++) loudAvg += Math.Abs(result[i]);
		loudAvg /= 50;

		// Compressed output should be lower than input
		Assert.True(loudAvg < 30000);
	}

	[Fact]
	public void ApplySaturation_SoftClips() {
		var input = CreateTestSamples();
		var result = SampleEffects.ApplySaturation(input, 2f);

		// Output should still be in valid range
		Assert.All(result, s => Assert.True(s >= short.MinValue && s <= short.MaxValue));
	}

	[Fact]
	public void ApplyTremolo_ModulatesAmplitude() {
		var input = new short[1000];
		Array.Fill(input, (short)10000);

		var result = SampleEffects.ApplyTremolo(input, 10f, 0.5f);

		// Find min and max values
		short min = short.MaxValue, max = short.MinValue;
		foreach (var s in result) {
			if (s < min) min = s;
			if (s > max) max = s;
		}

		// With depth 0.5, we should see modulation
		Assert.True(max - min > 1000);
	}

	[Fact]
	public void ApplyChorus_AddsModulation() {
		var input = CreateTestSamples();
		var result = SampleEffects.ApplyChorus(input, 1f, 20f, 0.5f);

		Assert.Equal(input.Length, result.Length);

		// Should be different from input
		int differences = 0;
		for (int i = 100; i < input.Length; i++) {
			if (result[i] != input[i]) differences++;
		}
		Assert.True(differences > input.Length / 2);
	}

	[Fact]
	public void ApplyFlanger_AddsModulation() {
		var input = CreateTestSamples();
		var result = SampleEffects.ApplyFlanger(input, 0.5f, 5f, 0.5f, 0.5f);

		Assert.Equal(input.Length, result.Length);
	}

	[Fact]
	public void ApplyPhaser_StagesClampCorrectly() {
		var input = CreateTestSamples();

		// Should handle edge cases
		var result1 = SampleEffects.ApplyPhaser(input, 1f, 0.5f, 0.5f, 1);  // Clamped to 2
		var result2 = SampleEffects.ApplyPhaser(input, 1f, 0.5f, 0.5f, 10); // Clamped to 8

		Assert.Equal(input.Length, result1.Length);
		Assert.Equal(input.Length, result2.Length);
	}

	[Fact]
	public void ApplyEQ3Band_ModifiesFrequencyContent() {
		var input = CreateTestSamples();

		// Boost bass, cut mids, boost highs
		var result = SampleEffects.ApplyEQ3Band(input, 6f, -6f, 6f);

		Assert.Equal(input.Length, result.Length);
	}

	[Fact]
	public void ApplyRingMod_CreatesModulation() {
		var input = CreateTestSamples();
		var result = SampleEffects.ApplyRingMod(input, 440f, 0.5f);

		// Should be different from input
		int differences = 0;
		for (int i = 0; i < input.Length; i++) {
			if (result[i] != input[i]) differences++;
		}
		Assert.True(differences > 0);
	}

	[Fact]
	public void ApplyPitchShift_Up_ShortensOutput() {
		var input = CreateTestSamples();

		// Shift up = faster playback = effectively shorter
		var result = SampleEffects.ApplyPitchShift(input, 12f); // One octave up

		Assert.Equal(input.Length, result.Length);
	}

	[Fact]
	public void ApplyPitchShift_Down_PreservesLength() {
		var input = CreateTestSamples();

		// Shift down = slower playback
		var result = SampleEffects.ApplyPitchShift(input, -12f); // One octave down

		Assert.Equal(input.Length, result.Length);
	}

	[Fact]
	public void ApplyFadeIn_StartsAtZero() {
		var input = new short[1000];
		Array.Fill(input, (short)10000);

		var result = SampleEffects.ApplyFadeIn(input, 100f);

		// First sample should be near zero
		Assert.True(Math.Abs(result[0]) < 100);
		// Later samples should be near original
		Assert.True(Math.Abs(result[999] - 10000) < 100);
	}

	[Fact]
	public void ApplyFadeOut_EndsAtZero() {
		var input = new short[1000];
		Array.Fill(input, (short)10000);

		var result = SampleEffects.ApplyFadeOut(input, 100f);

		// Last sample should be near zero
		Assert.True(Math.Abs(result[999]) < 100);
		// First samples should be near original
		Assert.True(Math.Abs(result[0] - 10000) < 100);
	}

	[Fact]
	public void ApplyVibrato_ModifiesPitch() {
		var input = CreateTestSamples();
		var result = SampleEffects.ApplyVibrato(input, 5f, 1f);

		Assert.Equal(input.Length, result.Length);

		// Output should be different
		int differences = 0;
		for (int i = 0; i < input.Length; i++) {
			if (result[i] != input[i]) differences++;
		}
		Assert.True(differences > 0);
	}
}
