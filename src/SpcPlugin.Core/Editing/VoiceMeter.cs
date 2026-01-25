using SpcPlugin.Core.Hardware;

namespace SpcPlugin.Core.Editing;

/// <summary>
/// Real-time voice level metering for the 8 S-DSP voices.
/// Provides peak levels, RMS, and clipping detection.
/// </summary>
public sealed class VoiceMeter {
	private readonly float[] _peakLevels = new float[SnesDspLimits.VoiceCount];
	private readonly float[] _rmsLevels = new float[SnesDspLimits.VoiceCount];
	private readonly float[] _peakHold = new float[SnesDspLimits.VoiceCount];
	private readonly int[] _peakHoldCountdown = new int[SnesDspLimits.VoiceCount];
	private readonly bool[] _clipping = new bool[SnesDspLimits.VoiceCount];
	private readonly RmsAccumulator[] _rmsAccumulators = new RmsAccumulator[SnesDspLimits.VoiceCount];

	private const int PeakHoldSamples = SnesDspLimits.SampleRate; // 1 second at 32kHz
	private const float DecayRate = 0.9995f;   // Peak decay rate per sample
	private const float ClipThreshold = 0.99f;

	/// <summary>
	/// Creates a new voice meter.
	/// </summary>
	public VoiceMeter() {
		for (int i = 0; i < SnesDspLimits.VoiceCount; i++) {
			_rmsAccumulators[i] = new RmsAccumulator(1024);
		}
	}

	/// <summary>Event raised when levels are updated.</summary>
	public event EventHandler<VoiceLevelEventArgs>? LevelsUpdated;

	/// <summary>Gets the peak level for a voice (0-1).</summary>
	public float GetPeakLevel(int voice) {
		if (voice < 0 || voice > 7) return 0;
		return _peakLevels[voice];
	}

	/// <summary>Gets the RMS level for a voice (0-1).</summary>
	public float GetRmsLevel(int voice) {
		if (voice < 0 || voice > 7) return 0;
		return _rmsLevels[voice];
	}

	/// <summary>Gets the peak hold level for a voice (0-1).</summary>
	public float GetPeakHold(int voice) {
		if (voice < 0 || voice > 7) return 0;
		return _peakHold[voice];
	}

	/// <summary>Gets whether a voice is clipping.</summary>
	public bool IsClipping(int voice) {
		if (voice < 0 || voice > 7) return false;
		return _clipping[voice];
	}

	/// <summary>Gets all peak levels.</summary>
	public float[] GetAllPeakLevels() {
		return (float[])_peakLevels.Clone();
	}

	/// <summary>Gets all RMS levels.</summary>
	public float[] GetAllRmsLevels() {
		return (float[])_rmsLevels.Clone();
	}

	/// <summary>Gets all peak hold levels.</summary>
	public float[] GetAllPeakHold() {
		return (float[])_peakHold.Clone();
	}

	/// <summary>Gets all clipping states.</summary>
	public bool[] GetAllClipping() {
		return (bool[])_clipping.Clone();
	}

	/// <summary>
	/// Processes voice output samples and updates meters.
	/// Call this from the audio processing thread.
	/// </summary>
	/// <param name="voiceOutputs">Array of 8 voice output buffers.</param>
	/// <param name="numSamples">Number of samples per buffer.</param>
	public void ProcessVoiceOutputs(float[][] voiceOutputs, int numSamples) {
		if (voiceOutputs.Length < 8) return;

		bool hasUpdate = false;

		for (int voice = 0; voice < 8; voice++) {
			if (voiceOutputs[voice] == null || voiceOutputs[voice].Length < numSamples) {
				continue;
			}

			float peak = 0;
			bool clipped = false;

			for (int i = 0; i < numSamples; i++) {
				float abs = Math.Abs(voiceOutputs[voice][i]);
				if (abs > peak) peak = abs;
				if (abs >= ClipThreshold) clipped = true;

				// Accumulate for RMS
				_rmsAccumulators[voice].Add(voiceOutputs[voice][i]);
			}

			// Update peak with decay
			if (peak > _peakLevels[voice]) {
				_peakLevels[voice] = peak;
			} else {
				_peakLevels[voice] *= (float)Math.Pow(DecayRate, numSamples);
			}

			// Update peak hold
			if (peak > _peakHold[voice]) {
				_peakHold[voice] = peak;
				_peakHoldCountdown[voice] = PeakHoldSamples;
			} else {
				_peakHoldCountdown[voice] -= numSamples;
				if (_peakHoldCountdown[voice] <= 0) {
					_peakHold[voice] *= (float)Math.Pow(DecayRate, numSamples);
				}
			}

			// Update RMS
			_rmsLevels[voice] = _rmsAccumulators[voice].GetRms();

			// Update clipping
			_clipping[voice] = clipped;

			hasUpdate = true;
		}

		if (hasUpdate) {
			OnLevelsUpdated();
		}
	}

