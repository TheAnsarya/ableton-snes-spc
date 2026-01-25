using SpcPlugin.Core.Audio;

namespace SpcPlugin.Core.Editing;

/// <summary>
/// Manages loop point editing for BRR samples with visual feedback.
/// Handles loop point validation, auto-detection, and crossfade smoothing.
/// </summary>
public sealed class LoopPointEditor {
	private short[]? _samples;
	private byte[]? _brrData;
	private int _sampleRate = 32000;

	/// <summary>BRR block size in samples.</summary>
	public const int SamplesPerBlock = 16;

	/// <summary>BRR block size in bytes.</summary>
	public const int BytesPerBlock = 9;

	/// <summary>Event raised when loop points change.</summary>
	public event EventHandler<LoopPointChangedEventArgs>? LoopPointChanged;

	/// <summary>Gets whether sample data is loaded.</summary>
	public bool HasSample => _samples != null && _samples.Length > 0;

	/// <summary>Gets the total length in samples.</summary>
	public int SampleCount => _samples?.Length ?? 0;

	/// <summary>Gets the total length in BRR blocks.</summary>
	public int BlockCount => (_samples?.Length ?? 0) / SamplesPerBlock;

	/// <summary>Gets the duration in seconds.</summary>
	public double Duration => _sampleRate > 0 ? (double)SampleCount / _sampleRate : 0;

	/// <summary>Gets or sets the loop start position in samples.</summary>
	public int LoopStart { get; private set; }

	/// <summary>Gets or sets the loop end position in samples.</summary>
	public int LoopEnd { get; private set; }

	/// <summary>Gets the loop start as a BRR block index.</summary>
	public int LoopStartBlock => LoopStart / SamplesPerBlock;

	/// <summary>Gets the loop end as a BRR block index.</summary>
	public int LoopEndBlock => LoopEnd / SamplesPerBlock;

	/// <summary>Gets whether the sample has a valid loop.</summary>
	public bool HasLoop => LoopStart >= 0 && LoopEnd > LoopStart;

	/// <summary>Gets the loop length in samples.</summary>
	public int LoopLength => HasLoop ? LoopEnd - LoopStart : 0;

	/// <summary>Gets the loop length in BRR blocks.</summary>
	public int LoopBlockCount => LoopLength / SamplesPerBlock;

	/// <summary>
	/// Loads sample data for loop editing.
	/// </summary>
	/// <param name="samples">PCM samples.</param>
	/// <param name="loopStart">Initial loop start (-1 for no loop).</param>
	/// <param name="sampleRate">Sample rate.</param>
	public void LoadSamples(short[] samples, int loopStart = -1, int sampleRate = 32000) {
		_samples = (short[])samples.Clone();
		_brrData = null;
		_sampleRate = sampleRate;

		// Align loop start to block boundary
		if (loopStart >= 0) {
			LoopStart = AlignToBlock(loopStart);
			LoopEnd = AlignToBlock(samples.Length);
		} else {
			LoopStart = -1;
			LoopEnd = -1;
		}
	}

	/// <summary>
	/// Loads BRR data for loop editing.
	/// </summary>
	/// <param name="brrData">BRR encoded data.</param>
	/// <param name="loopBlock">Loop point in BRR blocks (-1 for no loop).</param>
	public void LoadBrr(byte[] brrData, int loopBlock = -1) {
		_brrData = (byte[])brrData.Clone();
		_samples = BrrCodec.Decode(brrData);
		_sampleRate = 32000;

		if (loopBlock >= 0) {
			LoopStart = loopBlock * SamplesPerBlock;
			LoopEnd = _samples.Length;
		} else {
			LoopStart = -1;
			LoopEnd = -1;
		}
	}

	/// <summary>
	/// Sets the loop start position.
	/// </summary>
	/// <param name="samplePosition">Position in samples (will be aligned to block).</param>
	public void SetLoopStart(int samplePosition) {
		if (_samples == null) return;

		int oldStart = LoopStart;
		LoopStart = AlignToBlock(Math.Clamp(samplePosition, 0, _samples.Length - SamplesPerBlock));

		// Ensure loop end is after loop start
		if (LoopEnd <= LoopStart) {
			LoopEnd = AlignToBlock(_samples.Length);
		}

		if (LoopStart != oldStart) {
			OnLoopPointChanged(LoopPointType.Start, oldStart, LoopStart);
		}
	}

