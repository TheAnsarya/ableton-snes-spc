namespace SpcPlugin.Core.Audio;

/// <summary>
/// High-level sample import and export functionality for SPC editing.
/// Handles WAV import, BRR conversion, and sample management.
/// </summary>
public sealed class SampleImporter {
	/// <summary>
	/// Default maximum sample size in BRR blocks (64 KB / 9 bytes per block ≈ 7281 blocks).
	/// </summary>
	public const int DefaultMaxBlocks = 4096;

	/// <summary>
	/// Options for importing a sample.
	/// </summary>
	public class ImportOptions {
		/// <summary>Target sample rate. Null = keep original or use SNES native (32 kHz).</summary>
		public int? TargetSampleRate { get; set; }

		/// <summary>Whether to normalize the audio.</summary>
		public bool Normalize { get; set; } = true;

		/// <summary>Target peak level in dB for normalization.</summary>
		public double NormalizePeakDb { get; set; } = -1.0;

		/// <summary>Whether to apply fade out to prevent clicks.</summary>
		public bool ApplyFadeOut { get; set; } = true;

		/// <summary>Number of samples for fade out.</summary>
		public int FadeOutSamples { get; set; } = 32;

		/// <summary>Maximum number of BRR blocks.</summary>
		public int MaxBlocks { get; set; } = DefaultMaxBlocks;

		/// <summary>Loop start position in source samples (-1 = no loop).</summary>
		public int LoopStart { get; set; } = -1;

		/// <summary>Loop end position in source samples (-1 = end of sample).</summary>
		public int LoopEnd { get; set; } = -1;

		/// <summary>Whether to auto-detect loop points from WAV cue markers.</summary>
		public bool AutoDetectLoops { get; set; } = true;
	}

	/// <summary>
	/// Result of a sample import operation.
	/// </summary>
	public class ImportResult {
		/// <summary>Success status.</summary>
		public bool Success { get; init; }

		/// <summary>Error message if failed.</summary>
		public string? Error { get; init; }

		/// <summary>Encoded BRR data.</summary>
		public byte[] BrrData { get; init; } = [];

		/// <summary>Loop point in BRR blocks (block index, not byte offset).</summary>
		public int LoopBlock { get; init; }

		/// <summary>Whether the sample has a loop.</summary>
		public bool HasLoop { get; init; }

		/// <summary>Original sample count before processing.</summary>
		public int OriginalSamples { get; init; }

		/// <summary>Final sample count after processing.</summary>
		public int FinalSamples { get; init; }

		/// <summary>Original sample rate.</summary>
		public int OriginalSampleRate { get; init; }

		/// <summary>Final sample rate.</summary>
		public int FinalSampleRate { get; init; }

		/// <summary>Number of BRR blocks.</summary>
		public int BlockCount => BrrData.Length / 9;

		/// <summary>Estimated playback duration in seconds.</summary>
		public double Duration => FinalSampleRate > 0 ? (double)FinalSamples / FinalSampleRate : 0;

		/// <summary>Creates a failed result.</summary>
		public static ImportResult Failed(string error) => new() { Success = false, Error = error };
	}

	/// <summary>
	/// Imports a WAV file and converts it to BRR format.
	/// </summary>
	/// <param name="wavPath">Path to WAV file.</param>
	/// <param name="options">Import options (optional).</param>
	/// <returns>Import result with BRR data.</returns>
	public static ImportResult ImportWav(string wavPath, ImportOptions? options = null) {
		try {
			var wav = WavFile.Load(wavPath);
			return ImportFromWav(wav, options);
		} catch (Exception ex) {
			return ImportResult.Failed($"Failed to load WAV file: {ex.Message}");
		}
	}

	/// <summary>
	/// Imports WAV data from bytes and converts to BRR format.
	/// </summary>
	/// <param name="wavData">WAV file data.</param>
	/// <param name="options">Import options (optional).</param>
	/// <returns>Import result with BRR data.</returns>
	public static ImportResult ImportWavData(byte[] wavData, ImportOptions? options = null) {
		try {
			var wav = WavFile.Parse(wavData);
			return ImportFromWav(wav, options);
		} catch (Exception ex) {
			return ImportResult.Failed($"Failed to parse WAV data: {ex.Message}");
		}
	}

