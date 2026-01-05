
using SpcPlugin.Core.Editing;
using SpcPlugin.Core.Emulation;

namespace SpcPlugin.Core.Audio;
/// <summary>
/// Main audio engine that coordinates SPC700 CPU and S-DSP emulation
/// to generate audio output. Designed for use as a VST3 instrument in DAWs like Ableton Live.
/// </summary>
public sealed class SpcEngine : IDisposable {
	private readonly Spc700 _cpu;
	private readonly SDsp _dsp;
	private byte[] _ramBuffer;

	private readonly float[] _outputBuffer;
	private int _sampleRate;
	private bool _disposed;

	// Voice control for Ableton automation
	private readonly bool[] _voiceMuted = new bool[8];
	private readonly bool[] _voiceSolo = new bool[8];
	private readonly float[] _voiceVolume = [1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f];

	// Master controls

	// Position tracking for DAW sync (TODO: implement SetLoopPoints for DAW integration)
#pragma warning disable CS0169 // Field is never used - reserved for future DAW loop sync
	private long _loopStartSample;
	private long _loopEndSample;
#pragma warning restore CS0169

	// Tempo sync (BPM from DAW)
	private double _hostTimeSignatureDenominator = 4;

	// Timing constants
	private const int NativeSampleRate = 32000;
	private const int CpuClocksPerSample = 32; // ~1.024 MHz / 32 KHz

	/// <summary>
	/// Gets whether the engine is currently playing.
	/// </summary>
	public bool IsPlaying { get; private set; }

	/// <summary>
	/// Gets or sets the output sample rate (for resampling).
	/// </summary>
	public int SampleRate {
		get => _sampleRate;
		set => _sampleRate = value > 0 ? value : NativeSampleRate;
	}

	/// <summary>
	/// Gets or sets the master volume (0.0 - 1.0).
	/// </summary>
	public float MasterVolume { get; set => field = Math.Clamp(value, 0f, 2f); } = 1.0f;

	/// <summary>
	/// Gets or sets whether looping is enabled.
	/// </summary>
	public bool LoopEnabled { get; set; } = true;

	/// <summary>
	/// Gets the current playback position in samples.
	/// </summary>
	public long Position { get; private set; }

	/// <summary>
	/// Gets the current playback position in seconds.
	/// </summary>
	public double PositionSeconds => (double)Position / _sampleRate;

	/// <summary>
	/// Gets the editor for modifying the loaded SPC.
	/// </summary>
	public SpcEditor Editor { get; }

	/// <summary>
	/// Gets the total CPU cycles executed.
	/// </summary>
	public long TotalCycles => _cpu.TotalCycles;

	/// <summary>
	/// Creates a new SPC engine instance.
	/// </summary>
	/// <param name="sampleRate">Output sample rate (default 44100).</param>
	public SpcEngine(int sampleRate = 44100) {
		_cpu = new Spc700();
		_dsp = new SDsp();
		Editor = new SpcEditor();
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
		IsPlaying = true;
	}

	/// <summary>
	/// Pauses playback.
	/// </summary>
	public void Pause() {
		IsPlaying = false;
	}

	/// <summary>
	/// Stops playback and resets to beginning.
	/// </summary>
	public void Stop() {
		IsPlaying = false;
		_cpu.Reset();
		Position = 0;
	}

	/// <summary>
	/// Seeks to a specific position (resets and re-emulates to reach position).
	/// Note: This is expensive for long seeks.
	/// </summary>
	public void Seek(double seconds) {
		_cpu.Reset();
		Position = 0;

		int targetSamples = (int)(seconds * NativeSampleRate);
		var tempBuffer = new float[1024];

		while (Position < targetSamples) {
			int samplesToGenerate = Math.Min(512, (int)(targetSamples - Position));
			GenerateNative(tempBuffer.AsSpan(0, samplesToGenerate * 2), samplesToGenerate);
			Position += samplesToGenerate;
		}
	}

	#region Voice Control (Ableton Automation)

	/// <summary>
	/// Gets or sets whether a voice is muted.
	/// </summary>
	public bool GetVoiceMuted(int voice) {
		ValidateVoice(voice);
		return _voiceMuted[voice];
	}

	public void SetVoiceMuted(int voice, bool muted) {
		ValidateVoice(voice);
		_voiceMuted[voice] = muted;
	}

