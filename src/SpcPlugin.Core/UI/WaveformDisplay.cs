using SpcPlugin.Core.Audio;
using SpcPlugin.Core.Editing;

namespace SpcPlugin.Core.UI;

/// <summary>
/// Provides waveform visualization data for UI rendering.
/// </summary>
public class WaveformDisplay {
	private readonly SpcEngine _engine;
	private float[] _leftChannel;
	private float[] _rightChannel;
	private float[] _mixedChannel;
	private float _peakLeft;
	private float _peakRight;
	private int _writePosition;
	private readonly object _lock = new();

	/// <summary>
	/// Creates a new waveform display with the specified buffer size.
	/// </summary>
	/// <param name="engine">The SPC engine to visualize.</param>
	/// <param name="bufferSize">Number of samples to buffer (default 2048 for ~46ms at 44.1kHz).</param>
	public WaveformDisplay(SpcEngine engine, int bufferSize = 2048) {
		_engine = engine;
		BufferSize = bufferSize;
		_leftChannel = new float[bufferSize];
		_rightChannel = new float[bufferSize];
		_mixedChannel = new float[bufferSize];
	}

	/// <summary>
	/// Buffer size in samples.
	/// </summary>
	public int BufferSize { get; }

	/// <summary>
	/// Peak level for left channel (0-1).
	/// </summary>
	public float PeakLeft {
		get {
			lock (_lock) return _peakLeft;
		}
	}

	/// <summary>
	/// Peak level for right channel (0-1).
	/// </summary>
	public float PeakRight {
		get {
			lock (_lock) return _peakRight;
		}
	}

	/// <summary>
	/// Peak level for mixed stereo (0-1).
	/// </summary>
	public float PeakMixed {
		get {
			lock (_lock) return Math.Max(_peakLeft, _peakRight);
		}
	}

	/// <summary>
	/// Gets the left channel waveform data.
	/// </summary>
	public float[] GetLeftChannel() {
		lock (_lock) {
			return GetOrderedBuffer(_leftChannel);
		}
	}

	/// <summary>
	/// Gets the right channel waveform data.
	/// </summary>
	public float[] GetRightChannel() {
		lock (_lock) {
			return GetOrderedBuffer(_rightChannel);
		}
	}

	/// <summary>
	/// Gets the mixed mono waveform data.
	/// </summary>
	public float[] GetMixedChannel() {
		lock (_lock) {
			return GetOrderedBuffer(_mixedChannel);
		}
	}

	/// <summary>
	/// Feeds audio samples to the waveform display.
	/// Call this from the audio processing callback.
	/// </summary>
	/// <param name="left">Left channel samples.</param>
	/// <param name="right">Right channel samples.</param>
	public void Feed(ReadOnlySpan<float> left, ReadOnlySpan<float> right) {
		lock (_lock) {
			int count = Math.Min(left.Length, right.Length);
			float peakL = 0, peakR = 0;

			for (int i = 0; i < count; i++) {
				float l = left[i];
				float r = right[i];

				_leftChannel[_writePosition] = l;
				_rightChannel[_writePosition] = r;
				_mixedChannel[_writePosition] = (l + r) * 0.5f;

				peakL = Math.Max(peakL, Math.Abs(l));
				peakR = Math.Max(peakR, Math.Abs(r));

				_writePosition = (_writePosition + 1) % BufferSize;
			}

			// Smooth peak with decay
			_peakLeft = Math.Max(peakL, _peakLeft * 0.95f);
			_peakRight = Math.Max(peakR, _peakRight * 0.95f);
		}
	}

	/// <summary>
	/// Feeds interleaved stereo samples to the waveform display.
	/// </summary>
	/// <param name="interleaved">Interleaved L/R samples.</param>
	public void FeedInterleaved(ReadOnlySpan<float> interleaved) {
		lock (_lock) {
			float peakL = 0, peakR = 0;

			for (int i = 0; i < interleaved.Length - 1; i += 2) {
				float l = interleaved[i];
				float r = interleaved[i + 1];

				_leftChannel[_writePosition] = l;
				_rightChannel[_writePosition] = r;
				_mixedChannel[_writePosition] = (l + r) * 0.5f;

				peakL = Math.Max(peakL, Math.Abs(l));
				peakR = Math.Max(peakR, Math.Abs(r));

				_writePosition = (_writePosition + 1) % BufferSize;
			}

			_peakLeft = Math.Max(peakL, _peakLeft * 0.95f);
			_peakRight = Math.Max(peakR, _peakRight * 0.95f);
		}
	}

