namespace SpcPlugin.Core.Audio;

/// <summary>
/// BRR (Bit Rate Reduction) codec for SNES sample compression.
/// BRR encodes 16 samples into 9 bytes (4 bits per sample + 1 byte header).
/// </summary>
public static class BrrCodec {
	private const int SamplesPerBlock = 16;
	private const int BytesPerBlock = 9;

	/// <summary>
	/// Decodes complete BRR data into PCM samples.
	/// </summary>
	/// <param name="brrData">BRR encoded data (must be multiple of 9 bytes).</param>
	/// <returns>Decoded 16-bit PCM samples.</returns>
	public static short[] Decode(byte[] brrData) {
		if (brrData.Length == 0) return [];
		if (brrData.Length % BytesPerBlock != 0) {
			throw new ArgumentException("BRR data must be a multiple of 9 bytes", nameof(brrData));
		}

		int blockCount = brrData.Length / BytesPerBlock;
		var samples = new short[blockCount * SamplesPerBlock];
		short prev1 = 0, prev2 = 0;

		for (int block = 0; block < blockCount; block++) {
			var blockData = brrData.AsSpan(block * BytesPerBlock, BytesPerBlock);
			var output = samples.AsSpan(block * SamplesPerBlock, SamplesPerBlock);
			DecodeBlock(blockData, output, ref prev1, ref prev2);

			// Check for end flag
			if ((blockData[0] & 0x01) != 0) {
				// Return only the samples up to and including this block
				return samples.AsSpan(0, (block + 1) * SamplesPerBlock).ToArray();
			}
		}

		return samples;
	}

	/// <summary>
	/// Decodes a BRR block into 16 PCM samples.
	/// </summary>
	/// <param name="brrBlock">9-byte BRR block.</param>
	/// <param name="output">Output buffer for 16 samples.</param>
	/// <param name="prev1">Previous sample 1 (for filtering).</param>
	/// <param name="prev2">Previous sample 2 (for filtering).</param>
	public static void DecodeBlock(
		ReadOnlySpan<byte> brrBlock,
		Span<short> output,
		ref short prev1,
		ref short prev2) {
		if (brrBlock.Length < BytesPerBlock) {
			throw new ArgumentException("BRR block must be 9 bytes", nameof(brrBlock));
		}
		if (output.Length < SamplesPerBlock) {
			throw new ArgumentException("Output must hold 16 samples", nameof(output));
		}

		byte header = brrBlock[0];
		int shift = header >> 4;
		int filter = (header >> 2) & 0x03;

		// Decode 16 nibbles from 8 bytes
		for (int i = 0; i < SamplesPerBlock; i++) {
			int byteIndex = 1 + (i >> 1);
			int nibble = (i & 1) == 0
				? brrBlock[byteIndex] >> 4
				: brrBlock[byteIndex] & 0x0f;

			// Sign extend nibble
			if (nibble >= 8) nibble -= 16;

			// Apply shift
			int sample = nibble << shift;

			// Apply filter
			sample = ApplyFilter(sample, filter, prev1, prev2);

			// Clamp to 16-bit range
			sample = Math.Clamp(sample, short.MinValue, short.MaxValue);

			output[i] = (short)sample;

			// Update history
			prev2 = prev1;
			prev1 = (short)sample;
		}
	}

