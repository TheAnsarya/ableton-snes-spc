using SpcPlugin.Core.RealTime;

namespace SpcPlugin.Core.Effects;

/// <summary>
/// Additional audio effects/filters for sample processing.
/// </summary>
public static class SampleEffects {
	/// <summary>
	/// Applies a simple delay effect to samples.
	/// </summary>
	/// <param name="samples">Input samples.</param>
	/// <param name="delayMs">Delay time in milliseconds.</param>
	/// <param name="feedback">Feedback amount (0-1).</param>
	/// <param name="wet">Wet/dry mix (0-1).</param>
	/// <param name="sampleRate">Sample rate (default 32000 for SNES).</param>
	/// <returns>Processed samples.</returns>
	public static short[] ApplyDelay(short[] samples, float delayMs, float feedback, float wet, int sampleRate = 32000) {
		int delaySamples = (int)(delayMs * sampleRate / 1000f);
		if (delaySamples <= 0 || delaySamples >= samples.Length) return samples;

		var result = new short[samples.Length];
		var delayBuffer = new float[delaySamples];
		int writeIndex = 0;
		float dry = 1f - wet;

		feedback = Math.Clamp(feedback, 0f, 0.95f);

		for (int i = 0; i < samples.Length; i++) {
			float input = samples[i];
			float delayed = delayBuffer[writeIndex];

			float output = (input * dry) + (delayed * wet);
			delayBuffer[writeIndex] = input + (delayed * feedback);

			writeIndex = (writeIndex + 1) % delaySamples;

			result[i] = (short)Math.Clamp(output, short.MinValue, short.MaxValue);
		}

		return result;
	}

	/// <summary>
	/// Applies a bitcrusher effect (reduces bit depth).
	/// </summary>
	/// <param name="samples">Input samples.</param>
	/// <param name="bits">Target bit depth (1-16).</param>
	/// <returns>Processed samples.</returns>
	public static short[] ApplyBitCrush(short[] samples, int bits) {
		bits = Math.Clamp(bits, 1, 16);
		int step = 1 << (16 - bits);

		var result = new short[samples.Length];
		for (int i = 0; i < samples.Length; i++) {
			// Quantize to fewer bits
			result[i] = (short)(samples[i] / step * step);
		}

		return result;
	}

	/// <summary>
	/// Applies sample rate reduction (downsampling without filtering).
	/// </summary>
	/// <param name="samples">Input samples.</param>
	/// <param name="factor">Reduction factor (2 = half sample rate).</param>
	/// <returns>Processed samples.</returns>
	public static short[] ApplySampleRateReduce(short[] samples, int factor) {
		factor = Math.Clamp(factor, 1, 16);

		var result = new short[samples.Length];
		short held = 0;

		for (int i = 0; i < samples.Length; i++) {
			if (i % factor == 0) {
				held = samples[i];
			}

			result[i] = held;
		}

		return result;
	}

	/// <summary>
	/// Applies a simple compressor/limiter.
	/// </summary>
	/// <param name="samples">Input samples.</param>
	/// <param name="threshold">Threshold (0-1, percentage of max).</param>
	/// <param name="ratio">Compression ratio (e.g., 4 = 4:1).</param>
	/// <param name="makeupGain">Makeup gain multiplier.</param>
	/// <returns>Processed samples.</returns>
	public static short[] ApplyCompressor(short[] samples, float threshold, float ratio, float makeupGain = 1f) {
		threshold = Math.Clamp(threshold, 0.1f, 1f);
		ratio = Math.Max(1f, ratio);

		float thresholdValue = threshold * 32767f;

		var result = new short[samples.Length];
		for (int i = 0; i < samples.Length; i++) {
			float input = samples[i];
			float absInput = Math.Abs(input);

			float output;
			if (absInput > thresholdValue) {
				// Compress
				float excess = absInput - thresholdValue;
				float compressed = thresholdValue + (excess / ratio);
				output = Math.Sign(input) * compressed;
			} else {
				output = input;
			}

			output *= makeupGain;
			result[i] = (short)Math.Clamp(output, short.MinValue, short.MaxValue);
		}

		return result;
	}

	/// <summary>
	/// Applies a soft clipper/saturation effect.
	/// </summary>
	/// <param name="samples">Input samples.</param>
	/// <param name="drive">Drive amount (1 = clean, higher = more saturation).</param>
	/// <returns>Processed samples.</returns>
	public static short[] ApplySaturation(short[] samples, float drive) {
		drive = Math.Max(1f, drive);

		var result = new short[samples.Length];
		for (int i = 0; i < samples.Length; i++) {
			// Normalize to -1..1
			float x = samples[i] / 32768f * drive;

			// Soft clip using tanh
			float output = MathF.Tanh(x);

			result[i] = (short)(output * 32767f);
		}

		return result;
	}

