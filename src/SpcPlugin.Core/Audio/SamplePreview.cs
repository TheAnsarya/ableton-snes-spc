namespace SpcPlugin.Core.Audio;

/// <summary>
/// Simple sample preview player for audition purposes.
/// Plays decoded BRR samples or raw PCM for preview before editing.
/// </summary>
public sealed class SamplePreview : IDisposable {
	private short[]? _samples;
	private int _position;
	private int _sampleRate;
	private bool _isPlaying;
	private bool _loop;
	private int _loopStart;
	private int _loopEnd;
	private float _volume = 1.0f;
	private float _pitch = 1.0f;
	private readonly object _lock = new();
	private bool _disposed;

	/// <summary>
	/// Default sample rate for preview playback.
	/// </summary>
	public const int DefaultSampleRate = 32000;

	/// <summary>
	/// Event raised when playback state changes.
	/// </summary>
	public event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChanged;

	/// <summary>
	/// Gets whether playback is active.
	/// </summary>
	public bool IsPlaying {
		get {
			lock (_lock) { return _isPlaying; }
		}
	}

	/// <summary>
	/// Gets the current playback position in samples.
	/// </summary>
	public int Position {
		get {
			lock (_lock) { return _position; }
		}
	}

	/// <summary>
	/// Gets the total length in samples.
	/// </summary>
	public int Length {
		get {
			lock (_lock) { return _samples?.Length ?? 0; }
		}
	}

	/// <summary>
	/// Gets the playback progress (0.0 - 1.0).
	/// </summary>
	public float Progress {
		get {
			lock (_lock) {
				if (_samples == null || _samples.Length == 0) return 0;
				return (float)_position / _samples.Length;
			}
		}
	}

	/// <summary>
	/// Gets or sets whether looping is enabled.
	/// </summary>
	public bool Loop {
		get {
			lock (_lock) { return _loop; }
		}
		set {
			lock (_lock) { _loop = value; }
		}
	}

	/// <summary>
	/// Gets or sets the loop start position in samples.
	/// </summary>
	public int LoopStart {
		get {
			lock (_lock) { return _loopStart; }
		}
		set {
			lock (_lock) { _loopStart = Math.Max(0, value); }
		}
	}

	/// <summary>
	/// Gets or sets the loop end position in samples (-1 = end of sample).
	/// </summary>
	public int LoopEnd {
		get {
			lock (_lock) { return _loopEnd; }
		}
		set {
			lock (_lock) { _loopEnd = value; }
		}
	}

	/// <summary>
	/// Gets or sets the playback volume (0.0 - 1.0).
	/// </summary>
	public float Volume {
		get {
			lock (_lock) { return _volume; }
		}
		set {
			lock (_lock) { _volume = Math.Clamp(value, 0, 1); }
		}
	}

	/// <summary>
	/// Gets or sets the playback pitch multiplier (1.0 = normal).
	/// </summary>
	public float Pitch {
		get {
			lock (_lock) { return _pitch; }
		}
		set {
			lock (_lock) { _pitch = Math.Clamp(value, 0.1f, 4.0f); }
		}
	}

	/// <summary>
	/// Gets or sets the sample rate for playback.
	/// </summary>
	public int SampleRate {
		get {
			lock (_lock) { return _sampleRate; }
		}
		set {
			lock (_lock) { _sampleRate = value > 0 ? value : DefaultSampleRate; }
		}
	}

	/// <summary>
	/// Creates a new sample preview player.
	/// </summary>
	public SamplePreview() {
		_sampleRate = DefaultSampleRate;
	}

	/// <summary>
	/// Loads BRR data for preview.
	/// </summary>
	/// <param name="brrData">BRR encoded data.</param>
	/// <param name="loopBlock">Loop point in BRR blocks (-1 = no loop).</param>
	public void LoadBrr(byte[] brrData, int loopBlock = -1) {
		var samples = BrrCodec.Decode(brrData);
		LoadSamples(samples, loopBlock >= 0 ? loopBlock * 16 : -1);
	}

	/// <summary>
	/// Loads raw PCM samples for preview.
	/// </summary>
	/// <param name="samples">16-bit PCM samples.</param>
	/// <param name="loopStart">Loop start position (-1 = no loop).</param>
	public void LoadSamples(short[] samples, int loopStart = -1) {
		lock (_lock) {
			Stop();
			_samples = (short[])samples.Clone();
			_position = 0;
			_loopStart = loopStart >= 0 ? loopStart : 0;
			_loopEnd = -1;
			_loop = loopStart >= 0;
		}
	}

	/// <summary>
	/// Loads a WAV file for preview.
	/// </summary>
	/// <param name="wavPath">Path to WAV file.</param>
	public void LoadWav(string wavPath) {
		var wav = WavFile.Load(wavPath);
		LoadSamples(wav.MonoSamples);
		_sampleRate = wav.SampleRate;
	}

	/// <summary>
	/// Starts playback from current position.
	/// </summary>
	public void Play() {
		lock (_lock) {
			if (_samples == null || _samples.Length == 0) return;
			bool wasPlaying = _isPlaying;
			_isPlaying = true;
			if (!wasPlaying) {
				OnPlaybackStateChanged(true);
			}
		}
	}