	/// <summary>
	/// Encodes 16 PCM samples into a BRR block.
	/// </summary>
	/// <param name="samples">16 PCM samples to encode.</param>
	/// <param name="output">9-byte output buffer for BRR block.</param>
	/// <param name="isLoop">True if this is a loop point.</param>
	/// <param name="isEnd">True if this is the last block.</param>
	/// <param name="prev1">Previous sample 1 (for filtering).</param>
	/// <param name="prev2">Previous sample 2 (for filtering).</param>
	public static void EncodeBlock(
		ReadOnlySpan<short> samples,
		Span<byte> output,
		bool isLoop,
		bool isEnd,
		ref short prev1,
		ref short prev2) {
		if (samples.Length < SamplesPerBlock) {
			throw new ArgumentException("Input must have 16 samples", nameof(samples));
		}
		if (output.Length < BytesPerBlock) {
			throw new ArgumentException("Output must hold 9 bytes", nameof(output));
		}

		// Find optimal shift and filter
		int bestShift = FindOptimalShift(samples);
		int bestFilter = FindOptimalFilter(samples, bestShift, prev1, prev2);

		// Build header
		byte header = (byte)((bestShift << 4) | (bestFilter << 2));
		if (isLoop) header |= 0x02;
		if (isEnd) header |= 0x01;
		output[0] = header;

		// Encode samples
		short p1 = prev1, p2 = prev2;
		for (int i = 0; i < SamplesPerBlock; i += 2) {
			int nibble1 = EncodeSample(samples[i], bestShift, bestFilter, p1, p2);
			int decoded1 = DecodeSample(nibble1, bestShift, bestFilter, p1, p2);
			p2 = p1;
			p1 = (short)decoded1;

			int nibble2 = EncodeSample(samples[i + 1], bestShift, bestFilter, p1, p2);
			int decoded2 = DecodeSample(nibble2, bestShift, bestFilter, p1, p2);
			p2 = p1;
			p1 = (short)decoded2;

			output[1 + (i >> 1)] = (byte)((nibble1 << 4) | (nibble2 & 0x0f));
		}

		prev1 = p1;
		prev2 = p2;
	}

	/// <summary>
	/// Checks if a BRR block is the end block.
	/// </summary>
	public static bool IsEndBlock(byte header) => (header & 0x01) != 0;

	/// <summary>
	/// Checks if a BRR block is a loop point.
	/// </summary>
	public static bool IsLoopBlock(byte header) => (header & 0x02) != 0;

	private static int ApplyFilter(int sample, int filter, short prev1, short prev2) {
		return filter switch {
			0 => sample,
			1 => sample + prev1 + (-prev1 >> 4),
			2 => sample + (prev1 << 1) + (-((prev1 << 1) + prev1) >> 5)
				 - prev2 + (prev2 >> 4),
			3 => sample + (prev1 << 1) + (-(prev1 + (prev1 << 2) + (prev1 << 3)) >> 6)
				 - prev2 + (((prev2 << 1) + prev2) >> 4),
			_ => sample,
		};
	}

	private static int FindOptimalShift(ReadOnlySpan<short> samples) {
		int maxAbs = 0;
		foreach (short s in samples) {
			int abs = Math.Abs(s);
			if (abs > maxAbs) maxAbs = abs;
		}

		// Calculate minimum shift needed
		for (int shift = 0; shift <= 12; shift++) {
			if ((maxAbs >> shift) <= 7) return shift;
		}
		return 12;
	}

	private static int FindOptimalFilter(
		ReadOnlySpan<short> samples, int shift, short prev1, short prev2) {
		// TODO: Implement proper filter selection based on error minimization
		return 0;
	}

	private static int EncodeSample(short sample, int shift, int filter, short prev1, short prev2) {
		int predicted = filter switch {
			0 => 0,
			1 => prev1 + (-prev1 >> 4),
			2 => (prev1 << 1) + (-((prev1 << 1) + prev1) >> 5) - prev2 + (prev2 >> 4),
			3 => (prev1 << 1) + (-(prev1 + (prev1 << 2) + (prev1 << 3)) >> 6)
				 - prev2 + (((prev2 << 1) + prev2) >> 4),
			_ => 0,
		};

		int residual = sample - predicted;
		int nibble = residual >> shift;

		// Clamp to 4-bit signed range
		return Math.Clamp(nibble, -8, 7) & 0x0f;
	}

	private static int DecodeSample(int nibble, int shift, int filter, short prev1, short prev2) {
		// Sign extend
		if (nibble >= 8) nibble -= 16;
		int sample = nibble << shift;
		return Math.Clamp(ApplyFilter(sample, filter, prev1, prev2), short.MinValue, short.MaxValue);
	}
}