	/// <summary>
	/// Applies a tremolo effect (amplitude modulation).
	/// </summary>
	/// <param name="samples">Input samples.</param>
	/// <param name="rate">Modulation rate in Hz.</param>
	/// <param name="depth">Modulation depth (0-1).</param>
	/// <param name="sampleRate">Sample rate.</param>
	/// <returns>Processed samples.</returns>
	public static short[] ApplyTremolo(short[] samples, float rate, float depth, int sampleRate = 32000) {
		depth = Math.Clamp(depth, 0f, 1f);

		var result = new short[samples.Length];
		float phaseIncrement = 2f * MathF.PI * rate / sampleRate;
		float phase = 0;

		for (int i = 0; i < samples.Length; i++) {
			float mod = 1f - (depth * (0.5f + (0.5f * MathF.Sin(phase))));
			result[i] = (short)(samples[i] * mod);
			phase += phaseIncrement;
			if (phase > 2f * MathF.PI) phase -= 2f * MathF.PI;
		}

		return result;
	}

	/// <summary>
	/// Applies a vibrato effect (pitch modulation).
	/// </summary>
	/// <param name="samples">Input samples.</param>
	/// <param name="rate">Modulation rate in Hz.</param>
	/// <param name="depth">Modulation depth in semitones.</param>
	/// <param name="sampleRate">Sample rate.</param>
	/// <returns>Processed samples.</returns>
	public static short[] ApplyVibrato(short[] samples, float rate, float depth, int sampleRate = 32000) {
		// Max delay for pitch shift
		int maxDelay = (int)(sampleRate * 0.05f); // 50ms max
		var delayBuffer = new short[maxDelay];
		int bufferIndex = 0;

		var result = new short[samples.Length];
		float phaseIncrement = 2f * MathF.PI * rate / sampleRate;
		float phase = 0;

		for (int i = 0; i < samples.Length; i++) {
			delayBuffer[bufferIndex] = samples[i];

			// Calculate variable delay
			float mod = 0.5f + (0.5f * MathF.Sin(phase));
			int delay = (int)(mod * maxDelay * depth / 12f); // depth in semitones
			delay = Math.Clamp(delay, 0, maxDelay - 1);

			int readIndex = (bufferIndex - delay + maxDelay) % maxDelay;
			result[i] = delayBuffer[readIndex];

			bufferIndex = (bufferIndex + 1) % maxDelay;
			phase += phaseIncrement;
		}

		return result;
	}

	/// <summary>
	/// Applies a chorus effect.
	/// </summary>
	/// <param name="samples">Input samples.</param>
	/// <param name="rate">Modulation rate in Hz.</param>
	/// <param name="depth">Depth in ms.</param>
	/// <param name="wet">Wet/dry mix (0-1).</param>
	/// <param name="sampleRate">Sample rate.</param>
	/// <returns>Processed samples.</returns>
	public static short[] ApplyChorus(short[] samples, float rate, float depth, float wet, int sampleRate = 32000) {
		int maxDelay = (int)(depth * 2 * sampleRate / 1000f) + 1;
		var delayBuffer = new float[maxDelay];
		int bufferIndex = 0;

		var result = new short[samples.Length];
		float phaseIncrement = 2f * MathF.PI * rate / sampleRate;
		float phase = 0;
		float dry = 1f - wet;

		int baseDelay = maxDelay / 2;

		for (int i = 0; i < samples.Length; i++) {
			delayBuffer[bufferIndex] = samples[i];

			// Variable delay with LFO
			float mod = MathF.Sin(phase);
			float delay = baseDelay + (mod * ((maxDelay / 2) - 1));

			// Linear interpolation
			int delay1 = (int)delay;
			int delay2 = delay1 + 1;
			float frac = delay - delay1;

			int idx1 = (bufferIndex - delay1 + maxDelay) % maxDelay;
			int idx2 = (bufferIndex - delay2 + maxDelay) % maxDelay;

			float delayed = (delayBuffer[idx1] * (1 - frac)) + (delayBuffer[idx2] * frac);
			float output = (samples[i] * dry) + (delayed * wet);

			result[i] = (short)Math.Clamp(output, short.MinValue, short.MaxValue);

			bufferIndex = (bufferIndex + 1) % maxDelay;
			phase += phaseIncrement;
		}

		return result;
	}

