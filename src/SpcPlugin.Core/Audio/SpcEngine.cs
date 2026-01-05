namespace SpcPlugin.Core.Audio;

using SpcPlugin.Core.Emulation;

/// <summary>
/// Main audio engine that coordinates SPC700 CPU and S-DSP emulation
/// to generate audio output.
/// </summary>
public sealed class SpcEngine : IDisposable {
	private readonly Spc700 _cpu;
	private readonly SDsp _dsp;
	private byte[] _ramBuffer;

	private readonly float[] _outputBuffer;
	private int _sampleRate;
	private bool _isPlaying;
	private bool _disposed;

	// Timing constants
	private const int NativeSampleRate = 32000;
	private const int CpuClocksPerSample = 32; // ~1.024 MHz / 32 KHz

	/// <summary>
	/// Gets whether the engine is currently playing.
	/// </summary>
	public bool IsPlaying => _isPlaying;

	/// <summary>
	/// Gets or sets the output sample rate (for resampling).
	/// </summary>
	public int SampleRate {
		get => _sampleRate;
		set => _sampleRate = value > 0 ? value : NativeSampleRate;
	}

	/// <summary>
	/// Creates a new SPC engine instance.
	/// </summary>
	/// <param name="sampleRate">Output sample rate (default 44100).</param>
	public SpcEngine(int sampleRate = 44100) {
		_cpu = new Spc700();
		_dsp = new SDsp();
		_ramBuffer = new byte[0x10000];
		_outputBuffer = new float[8192];
		_sampleRate = sampleRate;

		// Connect CPU and DSP
		_cpu.Dsp = _dsp;
		_dsp.Ram = _ramBuffer;
	}

	/// <summary>
	/// Loads an SPC file into the engine.
	/// </summary>
	/// <param name="spcData">Raw SPC file data.</param>
	public void LoadSpc(ReadOnlySpan<byte> spcData) {
		if (spcData.Length < 0x10200) {
			throw new ArgumentException("Invalid SPC file - too small", nameof(spcData));
		}

		// Validate SPC header magic
		if (spcData[0] != 'S' || spcData[1] != 'N' || spcData[2] != 'E' || spcData[3] != 'S') {
			throw new ArgumentException("Invalid SPC file - bad header", nameof(spcData));
		}

		_cpu.Reset();
		_cpu.LoadSpc(spcData);
		_dsp.LoadFromSpc(spcData.Slice(0x10100, 128));

		// Copy RAM to shared buffer so DSP can read samples
		_cpu.Ram.CopyTo(_ramBuffer);
		_dsp.Ram = _ramBuffer;
	}

	/// <summary>
	/// Loads an SPC file from disk.
	/// </summary>
	/// <param name="path">Path to SPC file.</param>
	public void LoadSpcFile(string path) {
		byte[] data = File.ReadAllBytes(path);
		LoadSpc(data);
	}

	/// <summary>
	/// Starts playback.
	/// </summary>
	public void Play() {
		_isPlaying = true;
	}

	/// <summary>
	/// Pauses playback.
	/// </summary>
	public void Pause() {
		_isPlaying = false;
	}

	/// <summary>
	/// Stops playback and resets to beginning.
	/// </summary>
	public void Stop() {
		_isPlaying = false;
		_cpu.Reset();
	}

	/// <summary>
	/// Generates audio samples into the provided buffer.
	/// Called by the audio system (VST host) to fill buffers.
	/// </summary>
	/// <param name="output">Interleaved stereo output (L, R, L, R, ...).</param>
	/// <param name="sampleCount">Number of stereo sample pairs to generate.</param>
	public void Process(Span<float> output, int sampleCount) {
		if (!_isPlaying) {
			output[..(sampleCount * 2)].Clear();
			return;
		}

		// Calculate native samples needed (with resampling ratio)
		double ratio = (double)NativeSampleRate / _sampleRate;
		int nativeSamples = (int)Math.Ceiling(sampleCount * ratio);

		// Generate at native rate
		Span<float> nativeBuffer = _outputBuffer.AsSpan(0, nativeSamples * 2);
		GenerateNative(nativeBuffer, nativeSamples);

		// Resample to output rate
		Resample(nativeBuffer, output, nativeSamples, sampleCount);
	}

	private void GenerateNative(Span<float> output, int sampleCount) {
		// Run CPU and generate DSP output
		for (int i = 0; i < sampleCount; i++) {
			// Execute CPU cycles for one sample period
			_cpu.Execute(CpuClocksPerSample);
		}

		// Generate DSP audio
		_dsp.GenerateSamples(output, sampleCount);
	}

	private static void Resample(
		ReadOnlySpan<float> input, Span<float> output,
		int inputCount, int outputCount) {
		// Simple linear interpolation resampling
		double ratio = (double)inputCount / outputCount;

		for (int i = 0; i < outputCount; i++) {
			double srcPos = i * ratio;
			int srcIndex = (int)srcPos;
			double frac = srcPos - srcIndex;

			int idx1 = Math.Min(srcIndex, inputCount - 1) * 2;
			int idx2 = Math.Min(srcIndex + 1, inputCount - 1) * 2;

			// Interpolate left channel
			output[i * 2] = (float)(
				input[idx1] * (1 - frac) +
				input[idx2] * frac);

			// Interpolate right channel
			output[i * 2 + 1] = (float)(
				input[idx1 + 1] * (1 - frac) +
				input[idx2 + 1] * frac);
		}
	}

	/// <summary>
	/// Disposes engine resources.
	/// </summary>
	public void Dispose() {
		if (_disposed) return;
		_disposed = true;
		_isPlaying = false;
	}
}
