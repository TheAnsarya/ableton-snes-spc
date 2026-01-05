using SpcPlugin.Core.Audio;
using SpcPlugin.Core.Editing;

namespace SpcPlugin.Core.RealTime;

/// <summary>
/// Provides real-time BRR sample editing capabilities.
/// Allows modifying samples while the SPC engine is playing.
/// </summary>
public class RealtimeSampleEditor {
	private readonly SpcEngine _engine;
	private readonly object _editLock = new();

	// Sample cache for undo
	private readonly Dictionary<int, byte[]> _originalSamples = [];

	public RealtimeSampleEditor(SpcEngine engine) {
		_engine = engine;
	}

	/// <summary>
	/// Gets the SpcEditor for direct access.
	/// </summary>
	public SpcEditor? Editor => _engine.Editor;

	/// <summary>
	/// Replaces a sample with new PCM data in real-time.
	/// </summary>
	/// <param name="sourceNumber">Sample/source number (0-255).</param>
	/// <param name="pcmSamples">New 16-bit PCM samples.</param>
	/// <param name="loop">Whether the sample should loop.</param>
	/// <param name="loopPoint">Loop start point in samples.</param>
	public void ReplaceSample(int sourceNumber, ReadOnlySpan<short> pcmSamples, bool loop = false, int loopPoint = 0) {
		if (Editor == null) return;

		lock (_editLock) {
			// Save original if not already saved
			if (!_originalSamples.ContainsKey(sourceNumber)) {
				var info = Editor.GetSampleInfo(sourceNumber);
				if (info.StartAddress > 0) {
					// Estimate size and save
					int estimatedSize = ((pcmSamples.Length / 16) + 1) * 9;
					_originalSamples[sourceNumber] = Editor.Ram.Slice(info.StartAddress, Math.Min(estimatedSize, 0x10000 - info.StartAddress)).ToArray();
				}
			}

			// Get current sample address
			var currentInfo = Editor.GetSampleInfo(sourceNumber);
			int address = currentInfo.StartAddress;

			// Find suitable address if current is 0
			if (address == 0) {
				address = FindFreeRamSpace(pcmSamples.Length);
				if (address == 0) return; // No space
			}

			// Encode and write BRR data
			int brrSize = Editor.EncodeBrr(pcmSamples, address, loop, loopPoint);

			// Update sample directory
			int loopAddress = loop ? address + (loopPoint / 16 * 9) : address;
			Editor.SetSampleInfo(sourceNumber, (ushort)address, (ushort)loopAddress);
		}
	}

	/// <summary>
	/// Restores a sample to its original data.
	/// </summary>
	public void RestoreSample(int sourceNumber) {
		if (Editor == null) return;

		lock (_editLock) {
			if (_originalSamples.TryGetValue(sourceNumber, out var original)) {
				var info = Editor.GetSampleInfo(sourceNumber);
				original.CopyTo(Editor.Ram.Slice(info.StartAddress));
				_originalSamples.Remove(sourceNumber);
			}
		}
	}

	/// <summary>
	/// Adjusts sample pitch in real-time.
	/// </summary>
	public void SetSamplePitch(int voice, float pitchMultiplier) {
		if (Editor == null) return;

		lock (_editLock) {
			// Get current pitch
			var info = Editor.GetVoiceInfo(voice);
			int basePitch = info.Pitch;

			// Apply multiplier
			int newPitch = (int)(basePitch * pitchMultiplier);
			newPitch = Math.Clamp(newPitch, 0, 0x3FFF);

			Editor.SetVoicePitch(voice, (ushort)newPitch);
		}
	}

	/// <summary>
	/// Sets sample volume for a voice.
	/// </summary>
	public void SetSampleVolume(int voice, float left, float right) {
		if (Editor == null) return;

		lock (_editLock) {
			sbyte l = (sbyte)(Math.Clamp(left, -1f, 1f) * 127);
			sbyte r = (sbyte)(Math.Clamp(right, -1f, 1f) * 127);
			Editor.SetVoiceVolume(voice, l, r);
		}
	}

	/// <summary>
	/// Sets ADSR envelope for a voice.
	/// </summary>
	public void SetEnvelope(int voice, int attack, int decay, int sustain, int release) {
		if (Editor == null) return;

		lock (_editLock) {
			Editor.SetVoiceAdsr(voice, attack, decay, sustain, release);
		}
	}