	/// <summary>
	/// Applies a flanger effect.
	/// </summary>
	/// <param name="samples">Input samples.</param>
	/// <param name="rate">Modulation rate in Hz.</param>
	/// <param name="depth">Depth in ms.</param>
	/// <param name="feedback">Feedback amount (0-0.9).</param>
	/// <param name="wet">Wet/dry mix.</param>
	/// <param name="sampleRate">Sample rate.</param>
	/// <returns>Processed samples.</returns>
	public static short[] ApplyFlanger(short[] samples, float rate, float depth, float feedback, float wet, int sampleRate = 32000) {
		int maxDelay = (int)(depth * sampleRate / 1000f) + 1;
		var delayBuffer = new float[maxDelay];
		int bufferIndex = 0;

		var result = new short[samples.Length];
		float phaseIncrement = 2f * MathF.PI * rate / sampleRate;
		float phase = 0;
		float dry = 1f - wet;
		feedback = Math.Clamp(feedback, 0f, 0.9f);

		for (int i = 0; i < samples.Length; i++) {
			// Variable delay (0 to maxDelay)
			float mod = 0.5f + (0.5f * MathF.Sin(phase));
			float delay = mod * (maxDelay - 1);

			int delay1 = (int)delay;
			int delay2 = Math.Min(delay1 + 1, maxDelay - 1);
			float frac = delay - delay1;

			int idx1 = (bufferIndex - delay1 + maxDelay) % maxDelay;
			int idx2 = (bufferIndex - delay2 + maxDelay) % maxDelay;

			float delayed = (delayBuffer[idx1] * (1 - frac)) + (delayBuffer[idx2] * frac);

			// Store input + feedback
			delayBuffer[bufferIndex] = samples[i] + (delayed * feedback);

			float output = (samples[i] * dry) + (delayed * wet);
			result[i] = (short)Math.Clamp(output, short.MinValue, short.MaxValue);

			bufferIndex = (bufferIndex + 1) % maxDelay;
			phase += phaseIncrement;
		}

		return result;
	}

	/// <summary>
	/// Applies a phaser effect.
	/// </summary>
	/// <param name="samples">Input samples.</param>
	/// <param name="rate">Modulation rate in Hz.</param>
	/// <param name="depth">Depth (0-1).</param>
	/// <param name="feedback">Feedback (0-0.9).</param>
	/// <param name="stages">Number of allpass stages (2-8).</param>
	/// <param name="sampleRate">Sample rate.</param>
	/// <returns>Processed samples.</returns>
	public static short[] ApplyPhaser(short[] samples, float rate, float depth, float feedback, int stages, int sampleRate = 32000) {
		stages = Math.Clamp(stages, 2, 8);
		feedback = Math.Clamp(feedback, 0f, 0.9f);

		var result = new short[samples.Length];
		var allpassBuffers = new float[stages];
		float phaseIncrement = 2f * MathF.PI * rate / sampleRate;
		float phase = 0;

		float minFreq = 200f;
		float maxFreq = 4000f;

		for (int i = 0; i < samples.Length; i++) {
			// Calculate allpass coefficient from LFO
			float mod = 0.5f + (0.5f * MathF.Sin(phase));
			float freq = minFreq + (mod * depth * (maxFreq - minFreq));
			float coef = (MathF.Tan(MathF.PI * freq / sampleRate) - 1) /
						 (MathF.Tan(MathF.PI * freq / sampleRate) + 1);

			float input = samples[i] / 32768f;

			// Apply allpass stages
			float signal = input;
			for (int s = 0; s < stages; s++) {
				float allpassOut = (coef * signal) + allpassBuffers[s];
				allpassBuffers[s] = signal - (coef * allpassOut);
				signal = allpassOut;
			}

			// Mix with feedback
			float output = (input + (signal * feedback)) * 0.5f;

			result[i] = (short)(Math.Clamp(output, -1f, 1f) * 32767f);
			phase += phaseIncrement;
		}

		return result;
	}

	/// <summary>
	/// Applies a 3-band EQ.
	/// </summary>
	/// <param name="samples">Input samples.</param>
	/// <param name="lowGain">Low band gain in dB.</param>
	/// <param name="midGain">Mid band gain in dB.</param>
	/// <param name="highGain">High band gain in dB.</param>
	/// <param name="lowFreq">Low/mid crossover frequency.</param>
	/// <param name="highFreq">Mid/high crossover frequency.</param>
	/// <param name="sampleRate">Sample rate.</param>
	/// <returns>Processed samples.</returns>
	public static short[] ApplyEQ3Band(short[] samples, float lowGain, float midGain, float highGain,
		float lowFreq = 300f, float highFreq = 3000f, int sampleRate = 32000) {
		// Convert dB to linear
		float lowMul = MathF.Pow(10f, lowGain / 20f);
		float midMul = MathF.Pow(10f, midGain / 20f);
		float highMul = MathF.Pow(10f, highGain / 20f);

		// Simple crossover filters
		float lowRC = 1f / (2f * MathF.PI * lowFreq);
		float highRC = 1f / (2f * MathF.PI * highFreq);
		float dt = 1f / sampleRate;

		float lowAlpha = dt / (lowRC + dt);
		float highAlpha = dt / (highRC + dt);

		var result = new short[samples.Length];
		float lowPass1 = 0, lowPass2 = 0;

		for (int i = 0; i < samples.Length; i++) {
			float input = samples[i] / 32768f;

			// First crossover (low/mid-high)
			lowPass1 += lowAlpha * (input - lowPass1);
			float low = lowPass1;
			float midHigh = input - low;

			// Second crossover (mid/high)
			lowPass2 += highAlpha * (midHigh - lowPass2);
			float mid = lowPass2;
			float high = midHigh - mid;

			// Apply gains and sum
			float output = (low * lowMul) + (mid * midMul) + (high * highMul);

			result[i] = (short)(Math.Clamp(output, -1f, 1f) * 32767f);
		}

		return result;
	}