	/// <summary>
	/// Starts playback from the beginning.
	/// </summary>
	public void PlayFromStart() {
		lock (_lock) {
			_position = 0;
		}
		Play();
	}

	/// <summary>
	/// Stops playback.
	/// </summary>
	public void Stop() {
		lock (_lock) {
			bool wasPlaying = _isPlaying;
			_isPlaying = false;
			if (wasPlaying) {
				OnPlaybackStateChanged(false);
			}
		}
	}

	/// <summary>
	/// Pauses playback at current position.
	/// </summary>
	public void Pause() => Stop();

	/// <summary>
	/// Seeks to a specific position.
	/// </summary>
	/// <param name="position">Position in samples.</param>
	public void Seek(int position) {
		lock (_lock) {
			if (_samples == null) return;
			_position = Math.Clamp(position, 0, _samples.Length);
		}
	}

	/// <summary>
	/// Seeks to a normalized position (0.0 - 1.0).
	/// </summary>
	/// <param name="progress">Progress value.</param>
	public void SeekNormalized(float progress) {
		lock (_lock) {
			if (_samples == null) return;
			_position = (int)(Math.Clamp(progress, 0, 1) * _samples.Length);
		}
	}

	/// <summary>
	/// Generates audio output for the given buffer.
	/// Call this from your audio callback to get preview audio.
	/// </summary>
	/// <param name="outputLeft">Left channel output buffer.</param>
	/// <param name="outputRight">Right channel output buffer.</param>
	/// <param name="numSamples">Number of samples to generate.</param>
	/// <param name="outputSampleRate">Output sample rate.</param>
	public void Process(float[] outputLeft, float[] outputRight, int numSamples, int outputSampleRate) {
		lock (_lock) {
			if (!_isPlaying || _samples == null || _samples.Length == 0) {
				// Fill with silence
				Array.Clear(outputLeft, 0, numSamples);
				Array.Clear(outputRight, 0, numSamples);
				return;
			}

			double ratio = (double)_sampleRate * _pitch / outputSampleRate;
			int loopEnd = _loopEnd > 0 ? _loopEnd : _samples.Length;

			for (int i = 0; i < numSamples; i++) {
				if (_position >= _samples.Length) {
					if (_loop && _loopStart < loopEnd) {
						_position = _loopStart;
					} else {
						_isPlaying = false;
						Array.Clear(outputLeft, i, numSamples - i);
						Array.Clear(outputRight, i, numSamples - i);
						OnPlaybackStateChanged(false);
						return;
					}
				}

				// Linear interpolation for pitch shifting
				int idx = (int)(_position * ratio) % _samples.Length;
				float sample = _samples[idx] / 32768f * _volume;

				outputLeft[i] = sample;
				outputRight[i] = sample;

				_position++;

				// Check for loop point
				if (_loop && _position >= loopEnd && _loopStart < loopEnd) {
					_position = _loopStart;
				}
			}
		}
	}

	/// <summary>
	/// Generates mono audio output.
	/// </summary>
	/// <param name="output">Output buffer.</param>
	/// <param name="numSamples">Number of samples.</param>
	/// <param name="outputSampleRate">Output sample rate.</param>
	public void ProcessMono(float[] output, int numSamples, int outputSampleRate) {
		var temp = new float[numSamples];
		Process(output, temp, numSamples, outputSampleRate);
	}

	/// <summary>
	/// Gets a waveform visualization of the loaded sample.
	/// </summary>
	/// <param name="width">Number of points in the waveform.</param>
	/// <returns>Array of peak values (-1 to 1).</returns>
	public float[] GetWaveform(int width) {
		lock (_lock) {
			if (_samples == null || _samples.Length == 0 || width <= 0) {
				return new float[width];
			}

			var waveform = new float[width];
			int samplesPerPoint = _samples.Length / width;
			if (samplesPerPoint < 1) samplesPerPoint = 1;

			for (int i = 0; i < width; i++) {
				int start = (int)((long)i * _samples.Length / width);
				int end = (int)((long)(i + 1) * _samples.Length / width);
				if (end > _samples.Length) end = _samples.Length;

				float maxAbs = 0;
				for (int j = start; j < end; j++) {
					float abs = Math.Abs(_samples[j] / 32768f);
					if (abs > maxAbs) maxAbs = abs;
				}
				waveform[i] = maxAbs;
			}

			return waveform;
		}
	}

	/// <summary>
	/// Gets the loaded sample data.
	/// </summary>
	/// <returns>Copy of the sample data, or empty array if none loaded.</returns>
	public short[] GetSamples() {
		lock (_lock) {
			return _samples != null ? (short[])_samples.Clone() : [];
		}
	}

	private void OnPlaybackStateChanged(bool isPlaying) {
		PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(isPlaying));
	}

	public void Dispose() {
		if (_disposed) return;
		_disposed = true;
		Stop();
		lock (_lock) {
			_samples = null;
		}
	}
}

/// <summary>
/// Event arguments for playback state changes.
/// </summary>
public sealed class PlaybackStateChangedEventArgs : EventArgs {
	/// <summary>Whether playback is now active.</summary>
	public bool IsPlaying { get; }

	public PlaybackStateChangedEventArgs(bool isPlaying) {
		IsPlaying = isPlaying;
	}
}