	/// <summary>
	/// Sets the loop end position.
	/// </summary>
	/// <param name="samplePosition">Position in samples (will be aligned to block).</param>
	public void SetLoopEnd(int samplePosition) {
		if (_samples == null) return;

		int oldEnd = LoopEnd;
		LoopEnd = AlignToBlock(Math.Clamp(samplePosition, SamplesPerBlock, _samples.Length));

		// Ensure loop end is after loop start
		if (LoopEnd <= LoopStart && LoopStart >= 0) {
			LoopEnd = LoopStart + SamplesPerBlock;
		}

		if (LoopEnd != oldEnd) {
			OnLoopPointChanged(LoopPointType.End, oldEnd, LoopEnd);
		}
	}

	/// <summary>
	/// Sets the loop start by block index.
	/// </summary>
	public void SetLoopStartBlock(int blockIndex) {
		SetLoopStart(blockIndex * SamplesPerBlock);
	}

	/// <summary>
	/// Sets the loop end by block index.
	/// </summary>
	public void SetLoopEndBlock(int blockIndex) {
		SetLoopEnd(blockIndex * SamplesPerBlock);
	}

	/// <summary>
	/// Enables looping at the specified block.
	/// </summary>
	public void EnableLoop(int loopStartBlock) {
		if (_samples == null) return;
		SetLoopStartBlock(loopStartBlock);
		LoopEnd = AlignToBlock(_samples.Length);
	}

	/// <summary>
	/// Disables looping.
	/// </summary>
	public void DisableLoop() {
		int oldStart = LoopStart;
		LoopStart = -1;
		LoopEnd = -1;
		OnLoopPointChanged(LoopPointType.Start, oldStart, -1);
	}

	/// <summary>
	/// Auto-detects the optimal loop point by finding the best zero-crossing match.
	/// </summary>
	/// <param name="minLoopLength">Minimum loop length in samples.</param>
	/// <returns>Detected loop start position, or -1 if not found.</returns>
	public int AutoDetectLoopPoint(int minLoopLength = 512) {
		if (_samples == null || _samples.Length < minLoopLength * 2) return -1;

		int bestMatch = -1;
		double bestError = double.MaxValue;
		int searchEnd = _samples.Length - minLoopLength;

		// Find zero-crossings near the end
		var endRegion = _samples.AsSpan(_samples.Length - 64);

		for (int start = minLoopLength; start < searchEnd; start += SamplesPerBlock) {
			var startRegion = _samples.AsSpan(start, Math.Min(64, _samples.Length - start));

			// Calculate match error
			double error = CalculateMatchError(startRegion, endRegion);
			if (error < bestError) {
				bestError = error;
				bestMatch = start;
			}
		}

		// Only accept if error is reasonably low
		if (bestMatch >= 0 && bestError < 5000) {
			SetLoopStart(bestMatch);
			return bestMatch;
		}

		return -1;
	}

	/// <summary>
	/// Gets a waveform visualization with loop points marked.
	/// </summary>
	/// <param name="width">Width in points.</param>
	/// <returns>Waveform data with loop markers.</returns>
	public LoopWaveformData GetWaveform(int width) {
		var result = new LoopWaveformData {
			Samples = new float[width],
			LoopStartX = -1,
			LoopEndX = -1,
		};

		if (_samples == null || _samples.Length == 0 || width <= 0) {
			return result;
		}

		// Generate waveform
		for (int i = 0; i < width; i++) {
			int start = (int)((long)i * _samples.Length / width);
			int end = (int)((long)(i + 1) * _samples.Length / width);
			if (end > _samples.Length) end = _samples.Length;

			float maxAbs = 0;
			for (int j = start; j < end; j++) {
				float abs = Math.Abs(_samples[j] / 32768f);
				if (abs > maxAbs) maxAbs = abs;
			}
			result.Samples[i] = maxAbs;
		}

		// Calculate loop marker positions
		if (HasLoop) {
			result.LoopStartX = (int)((long)LoopStart * width / _samples.Length);
			result.LoopEndX = (int)((long)LoopEnd * width / _samples.Length);
		}

		return result;
	}