	/// <summary>
	/// Triggers a sample on a specific voice.
	/// </summary>
	public void TriggerSample(int voice, int sourceNumber) {
		if (Editor == null) return;

		lock (_editLock) {
			Editor.SetVoiceSource(voice, (byte)sourceNumber);
			Editor.KeyOnVoice(voice);
		}
	}

	/// <summary>
	/// Stops a voice.
	/// </summary>
	public void StopVoice(int voice) {
		if (Editor == null) return;

		lock (_editLock) {
			Editor.KeyOffVoice(voice);
		}
	}

	/// <summary>
	/// Gets the decoded PCM samples for a source.
	/// </summary>
	public short[] GetSamplePcm(int sourceNumber) {
		if (Editor == null) return [];

		lock (_editLock) {
			return Editor.ExtractSample(sourceNumber);
		}
	}

	/// <summary>
	/// Imports a WAV file as a sample.
	/// </summary>
	public bool ImportWavAsSample(int sourceNumber, string wavPath, bool loop = false, int loopPoint = 0) {
		if (!File.Exists(wavPath)) return false;

		try {
			var samples = ReadWavFile(wavPath);
			if (samples.Length == 0) return false;

			ReplaceSample(sourceNumber, samples, loop, loopPoint);
			return true;
		} catch {
			return false;
		}
	}

	/// <summary>
	/// Applies a simple filter to a sample in RAM.
	/// </summary>
	public void ApplyFilter(int sourceNumber, SampleFilter filter) {
		if (Editor == null) return;

		lock (_editLock) {
			// Extract current sample
			var samples = Editor.ExtractSample(sourceNumber);
			if (samples.Length == 0) return;

			// Apply filter
			var filtered = filter switch {
				SampleFilter.LowPass => ApplyLowPassFilter(samples),
				SampleFilter.HighPass => ApplyHighPassFilter(samples),
				SampleFilter.Boost => ApplyBoost(samples, 1.5f),
				SampleFilter.Attenuate => ApplyBoost(samples, 0.5f),
				SampleFilter.Reverse => ApplyReverse(samples),
				SampleFilter.Normalize => ApplyNormalize(samples),
				_ => samples
			};

			// Write back
			var info = Editor.GetSampleInfo(sourceNumber);
			Editor.EncodeBrr(filtered, info.StartAddress, info.HasLoop, 0);
		}
	}

	/// <summary>
	/// Gets information about all samples in use.
	/// </summary>
	public List<SampleUsageInfo> GetSampleUsage() {
		var result = new List<SampleUsageInfo>();
		if (Editor == null) return result;

		lock (_editLock) {
			var usedSources = new HashSet<int>();

			// Find which sources are used by voices
			for (int v = 0; v < 8; v++) {
				var info = Editor.GetVoiceInfo(v);
				usedSources.Add(info.SourceNumber);
			}

			// Get info for each used source
			foreach (int source in usedSources) {
				var sampleInfo = Editor.GetSampleInfo(source);
				if (sampleInfo.StartAddress > 0) {
					result.Add(new SampleUsageInfo {
						SourceNumber = source,
						StartAddress = sampleInfo.StartAddress,
						LoopAddress = sampleInfo.LoopAddress,
						HasLoop = sampleInfo.HasLoop,
						IsModified = _originalSamples.ContainsKey(source)
					});
				}
			}
		}

		return result;
	}

	#region Private Helpers

	private int FindFreeRamSpace(int samplesNeeded) {
		// Estimate BRR size needed
		int brrSize = (samplesNeeded + 15) / 16 * 9;

		// Look for free space after echo buffer
		if (Editor == null) return 0;

		int echoStart = Editor.DspRegisters[0x6d] << 8;
		int echoSize = (Editor.DspRegisters[0x7d] & 0x0f) * 2048;
		int searchStart = echoStart + echoSize;

		// Simple allocation: find space at end of used area
		// This is a naive approach - production code would need proper memory management
		if (searchStart + brrSize < 0xFFC0) { // Leave room for IPL
			return searchStart;
		}

		return 0;
	}