	/// <summary>
	/// Imports from a parsed WAV file and converts to BRR format.
	/// </summary>
	/// <param name="wav">Parsed WAV file.</param>
	/// <param name="options">Import options (optional).</param>
	/// <returns>Import result with BRR data.</returns>
	public static ImportResult ImportFromWav(WavFile wav, ImportOptions? options = null) {
		options ??= new ImportOptions();

		try {
			// Get mono samples
			var samples = wav.MonoSamples;
			int originalSamples = samples.Length;
			int originalRate = wav.SampleRate;

			// Determine target sample rate
			int targetRate = options.TargetSampleRate ?? Resampler.SnesSampleRate;

			// Resample if needed
			if (wav.SampleRate != targetRate) {
				samples = Resampler.Resample(samples, wav.SampleRate, targetRate);
			}

			// Normalize if requested
			if (options.Normalize) {
				samples = Resampler.Normalize(samples, options.NormalizePeakDb);
			}

			// Calculate loop positions in resampled coordinates
			int loopStart = options.LoopStart;
			int loopEnd = options.LoopEnd;

			if (loopStart >= 0 && wav.SampleRate != targetRate) {
				double ratio = (double)targetRate / wav.SampleRate;
				loopStart = (int)(loopStart * ratio);
				if (loopEnd > 0) loopEnd = (int)(loopEnd * ratio);
			}

			// Process with or without loop
			byte[] brrData;
			int loopBlock = 0;
			bool hasLoop = loopStart >= 0;

			if (hasLoop) {
				(brrData, loopBlock) = EncodeBrrWithLoop(samples, loopStart, loopEnd, options);
			} else {
				brrData = EncodeBrrNoLoop(samples, options);
			}

			return new ImportResult {
				Success = true,
				BrrData = brrData,
				LoopBlock = loopBlock,
				HasLoop = hasLoop,
				OriginalSamples = originalSamples,
				FinalSamples = (brrData.Length / 9) * 16,
				OriginalSampleRate = originalRate,
				FinalSampleRate = targetRate,
			};
		} catch (Exception ex) {
			return ImportResult.Failed($"Import failed: {ex.Message}");
		}
	}

	/// <summary>
	/// Imports raw PCM samples and converts to BRR format.
	/// </summary>
	/// <param name="samples">16-bit PCM samples.</param>
	/// <param name="sampleRate">Sample rate of input.</param>
	/// <param name="options">Import options (optional).</param>
	/// <returns>Import result with BRR data.</returns>
	public static ImportResult ImportSamples(short[] samples, int sampleRate, ImportOptions? options = null) {
		options ??= new ImportOptions();

		try {
			int originalSamples = samples.Length;
			int targetRate = options.TargetSampleRate ?? Resampler.SnesSampleRate;

			// Resample if needed
			if (sampleRate != targetRate) {
				samples = Resampler.Resample(samples, sampleRate, targetRate);
			}

			// Normalize if requested
			if (options.Normalize) {
				samples = Resampler.Normalize(samples, options.NormalizePeakDb);
			}

			// Encode
			byte[] brrData;
			int loopBlock = 0;
			bool hasLoop = options.LoopStart >= 0;

			if (hasLoop) {
				int loopStart = options.LoopStart;
				int loopEnd = options.LoopEnd;

				if (sampleRate != targetRate) {
					double ratio = (double)targetRate / sampleRate;
					loopStart = (int)(loopStart * ratio);
					if (loopEnd > 0) loopEnd = (int)(loopEnd * ratio);
				}

				(brrData, loopBlock) = EncodeBrrWithLoop(samples, loopStart, loopEnd, options);
			} else {
				brrData = EncodeBrrNoLoop(samples, options);
			}

			return new ImportResult {
				Success = true,
				BrrData = brrData,
				LoopBlock = loopBlock,
				HasLoop = hasLoop,
				OriginalSamples = originalSamples,
				FinalSamples = (brrData.Length / 9) * 16,
				OriginalSampleRate = sampleRate,
				FinalSampleRate = targetRate,
			};
		} catch (Exception ex) {
			return ImportResult.Failed($"Import failed: {ex.Message}");
		}
	}

	/// <summary>
	/// Exports BRR data to a WAV file.
	/// </summary>
	/// <param name="brrData">BRR data to export.</param>
	/// <param name="wavPath">Output WAV file path.</param>
	/// <param name="sampleRate">Sample rate for WAV (default 32000).</param>
	public static void ExportBrrToWav(byte[] brrData, string wavPath, int sampleRate = 32000) {
		var samples = BrrCodec.Decode(brrData);
		var wavData = WavFile.Create(samples, sampleRate);
		File.WriteAllBytes(wavPath, wavData);
	}

	/// <summary>
	/// Exports BRR data to WAV data in memory.
	/// </summary>
	/// <param name="brrData">BRR data to export.</param>
	/// <param name="sampleRate">Sample rate for WAV (default 32000).</param>
	/// <returns>WAV file data as byte array.</returns>
	public static byte[] ExportBrrToWavData(byte[] brrData, int sampleRate = 32000) {
		var samples = BrrCodec.Decode(brrData);
		return WavFile.Create(samples, sampleRate);
	}