	/// <summary>
	/// Applies a crossfade at the loop point to reduce clicking.
	/// </summary>
	/// <param name="crossfadeSamples">Number of samples to crossfade.</param>
	public void ApplyCrossfade(int crossfadeSamples = 32) {
		if (_samples == null || !HasLoop || crossfadeSamples <= 0) return;

		int fadeLength = Math.Min(crossfadeSamples, LoopLength / 2);
		int loopEndStart = LoopEnd - fadeLength;

		for (int i = 0; i < fadeLength; i++) {
			float fadeOut = 1.0f - ((float)i / fadeLength);
			float fadeIn = (float)i / fadeLength;

			int endIdx = loopEndStart + i;
			int startIdx = LoopStart + i;

			if (endIdx < _samples.Length && startIdx < _samples.Length) {
				_samples[endIdx] = (short)(_samples[endIdx] * fadeOut + _samples[startIdx] * fadeIn);
			}
		}
	}

	/// <summary>
	/// Gets the loop quality metric (lower is better, 0 = perfect).
	/// </summary>
	public double GetLoopQuality() {
		if (_samples == null || !HasLoop) return double.MaxValue;

		int testLength = Math.Min(32, LoopLength);
		var loopEndRegion = _samples.AsSpan(LoopEnd - testLength, testLength);
		var loopStartRegion = _samples.AsSpan(LoopStart, testLength);

		return CalculateMatchError(loopStartRegion, loopEndRegion);
	}

	/// <summary>
	/// Exports the current state as BRR data with proper loop flags.
	/// </summary>
	/// <returns>BRR encoded data.</returns>
	public byte[] ExportBrr() {
		if (_samples == null) return [];

		var options = new SampleImporter.ImportOptions {
			Normalize = false,
			ApplyFadeOut = !HasLoop,
			LoopStart = HasLoop ? LoopStart : -1,
			LoopEnd = HasLoop ? LoopEnd : -1,
		};

		var result = SampleImporter.ImportSamples(_samples, _sampleRate, options);
		return result.Success ? result.BrrData : [];
	}

	/// <summary>
	/// Gets the loaded sample data.
	/// </summary>
	public short[] GetSamples() {
		return _samples != null ? (short[])_samples.Clone() : [];
	}

	private static int AlignToBlock(int position) {
		return (position / SamplesPerBlock) * SamplesPerBlock;
	}

	private static double CalculateMatchError(ReadOnlySpan<short> a, ReadOnlySpan<short> b) {
		int length = Math.Min(a.Length, b.Length);
		double error = 0;

		for (int i = 0; i < length; i++) {
			double diff = a[i] - b[i];
			error += diff * diff;
		}

		return Math.Sqrt(error / length);
	}

	private void OnLoopPointChanged(LoopPointType type, int oldValue, int newValue) {
		LoopPointChanged?.Invoke(this, new LoopPointChangedEventArgs(type, oldValue, newValue));
	}
}

/// <summary>
/// Type of loop point.
/// </summary>
public enum LoopPointType {
	Start,
	End,
}

/// <summary>
/// Event args for loop point changes.
/// </summary>
public sealed class LoopPointChangedEventArgs : EventArgs {
	public LoopPointType PointType { get; }
	public int OldValue { get; }
	public int NewValue { get; }

	public LoopPointChangedEventArgs(LoopPointType type, int oldValue, int newValue) {
		PointType = type;
		OldValue = oldValue;
		NewValue = newValue;
	}
}

/// <summary>
/// Waveform data with loop point markers.
/// </summary>
public sealed class LoopWaveformData {
	/// <summary>Waveform peak values (0-1).</summary>
	public float[] Samples { get; set; } = [];

	/// <summary>Loop start X position (-1 if no loop).</summary>
	public int LoopStartX { get; set; }

	/// <summary>Loop end X position (-1 if no loop).</summary>
	public int LoopEndX { get; set; }

	/// <summary>Whether a loop is defined.</summary>
	public bool HasLoop => LoopStartX >= 0;
}