	/// <summary>
	/// Clears the waveform buffer.
	/// </summary>
	public void Clear() {
		lock (_lock) {
			Array.Clear(_leftChannel);
			Array.Clear(_rightChannel);
			Array.Clear(_mixedChannel);
			_writePosition = 0;
			_peakLeft = 0;
			_peakRight = 0;
		}
	}

	/// <summary>
	/// Gets downsampled waveform data suitable for visualization.
	/// </summary>
	/// <param name="targetWidth">Target width in pixels/points.</param>
	/// <returns>Min/Max pairs for each column.</returns>
	public (float min, float max)[] GetDownsampledWaveform(int targetWidth) {
		lock (_lock) {
			var result = new (float min, float max)[targetWidth];
			var ordered = GetOrderedBuffer(_mixedChannel);
			int samplesPerBin = BufferSize / targetWidth;

			if (samplesPerBin < 1) samplesPerBin = 1;

			for (int i = 0; i < targetWidth; i++) {
				int start = i * samplesPerBin;
				int end = Math.Min(start + samplesPerBin, BufferSize);

				float min = float.MaxValue;
				float max = float.MinValue;

				for (int j = start; j < end; j++) {
					float v = ordered[j];
					if (v < min) min = v;
					if (v > max) max = v;
				}

				result[i] = (min == float.MaxValue ? 0 : min, max == float.MinValue ? 0 : max);
			}

			return result;
		}
	}

	/// <summary>
	/// Gets peak data for level meters (0-100 scale).
	/// </summary>
	public (int left, int right) GetLevelMeterValues() {
		lock (_lock) {
			return (
				(int)Math.Min(100, _peakLeft * 100),
				(int)Math.Min(100, _peakRight * 100)
			);
		}
	}

	/// <summary>
	/// Gets peak data in decibels.
	/// </summary>
	public (float left, float right) GetLevelMeterDb() {
		lock (_lock) {
			float dbL = _peakLeft > 0 ? 20f * MathF.Log10(_peakLeft) : -60f;
			float dbR = _peakRight > 0 ? 20f * MathF.Log10(_peakRight) : -60f;
			return (Math.Max(-60f, dbL), Math.Max(-60f, dbR));
		}
	}

	private float[] GetOrderedBuffer(float[] circular) {
		var result = new float[BufferSize];
		int firstPart = BufferSize - _writePosition;
		Array.Copy(circular, _writePosition, result, 0, firstPart);
		Array.Copy(circular, 0, result, firstPart, _writePosition);
		return result;
	}
}

/// <summary>
/// Provides spectrum analysis (FFT) for visualization.
/// </summary>
public class SpectrumAnalyzer {
	private readonly float[] _inputBuffer;
	private readonly float[] _window;
	private readonly float[] _magnitudes;
	private int _writePosition;
	private readonly object _lock = new();

