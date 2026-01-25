namespace SpcPlugin.Core.Audio;

/// <summary>
/// Resampler for converting audio between sample rates.
/// Uses high-quality sinc interpolation for best results.
/// </summary>
public static class Resampler {
	/// <summary>
	/// SNES native sample rate (32 kHz nominal).
	/// </summary>
	public const int SnesSampleRate = 32000;

	/// <summary>
	/// Resamples audio to a target sample rate using sinc interpolation.
	/// </summary>
	/// <param name="samples">Input samples.</param>
	/// <param name="sourceSampleRate">Source sample rate in Hz.</param>
	/// <param name="targetSampleRate">Target sample rate in Hz.</param>
	/// <returns>Resampled audio.</returns>
	public static short[] Resample(short[] samples, int sourceSampleRate, int targetSampleRate) {
		if (sourceSampleRate == targetSampleRate) {
			return (short[])samples.Clone();
		}

		double ratio = (double)targetSampleRate / sourceSampleRate;
		int newLength = (int)(samples.Length * ratio);
		var output = new short[newLength];

		// Use windowed sinc interpolation for high quality
		const int windowSize = 16;
		for (int i = 0; i < newLength; i++) {
			double sourcePos = i / ratio;
			int sourceIndex = (int)sourcePos;
			double frac = sourcePos - sourceIndex;

			double sum = 0;
			double weightSum = 0;

			for (int j = -windowSize; j <= windowSize; j++) {
				int idx = sourceIndex + j;
				if (idx < 0 || idx >= samples.Length) continue;

				double x = j - frac;
				double weight = Sinc(x) * BlackmanWindow(x, windowSize);
				sum += samples[idx] * weight;
				weightSum += weight;
			}

			output[i] = weightSum > 0
				? (short)Math.Clamp(sum / weightSum, short.MinValue, short.MaxValue)
				: (short)0;
		}

		return output;
	}

	/// <summary>
	/// Resamples audio to SNES native rate (32 kHz).
	/// </summary>
	public static short[] ResampleToSnes(short[] samples, int sourceSampleRate) {
		return Resample(samples, sourceSampleRate, SnesSampleRate);
	}

	/// <summary>
	/// Normalizes audio to a target peak level.
	/// </summary>
	/// <param name="samples">Input samples.</param>
	/// <param name="targetPeakDb">Target peak level in dB (default -1 dB).</param>
	/// <returns>Normalized samples.</returns>
	public static short[] Normalize(short[] samples, double targetPeakDb = -1.0) {
		if (samples.Length == 0) return [];

		// Find current peak
		int maxAbs = 0;
		foreach (short s in samples) {
			int abs = Math.Abs(s);
			if (abs > maxAbs) maxAbs = abs;
		}

		if (maxAbs == 0) return (short[])samples.Clone();

		// Calculate gain
		double targetPeak = 32767 * Math.Pow(10, targetPeakDb / 20);
		double gain = targetPeak / maxAbs;

		// Apply gain
		var output = new short[samples.Length];
		for (int i = 0; i < samples.Length; i++) {
			output[i] = (short)Math.Clamp(samples[i] * gain, short.MinValue, short.MaxValue);
		}

		return output;
	}

	/// <summary>
	/// Truncates or pads samples to fit within BRR block boundaries (16 samples per block).
	/// </summary>
	/// <param name="samples">Input samples.</param>
	/// <param name="maxBlocks">Maximum number of BRR blocks (optional).</param>
	/// <returns>Aligned samples.</returns>
	public static short[] AlignToBrrBlocks(short[] samples, int? maxBlocks = null) {
		const int samplesPerBlock = 16;

		int targetLength = ((samples.Length + samplesPerBlock - 1) / samplesPerBlock) * samplesPerBlock;

		if (maxBlocks.HasValue) {
			int maxLength = maxBlocks.Value * samplesPerBlock;
			if (targetLength > maxLength) {
				targetLength = maxLength;
			}
		}

		var output = new short[targetLength];
		int copyLength = Math.Min(samples.Length, targetLength);
		Array.Copy(samples, output, copyLength);

		return output;
	}

	/// <summary>
	/// Applies a fade out to prevent clicks at sample end.
	/// </summary>
	/// <param name="samples">Input samples.</param>
	/// <param name="fadeSamples">Number of samples to fade (default 32).</param>
	/// <returns>Samples with fade applied.</returns>
	public static short[] ApplyFadeOut(short[] samples, int fadeSamples = 32) {
		if (samples.Length <= fadeSamples) {
			return (short[])samples.Clone();
		}

		var output = (short[])samples.Clone();
		int startFade = samples.Length - fadeSamples;

		for (int i = 0; i < fadeSamples; i++) {
			double fade = 1.0 - ((double)i / fadeSamples);
			output[startFade + i] = (short)(output[startFade + i] * fade);
		}

		return output;
	}

	/// <summary>
	/// Extracts a loop region from samples and prepares it for BRR encoding.
	/// </summary>
	/// <param name="samples">Input samples.</param>
	/// <param name="loopStart">Loop start position in samples.</param>
	/// <param name="loopEnd">Loop end position in samples (optional, defaults to end).</param>
	/// <returns>Tuple of (pre-loop samples, loop samples).</returns>
	public static (short[] PreLoop, short[] Loop) ExtractLoopRegion(
		short[] samples,
		int loopStart,
		int? loopEnd = null) {
		int end = loopEnd ?? samples.Length;

		// Align loop points to BRR block boundaries
		const int samplesPerBlock = 16;
		loopStart = (loopStart / samplesPerBlock) * samplesPerBlock;
		end = ((end + samplesPerBlock - 1) / samplesPerBlock) * samplesPerBlock;

		if (loopStart >= samples.Length) {
			loopStart = (samples.Length / samplesPerBlock) * samplesPerBlock - samplesPerBlock;
			if (loopStart < 0) loopStart = 0;
		}

		// Extract regions
		var preLoop = new short[loopStart];
		var loop = new short[end - loopStart];

		if (loopStart > 0) {
			Array.Copy(samples, 0, preLoop, 0, Math.Min(loopStart, samples.Length));
		}

		int loopCopyLength = Math.Min(end - loopStart, samples.Length - loopStart);
		if (loopCopyLength > 0 && loopStart < samples.Length) {
			Array.Copy(samples, loopStart, loop, 0, loopCopyLength);
		}

		return (preLoop, loop);
	}

	private static double Sinc(double x) {
		if (Math.Abs(x) < 1e-10) return 1.0;
		double px = Math.PI * x;
		return Math.Sin(px) / px;
	}

	private static double BlackmanWindow(double x, int windowSize) {
		if (Math.Abs(x) > windowSize) return 0;
		double n = (x / windowSize + 1) / 2;  // Normalize to 0-1
		return 0.42 - 0.5 * Math.Cos(2 * Math.PI * n) + 0.08 * Math.Cos(4 * Math.PI * n);
	}
}