	/// <summary>
	/// Calculates the size in BRR blocks needed for a given duration.
	/// </summary>
	/// <param name="durationSeconds">Duration in seconds.</param>
	/// <param name="sampleRate">Sample rate (default 32000).</param>
	/// <returns>Number of BRR blocks needed.</returns>
	public static int CalculateBlocksForDuration(double durationSeconds, int sampleRate = 32000) {
		int samples = (int)(durationSeconds * sampleRate);
		return (samples + 15) / 16;  // Round up to block boundary
	}

	/// <summary>
	/// Calculates the duration in seconds for a given number of BRR blocks.
	/// </summary>
	/// <param name="blocks">Number of BRR blocks.</param>
	/// <param name="sampleRate">Sample rate (default 32000).</param>
	/// <returns>Duration in seconds.</returns>
	public static double CalculateDurationForBlocks(int blocks, int sampleRate = 32000) {
		return (blocks * 16.0) / sampleRate;
	}

	private static byte[] EncodeBrrNoLoop(short[] samples, ImportOptions options) {
		// Apply fade out
		if (options.ApplyFadeOut) {
			samples = Resampler.ApplyFadeOut(samples, options.FadeOutSamples);
		}

		// Align to block boundary
		samples = Resampler.AlignToBrrBlocks(samples, options.MaxBlocks);

		// Encode
		int blockCount = samples.Length / 16;
		var brrData = new byte[blockCount * 9];

		short prev1 = 0, prev2 = 0;
		for (int i = 0; i < blockCount; i++) {
			var blockSamples = samples.AsSpan(i * 16, 16);
			var blockOutput = brrData.AsSpan(i * 9, 9);
			bool isEnd = i == blockCount - 1;

			BrrCodec.EncodeBlock(blockSamples, blockOutput, isLoop: false, isEnd: isEnd, ref prev1, ref prev2);
		}

		return brrData;
	}

	private static (byte[] BrrData, int LoopBlock) EncodeBrrWithLoop(
		short[] samples,
		int loopStart,
		int loopEnd,
		ImportOptions options) {
		// Extract loop region
		var (preLoop, loopRegion) = Resampler.ExtractLoopRegion(samples, loopStart, loopEnd > 0 ? loopEnd : null);

		// Ensure we have some loop content
		if (loopRegion.Length == 0) {
			loopRegion = new short[16];  // Minimum one block of silence
		}

		// Calculate total blocks respecting max limit
		int preLoopBlocks = preLoop.Length / 16;
		int loopBlocks = loopRegion.Length / 16;
		int totalBlocks = preLoopBlocks + loopBlocks;

		if (totalBlocks > options.MaxBlocks) {
			// Truncate, preferring to keep loop content
			if (loopBlocks > options.MaxBlocks) {
				loopBlocks = options.MaxBlocks;
				preLoopBlocks = 0;
				loopRegion = loopRegion.AsSpan(0, loopBlocks * 16).ToArray();
				preLoop = [];
			} else {
				preLoopBlocks = options.MaxBlocks - loopBlocks;
				preLoop = preLoop.AsSpan(0, preLoopBlocks * 16).ToArray();
			}
			totalBlocks = preLoopBlocks + loopBlocks;
		}

		// Encode
		var brrData = new byte[totalBlocks * 9];
		short prev1 = 0, prev2 = 0;

		// Encode pre-loop
		for (int i = 0; i < preLoopBlocks; i++) {
			var blockSamples = preLoop.AsSpan(i * 16, 16);
			var blockOutput = brrData.AsSpan(i * 9, 9);

			BrrCodec.EncodeBlock(blockSamples, blockOutput, isLoop: false, isEnd: false, ref prev1, ref prev2);
		}

		// Encode loop region
		for (int i = 0; i < loopBlocks; i++) {
			var blockSamples = loopRegion.AsSpan(i * 16, 16);
			var blockOutput = brrData.AsSpan((preLoopBlocks + i) * 9, 9);
			bool isEnd = i == loopBlocks - 1;
			bool isLoopStart = i == 0;

			BrrCodec.EncodeBlock(blockSamples, blockOutput, isLoop: isLoopStart, isEnd: isEnd, ref prev1, ref prev2);
		}

		// Set loop flag on last block
		if (totalBlocks > 0) {
			int lastBlockOffset = (totalBlocks - 1) * 9;
			brrData[lastBlockOffset] |= 0x03;  // End + Loop flags
		}

		return (brrData, preLoopBlocks);
	}
}