	/// <summary>
	/// Creates a new spectrum analyzer.
	/// </summary>
	/// <param name="fftSize">FFT size (must be power of 2, default 1024).</param>
	public SpectrumAnalyzer(int fftSize = 1024) {
		if ((fftSize & (fftSize - 1)) != 0)
			throw new ArgumentException("FFT size must be power of 2", nameof(fftSize));

		FftSize = fftSize;
		_inputBuffer = new float[fftSize];
		_magnitudes = new float[fftSize / 2];

		// Hann window
		_window = new float[fftSize];
		for (int i = 0; i < fftSize; i++) {
			_window[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (fftSize - 1)));
		}
	}

	/// <summary>
	/// FFT size.
	/// </summary>
	public int FftSize { get; }

	/// <summary>
	/// Number of frequency bins.
	/// </summary>
	public int BinCount => FftSize / 2;

	/// <summary>
	/// Feeds audio samples to the analyzer.
	/// </summary>
	public void Feed(ReadOnlySpan<float> samples) {
		lock (_lock) {
			for (int i = 0; i < samples.Length; i++) {
				_inputBuffer[_writePosition] = samples[i];
				_writePosition = (_writePosition + 1) % FftSize;
			}
		}
	}

	/// <summary>
	/// Gets the current spectrum magnitudes (0-1 normalized).
	/// </summary>
	public float[] GetMagnitudes() {
		lock (_lock) {
			// Simple DFT (for small sizes) - replace with FFT for production
			var windowed = new float[FftSize];
			for (int i = 0; i < FftSize; i++) {
				int idx = (_writePosition + i) % FftSize;
				windowed[i] = _inputBuffer[idx] * _window[i];
			}

			// Compute magnitude for each bin
			for (int k = 0; k < FftSize / 2; k++) {
				float real = 0, imag = 0;

				// For performance, only compute a subset of bins
				// Full DFT is O(N²), too slow for real-time
				// This is a simplified version
				if (k < 32 || k % 4 == 0) {
					for (int n = 0; n < FftSize; n++) {
						float angle = 2f * MathF.PI * k * n / FftSize;
						real += windowed[n] * MathF.Cos(angle);
						imag -= windowed[n] * MathF.Sin(angle);
					}

					_magnitudes[k] = MathF.Sqrt((real * real) + (imag * imag)) / FftSize * 4f;
				}
			}

			// Interpolate skipped bins
			for (int k = 32; k < FftSize / 2; k++) {
				if (k % 4 != 0) {
					int prev = k / 4 * 4;
					int next = prev + 4;
					if (next < FftSize / 2) {
						float t = (k - prev) / 4f;
						_magnitudes[k] = (_magnitudes[prev] * (1 - t)) + (_magnitudes[next] * t);
					}
				}
			}

			return (float[])_magnitudes.Clone();
		}
	}

	/// <summary>
	/// Gets spectrum data grouped into bands (for bar visualization).
	/// </summary>
	/// <param name="bandCount">Number of frequency bands.</param>
	public float[] GetBands(int bandCount = 32) {
		lock (_lock) {
			var mags = GetMagnitudes();
			var bands = new float[bandCount];

			// Logarithmic band distribution
			for (int b = 0; b < bandCount; b++) {
				float lowFreq = 20f * MathF.Pow(1000f, (float)b / bandCount);
				float highFreq = 20f * MathF.Pow(1000f, (float)(b + 1) / bandCount);

				int lowBin = (int)(lowFreq * FftSize / 44100f);
				int highBin = (int)(highFreq * FftSize / 44100f);

				lowBin = Math.Clamp(lowBin, 0, (FftSize / 2) - 1);
				highBin = Math.Clamp(highBin, lowBin + 1, FftSize / 2);

				float sum = 0;
				int count = 0;
				for (int i = lowBin; i < highBin; i++) {
					sum += mags[i];
					count++;
				}

				bands[b] = count > 0 ? sum / count : 0;
			}

			return bands;
		}
	}

	/// <summary>
	/// Clears the analyzer buffer.
	/// </summary>
	public void Clear() {
		lock (_lock) {
			Array.Clear(_inputBuffer);
			Array.Clear(_magnitudes);
			_writePosition = 0;
		}
	}
}

/// <summary>
/// Provides BRR sample visualization data.
/// </summary>
public class SampleDisplay {
	/// <summary>
	/// Gets decoded PCM data from a BRR sample for visualization.
	/// </summary>
	/// <param name="brrData">BRR encoded sample data.</param>
	/// <returns>Decoded 16-bit PCM samples normalized to -1..1.</returns>
	public static float[] DecodeBrrForDisplay(byte[] brrData) {
		var decoded = BrrCodec.Decode(brrData);
		var result = new float[decoded.Length];

		for (int i = 0; i < decoded.Length; i++) {
			result[i] = decoded[i] / 32768f;
		}

		return result;
	}

	/// <summary>
	/// Gets downsampled sample data for waveform display.
	/// </summary>
	/// <param name="samples">Audio samples.</param>
	/// <param name="targetWidth">Target width in pixels.</param>
	public static (float min, float max)[] GetDownsampled(float[] samples, int targetWidth) {
		var result = new (float min, float max)[targetWidth];
		int samplesPerBin = Math.Max(1, samples.Length / targetWidth);

		for (int i = 0; i < targetWidth; i++) {
			int start = i * samplesPerBin;
			int end = Math.Min(start + samplesPerBin, samples.Length);

			float min = float.MaxValue;
			float max = float.MinValue;

			for (int j = start; j < end; j++) {
				float v = samples[j];
				if (v < min) min = v;
				if (v > max) max = v;
			}

			result[i] = (min == float.MaxValue ? 0 : min, max == float.MinValue ? 0 : max);
		}

		return result;
	}
}