	/// <summary>
	/// Gets or sets whether a voice is soloed.
	/// </summary>
	public bool GetVoiceSolo(int voice) {
		ValidateVoice(voice);
		return _voiceSolo[voice];
	}

	public void SetVoiceSolo(int voice, bool solo) {
		ValidateVoice(voice);
		_voiceSolo[voice] = solo;
	}

	/// <summary>
	/// Gets or sets individual voice volume (0.0 - 1.0).
	/// </summary>
	public float GetVoiceVolume(int voice) {
		ValidateVoice(voice);
		return _voiceVolume[voice];
	}

	public void SetVoiceVolume(int voice, float volume) {
		ValidateVoice(voice);
		_voiceVolume[voice] = Math.Clamp(volume, 0f, 2f);
	}

	/// <summary>
	/// Mutes all voices.
	/// </summary>
	public void MuteAll() {
		for (int i = 0; i < 8; i++) _voiceMuted[i] = true;
	}

	/// <summary>
	/// Unmutes all voices.
	/// </summary>
	public void UnmuteAll() {
		for (int i = 0; i < 8; i++) _voiceMuted[i] = false;
	}

	/// <summary>
	/// Clears all solo states.
	/// </summary>
	public void ClearSolo() {
		for (int i = 0; i < 8; i++) _voiceSolo[i] = false;
	}

	/// <summary>
	/// Gets the effective voice enable mask based on mute/solo state.
	/// </summary>
	private byte GetEffectiveVoiceMask() {
		bool anySolo = false;
		for (int i = 0; i < 8; i++) {
			if (_voiceSolo[i]) { anySolo = true; break; }
		}

		byte mask = 0;
		for (int i = 0; i < 8; i++) {
			bool enabled = anySolo ? _voiceSolo[i] && !_voiceMuted[i] : !_voiceMuted[i];
			if (enabled) mask |= (byte)(1 << i);
		}

		return mask;
	}

	private static void ValidateVoice(int voice) {
		if (voice < 0 || voice >= 8)
			throw new ArgumentOutOfRangeException(nameof(voice), "Voice must be 0-7");
	}

	#endregion

	#region DAW Sync (Ableton Integration)

	/// <summary>
	/// Sets the host tempo for tempo-sync features.
	/// </summary>
	public void SetHostTempo(double bpm) {
		PositionBeats = Math.Clamp(bpm, 20, 999);
	}

	/// <summary>
	/// Sets the host time signature.
	/// </summary>
	public void SetTimeSignature(double numerator, double denominator) {
		PositionBars = numerator;
		_hostTimeSignatureDenominator = denominator;
	}

	/// <summary>
	/// Syncs playback position to host transport.
	/// </summary>
	public void SyncToHostPosition(double positionSeconds) {
		// For tempo-synced playback, adjust position based on host
		// This is useful for quantized loop points
	}

	/// <summary>
	/// Gets the current position in beats (based on host tempo).
	/// </summary>
	public double PositionBeats { get => PositionSeconds * (field / 60.0); private set; } = 120.0;

	/// <summary>
	/// Gets the current position in bars.
	/// </summary>
	public double PositionBars { get => PositionBeats / field; private set; } = 4;

	#endregion

	/// <summary>
	/// Generates audio samples into the provided buffer.
	/// Called by the audio system (VST host) to fill buffers.
	/// </summary>
	/// <param name="output">Interleaved stereo output (L, R, L, R, ...).</param>
	/// <param name="sampleCount">Number of stereo sample pairs to generate.</param>
	public void Process(Span<float> output, int sampleCount) {
		if (!IsPlaying) {
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

		// Apply master volume
		for (int i = 0; i < sampleCount * 2; i++) {
			output[i] *= MasterVolume;
		}

		Position += sampleCount;
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
				(input[idx1] * (1 - frac)) +
				(input[idx2] * frac));

			// Interpolate right channel
			output[(i * 2) + 1] = (float)(
				(input[idx1 + 1] * (1 - frac)) +
				(input[idx2 + 1] * frac));
		}
	}

	/// <summary>
	/// Disposes engine resources.
	/// </summary>
	public void Dispose() {
		if (_disposed) return;
		_disposed = true;
		IsPlaying = false;
	}
}