	/// <summary>
	/// Processes voice output samples from ENVX/OUTX DSP registers.
	/// </summary>
	/// <param name="envx">ENVX values for 8 voices (envelope level).</param>
	/// <param name="outx">OUTX values for 8 voices (output level).</param>
	public void ProcessDspOutputs(byte[] envx, byte[] outx) {
		if (envx.Length < 8 || outx.Length < 8) return;

		for (int voice = 0; voice < 8; voice++) {
			// OUTX is signed 8-bit output sample
			float level = Math.Abs((sbyte)outx[voice]) / 128f;

			// Update peak
			if (level > _peakLevels[voice]) {
				_peakLevels[voice] = level;
			} else {
				_peakLevels[voice] *= DecayRate;
			}

			// Update peak hold
			if (level > _peakHold[voice]) {
				_peakHold[voice] = level;
				_peakHoldCountdown[voice] = PeakHoldSamples / 32; // Fewer updates
			} else if (_peakHoldCountdown[voice] > 0) {
				_peakHoldCountdown[voice]--;
			} else {
				_peakHold[voice] *= DecayRate;
			}

			// Simple RMS approximation
			_rmsLevels[voice] = _rmsLevels[voice] * 0.95f + level * 0.05f;

			// Clipping detection
			_clipping[voice] = level >= ClipThreshold;
		}

		OnLevelsUpdated();
	}

	/// <summary>
	/// Resets all meters to zero.
	/// </summary>
	public void Reset() {
		Array.Clear(_peakLevels);
		Array.Clear(_rmsLevels);
		Array.Clear(_peakHold);
		Array.Clear(_peakHoldCountdown);
		Array.Clear(_clipping);

		for (int i = 0; i < 8; i++) {
			_rmsAccumulators[i].Reset();
		}
	}

	/// <summary>
	/// Resets clip indicators.
	/// </summary>
	public void ResetClipping() {
		Array.Clear(_clipping);
	}

	/// <summary>
	/// Resets peak hold.
	/// </summary>
	public void ResetPeakHold() {
		Array.Clear(_peakHold);
		Array.Clear(_peakHoldCountdown);
	}

	/// <summary>
	/// Gets a snapshot of all meter values.
	/// </summary>
	public VoiceMeterSnapshot GetSnapshot() {
		return new VoiceMeterSnapshot {
			PeakLevels = (float[])_peakLevels.Clone(),
			RmsLevels = (float[])_rmsLevels.Clone(),
			PeakHold = (float[])_peakHold.Clone(),
			Clipping = (bool[])_clipping.Clone(),
		};
	}

	/// <summary>
	/// Converts a linear level (0-1) to dB.
	/// </summary>
	public static float LinearToDb(float linear) {
		if (linear <= 0) return float.NegativeInfinity;
		return 20f * MathF.Log10(linear);
	}

	/// <summary>
	/// Converts dB to linear level (0-1).
	/// </summary>
	public static float DbToLinear(float db) {
		return MathF.Pow(10f, db / 20f);
	}

	private void OnLevelsUpdated() {
		LevelsUpdated?.Invoke(this, new VoiceLevelEventArgs(GetSnapshot()));
	}

	/// <summary>
	/// Accumulator for RMS calculation.
	/// </summary>
	private sealed class RmsAccumulator {
		private readonly float[] _buffer;
		private int _index;
		private float _sum;
		private bool _filled;

		public RmsAccumulator(int windowSize) {
			_buffer = new float[windowSize];
		}

		public void Add(float sample) {
			float squared = sample * sample;

			// Remove old value, add new
			_sum -= _buffer[_index];
			_buffer[_index] = squared;
			_sum += squared;

			_index = (_index + 1) % _buffer.Length;
			if (_index == 0) _filled = true;
		}

		public float GetRms() {
			int count = _filled ? _buffer.Length : _index;
			if (count == 0) return 0;
			return MathF.Sqrt(_sum / count);
		}

		public void Reset() {
			Array.Clear(_buffer);
			_index = 0;
			_sum = 0;
			_filled = false;
		}
	}
}

/// <summary>
/// Snapshot of voice meter values.
/// </summary>
public sealed class VoiceMeterSnapshot {
	/// <summary>Peak levels (0-1) for each voice.</summary>
	public float[] PeakLevels { get; init; } = new float[8];

	/// <summary>RMS levels (0-1) for each voice.</summary>
	public float[] RmsLevels { get; init; } = new float[8];

	/// <summary>Peak hold levels (0-1) for each voice.</summary>
	public float[] PeakHold { get; init; } = new float[8];

	/// <summary>Clipping state for each voice.</summary>
	public bool[] Clipping { get; init; } = new bool[8];

	/// <summary>Gets peak level in dB for a voice.</summary>
	public float GetPeakDb(int voice) => VoiceMeter.LinearToDb(PeakLevels[voice]);

	/// <summary>Gets RMS level in dB for a voice.</summary>
	public float GetRmsDb(int voice) => VoiceMeter.LinearToDb(RmsLevels[voice]);
}

/// <summary>
/// Event args for voice level updates.
/// </summary>
public sealed class VoiceLevelEventArgs : EventArgs {
	/// <summary>Current meter snapshot.</summary>
	public VoiceMeterSnapshot Snapshot { get; }

	public VoiceLevelEventArgs(VoiceMeterSnapshot snapshot) {
		Snapshot = snapshot;
	}
}