	/// <summary>
	/// Applies a ring modulator effect.
	/// </summary>
	/// <param name="samples">Input samples.</param>
	/// <param name="frequency">Modulation frequency in Hz.</param>
	/// <param name="wet">Wet/dry mix.</param>
	/// <param name="sampleRate">Sample rate.</param>
	/// <returns>Processed samples.</returns>
	public static short[] ApplyRingMod(short[] samples, float frequency, float wet, int sampleRate = 32000) {
		var result = new short[samples.Length];
		float phaseIncrement = 2f * MathF.PI * frequency / sampleRate;
		float phase = 0;
		float dry = 1f - wet;

		for (int i = 0; i < samples.Length; i++) {
			float mod = MathF.Sin(phase);
			float modulated = samples[i] * mod;
			float output = (samples[i] * dry) + (modulated * wet);

			result[i] = (short)Math.Clamp(output, short.MinValue, short.MaxValue);
			phase += phaseIncrement;
		}

		return result;
	}

	/// <summary>
	/// Applies pitch shifting (simple version using resampling).
	/// </summary>
	/// <param name="samples">Input samples.</param>
	/// <param name="semitones">Pitch shift in semitones.</param>
	/// <returns>Processed samples.</returns>
	public static short[] ApplyPitchShift(short[] samples, float semitones) {
		float ratio = MathF.Pow(2f, semitones / 12f);
		int newLength = (int)(samples.Length / ratio);

		if (newLength <= 0) return samples;

		var result = new short[samples.Length];

		// Simple linear interpolation resampling
		for (int i = 0; i < samples.Length; i++) {
			float srcPos = i * ratio;
			int srcIdx = (int)srcPos;
			float frac = srcPos - srcIdx;

			if (srcIdx >= samples.Length - 1) {
				srcIdx = samples.Length - 1;
				frac = 0;
			}

			int next = Math.Min(srcIdx + 1, samples.Length - 1);
			result[i] = (short)((samples[srcIdx] * (1 - frac)) + (samples[next] * frac));
		}

		return result;
	}

	/// <summary>
	/// Applies fade in effect.
	/// </summary>
	public static short[] ApplyFadeIn(short[] samples, float durationMs, int sampleRate = 32000) {
		int fadeSamples = (int)(durationMs * sampleRate / 1000f);
		fadeSamples = Math.Min(fadeSamples, samples.Length);

		var result = (short[])samples.Clone();

		for (int i = 0; i < fadeSamples; i++) {
			float gain = (float)i / fadeSamples;
			result[i] = (short)(samples[i] * gain);
		}

		return result;
	}

	/// <summary>
	/// Applies fade out effect.
	/// </summary>
	public static short[] ApplyFadeOut(short[] samples, float durationMs, int sampleRate = 32000) {
		int fadeSamples = (int)(durationMs * sampleRate / 1000f);
		fadeSamples = Math.Min(fadeSamples, samples.Length);

		var result = (short[])samples.Clone();
		int fadeStart = samples.Length - fadeSamples;

		for (int i = 0; i < fadeSamples; i++) {
			float gain = 1f - ((float)i / fadeSamples);
			result[fadeStart + i] = (short)(samples[fadeStart + i] * gain);
		}

		return result;
	}
}

/// <summary>
/// Extended sample filter types.
/// </summary>
public enum ExtendedSampleFilter {
	// Basic filters (from SampleFilter)
	LowPass,
	HighPass,
	Boost,
	Attenuate,
	Reverse,
	Normalize,

	// Additional filters
	Delay,
	BitCrush,
	SampleRateReduce,
	Compressor,
	Saturation,
	Tremolo,
	Vibrato,
	Chorus,
	Flanger,
	Phaser,
	EQ3Band,
	RingMod,
	PitchShift,
	FadeIn,
	FadeOut
}