	private static short[] ReadWavFile(string path) {
		using var stream = File.OpenRead(path);
		using var reader = new BinaryReader(stream);

		// Read RIFF header
		if (new string(reader.ReadChars(4)) != "RIFF") return [];
		reader.ReadInt32(); // File size
		if (new string(reader.ReadChars(4)) != "WAVE") return [];

		// Find fmt chunk
		int sampleRate = 44100;
		int channels = 1;
		int bitsPerSample = 16;

		while (stream.Position < stream.Length - 8) {
			string chunkId = new(reader.ReadChars(4));
			int chunkSize = reader.ReadInt32();

			if (chunkId == "fmt ") {
				reader.ReadInt16(); // Audio format
				channels = reader.ReadInt16();
				sampleRate = reader.ReadInt32();
				reader.ReadInt32(); // Byte rate
				reader.ReadInt16(); // Block align
				bitsPerSample = reader.ReadInt16();
				if (chunkSize > 16) reader.ReadBytes(chunkSize - 16);
			} else if (chunkId == "data") {
				int sampleCount = chunkSize / (bitsPerSample / 8) / channels;
				var samples = new short[sampleCount];

				for (int i = 0; i < sampleCount; i++) {
					if (bitsPerSample == 16) {
						int sum = 0;
						for (int c = 0; c < channels; c++) {
							sum += reader.ReadInt16();
						}

						samples[i] = (short)(sum / channels);
					} else if (bitsPerSample == 8) {
						int sum = 0;
						for (int c = 0; c < channels; c++) {
							sum += (reader.ReadByte() - 128) * 256;
						}

						samples[i] = (short)(sum / channels);
					}
				}

				// Resample to 32kHz if needed
				if (sampleRate != 32000) {
					samples = Resample(samples, sampleRate, 32000);
				}

				return samples;
			} else {
				reader.ReadBytes(chunkSize);
			}
		}

		return [];
	}

	private static short[] Resample(short[] samples, int fromRate, int toRate) {
		double ratio = (double)fromRate / toRate;
		int newLength = (int)(samples.Length / ratio);
		var result = new short[newLength];

		for (int i = 0; i < newLength; i++) {
			double srcPos = i * ratio;
			int srcIndex = (int)srcPos;
			double frac = srcPos - srcIndex;

			result[i] = srcIndex + 1 < samples.Length ? (short)((samples[srcIndex] * (1 - frac)) + (samples[srcIndex + 1] * frac)) : samples[^1];
		}

		return result;
	}

	private static short[] ApplyLowPassFilter(short[] samples) {
		var result = new short[samples.Length];
		int prev = 0;

		for (int i = 0; i < samples.Length; i++) {
			int current = samples[i];
			result[i] = (short)((current + prev) / 2);
			prev = current;
		}

		return result;
	}

	private static short[] ApplyHighPassFilter(short[] samples) {
		var result = new short[samples.Length];
		int prev = 0;

		for (int i = 0; i < samples.Length; i++) {
			int current = samples[i];
			result[i] = (short)Math.Clamp(current - prev, short.MinValue, short.MaxValue);
			prev = current;
		}

		return result;
	}

	private static short[] ApplyBoost(short[] samples, float factor) {
		var result = new short[samples.Length];

		for (int i = 0; i < samples.Length; i++) {
			result[i] = (short)Math.Clamp((int)(samples[i] * factor), short.MinValue, short.MaxValue);
		}

		return result;
	}

	private static short[] ApplyReverse(short[] samples) {
		var result = new short[samples.Length];
		Array.Copy(samples, result, samples.Length);
		Array.Reverse(result);
		return result;
	}

	private static short[] ApplyNormalize(short[] samples) {
		if (samples.Length == 0) return samples;

		int max = 1;
		foreach (var s in samples) {
			int abs = Math.Abs(s);
			if (abs > max) max = abs;
		}

		float factor = 32767f / max;
		return ApplyBoost(samples, factor);
	}

	#endregion
}

/// <summary>
/// Sample filter types.
/// </summary>
public enum SampleFilter {
	None,
	LowPass,
	HighPass,
	Boost,
	Attenuate,
	Reverse,
	Normalize
}

/// <summary>
/// Information about a sample's usage.
/// </summary>
public class SampleUsageInfo {
	public int SourceNumber { get; init; }
	public ushort StartAddress { get; init; }
	public ushort LoopAddress { get; init; }
	public bool HasLoop { get; init; }
	public bool IsModified { get; init; }
}
