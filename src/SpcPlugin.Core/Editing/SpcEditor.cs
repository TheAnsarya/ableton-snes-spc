namespace SpcPlugin.Core.Editing;

/// <summary>
/// Provides editing capabilities for SPC files.
/// Allows modifying samples, instruments, and playback parameters.
/// </summary>
public sealed class SpcEditor {
	private readonly byte[] _ram = new byte[0x10000];
	private readonly byte[] _dspRegisters = new byte[128];

	/// <summary>
	/// Gets whether the SPC data has been modified.
	/// </summary>
	public bool IsModified { get; private set; }

	/// <summary>
	/// Gets the raw RAM buffer for direct access.
	/// </summary>
	public Span<byte> Ram => _ram.AsSpan();

	/// <summary>
	/// Gets the DSP registers.
	/// </summary>
	public Span<byte> DspRegisters => _dspRegisters.AsSpan();

	/// <summary>
	/// Loads SPC file data for editing.
	/// </summary>
	public void LoadSpc(ReadOnlySpan<byte> spcData) {
		if (spcData.Length < 0x10200) {
			throw new ArgumentException("SPC data too small", nameof(spcData));
		}

		spcData.Slice(0x100, 0x10000).CopyTo(_ram);
		spcData.Slice(0x10100, 128).CopyTo(_dspRegisters);
		IsModified = false;
	}

	/// <summary>
	/// Exports the current state as SPC file data.
	/// </summary>
	public byte[] ExportSpc(SpcMetadata? metadata = null) {
		var output = new byte[0x10200];

		// Write header
		"SNES-SPC700 Sound File Data v0.30"u8.CopyTo(output.AsSpan(0, 33));
		output[0x21] = 0x1a;
		output[0x22] = 0x1a;
		output[0x23] = metadata != null ? (byte)0x1a : (byte)0x1b; // ID666 present flag

		// Default registers (can be overridden)
		output[0x25] = 0x00; // PC low
		output[0x26] = 0x00; // PC high
		output[0x27] = 0x00; // A
		output[0x28] = 0x00; // X
		output[0x29] = 0x00; // Y
		output[0x2a] = 0x00; // PSW
		output[0x2b] = 0xef; // SP

		// Write metadata if provided
		if (metadata != null) {
			WriteString(output, 0x2e, metadata.SongTitle, 32);
			WriteString(output, 0x4e, metadata.GameTitle, 32);
			WriteString(output, 0x6e, metadata.DumperName, 16);
			WriteString(output, 0x7e, metadata.Comments, 32);
			WriteString(output, 0xb0, metadata.ArtistName, 32);
		}

		// Write RAM
		_ram.CopyTo(output.AsSpan(0x100));

		// Write DSP registers
		_dspRegisters.CopyTo(output.AsSpan(0x10100));

		return output;
	}

	#region Voice Editing

	/// <summary>
	/// Gets information about a voice.
	/// </summary>
	public VoiceInfo GetVoiceInfo(int voice) {
		ValidateVoice(voice);
		int baseReg = voice << 4;

		return new VoiceInfo {
			VolumeLeft = (sbyte)_dspRegisters[baseReg | 0x00],
			VolumeRight = (sbyte)_dspRegisters[baseReg | 0x01],
			Pitch = (ushort)(_dspRegisters[baseReg | 0x02] | (_dspRegisters[baseReg | 0x03] << 8)),
			SourceNumber = _dspRegisters[baseReg | 0x04],
			Adsr1 = _dspRegisters[baseReg | 0x05],
			Adsr2 = _dspRegisters[baseReg | 0x06],
			Gain = _dspRegisters[baseReg | 0x07],
			EnvX = _dspRegisters[baseReg | 0x08],
			OutX = _dspRegisters[baseReg | 0x09],
		};
	}

	/// <summary>
	/// Sets voice volume (stereo).
	/// </summary>
	public void SetVoiceVolume(int voice, sbyte left, sbyte right) {
		ValidateVoice(voice);
		int baseReg = voice << 4;
		_dspRegisters[baseReg | 0x00] = (byte)left;
		_dspRegisters[baseReg | 0x01] = (byte)right;
		IsModified = true;
	}

	/// <summary>
	/// Sets voice pitch (14-bit value).
	/// </summary>
	public void SetVoicePitch(int voice, ushort pitch) {
		ValidateVoice(voice);
		int baseReg = voice << 4;
		_dspRegisters[baseReg | 0x02] = (byte)(pitch & 0xff);
		_dspRegisters[baseReg | 0x03] = (byte)((pitch >> 8) & 0x3f);
		IsModified = true;
	}

	/// <summary>
	/// Sets voice source (sample) number.
	/// </summary>
	public void SetVoiceSource(int voice, byte sourceNumber) {
		ValidateVoice(voice);
		_dspRegisters[(voice << 4) | 0x04] = sourceNumber;
		IsModified = true;
	}

	/// <summary>
	/// Sets voice ADSR parameters.
	/// </summary>
	public void SetVoiceAdsr(int voice, byte adsr1, byte adsr2) {
		ValidateVoice(voice);
		int baseReg = voice << 4;
		_dspRegisters[baseReg | 0x05] = adsr1;
		_dspRegisters[baseReg | 0x06] = adsr2;
		IsModified = true;
	}

	/// <summary>
	/// Sets voice GAIN mode.
	/// </summary>
	public void SetVoiceGain(int voice, byte gain) {
		ValidateVoice(voice);
		_dspRegisters[(voice << 4) | 0x07] = gain;
		IsModified = true;
	}

	#endregion

	#region Global DSP Settings

	/// <summary>
	/// Gets/sets main volume (stereo).
	/// </summary>
	public (sbyte Left, sbyte Right) MainVolume {
		get => ((sbyte)_dspRegisters[0x0c], (sbyte)_dspRegisters[0x1c]);
		set {
			_dspRegisters[0x0c] = (byte)value.Left;
			_dspRegisters[0x1c] = (byte)value.Right;
			IsModified = true;
		}
	}

	/// <summary>
	/// Gets/sets echo volume (stereo).
	/// </summary>
	public (sbyte Left, sbyte Right) EchoVolume {
		get => ((sbyte)_dspRegisters[0x2c], (sbyte)_dspRegisters[0x3c]);
		set {
			_dspRegisters[0x2c] = (byte)value.Left;
			_dspRegisters[0x3c] = (byte)value.Right;
			IsModified = true;
		}
	}

	/// <summary>
	/// Gets/sets echo feedback volume.
	/// </summary>
	public sbyte EchoFeedback {
		get => (sbyte)_dspRegisters[0x0d];
		set {
			_dspRegisters[0x0d] = (byte)value;
			IsModified = true;
		}
	}

	/// <summary>
	/// Gets/sets echo delay (0-15, each unit = 16ms).
	/// </summary>
	public byte EchoDelay {
		get => (byte)(_dspRegisters[0x7d] & 0x0f);
		set {
			_dspRegisters[0x7d] = (byte)(value & 0x0f);
			IsModified = true;
		}
	}

	/// <summary>
	/// Gets/sets the FIR filter coefficients.
	/// </summary>
	public void SetFirCoefficients(ReadOnlySpan<sbyte> coefficients) {
		if (coefficients.Length != 8) {
			throw new ArgumentException("FIR requires exactly 8 coefficients", nameof(coefficients));
		}

		for (int i = 0; i < 8; i++) {
			_dspRegisters[(i << 4) | 0x0f] = (byte)coefficients[i];
		}

		IsModified = true;
	}

	/// <summary>
	/// Gets the FIR filter coefficients.
	/// </summary>
	public sbyte[] GetFirCoefficients() {
		var result = new sbyte[8];
		for (int i = 0; i < 8; i++) {
			result[i] = (sbyte)_dspRegisters[(i << 4) | 0x0f];
		}

		return result;
	}

	/// <summary>
	/// Gets/sets which voices have echo enabled.
	/// </summary>
	public byte EchoEnable {
		get => _dspRegisters[0x4d];
		set {
			_dspRegisters[0x4d] = value;
			IsModified = true;
		}
	}

	/// <summary>
	/// Gets/sets which voices use noise instead of samples.
	/// </summary>
	public byte NoiseEnable {
		get => _dspRegisters[0x3d];
		set {
			_dspRegisters[0x3d] = value;
			IsModified = true;
		}
	}

	/// <summary>
	/// Gets/sets pitch modulation enable bits.
	/// </summary>
	public byte PitchModulation {
		get => _dspRegisters[0x2d];
		set {
			_dspRegisters[0x2d] = value;
			IsModified = true;
		}
	}

	/// <summary>
	/// Sets echo enabled for voices (convenience method).
	/// </summary>
	public void SetEchoEnabled(int voiceMask) {
		EchoEnable = (byte)voiceMask;
	}

	/// <summary>
	/// Sets noise enabled for voices (convenience method).
	/// </summary>
	public void SetNoiseEnabled(int voiceMask) {
		NoiseEnable = (byte)voiceMask;
	}

	/// <summary>
	/// Sets pitch modulation enabled for voices (convenience method).
	/// </summary>
	public void SetPitchModEnabled(int voiceMask) {
		PitchModulation = (byte)voiceMask;
	}

	/// <summary>
	/// Sets echo feedback (convenience method).
	/// </summary>
	public void SetEchoFeedback(int feedback) {
		EchoFeedback = (sbyte)feedback;
	}

	/// <summary>
	/// Sets echo delay (convenience method).
	/// </summary>
	public void SetEchoDelay(int delay) {
		EchoDelay = (byte)delay;
	}

	/// <summary>
	/// Sets echo volume (convenience method).
	/// </summary>
	public void SetEchoVolume(int left, int right) {
		EchoVolume = ((sbyte)left, (sbyte)right);
	}

	/// <summary>
	/// Sets main volume (convenience method).
	/// </summary>
	public void SetMainVolume(int left, int right) {
		MainVolume = ((sbyte)left, (sbyte)right);
	}

	/// <summary>
	/// Triggers key-on for a voice.
	/// </summary>
	public void KeyOnVoice(int voice) {
		ValidateVoice(voice);
		_dspRegisters[0x4c] = (byte)(1 << voice);
		IsModified = true;
	}

	/// <summary>
	/// Triggers key-off for a voice.
	/// </summary>
	public void KeyOffVoice(int voice) {
		ValidateVoice(voice);
		_dspRegisters[0x5c] = (byte)(1 << voice);
		IsModified = true;
	}

	/// <summary>
	/// Sets voice ADSR parameters with individual values.
	/// </summary>
	public void SetVoiceAdsr(int voice, int attack, int decay, int sustain, int release) {
		ValidateVoice(voice);
		byte adsr1 = (byte)(0x80 | ((decay & 0x07) << 4) | (attack & 0x0f));
		byte adsr2 = (byte)(((sustain & 0x07) << 5) | (release & 0x1f));
		SetVoiceAdsr(voice, adsr1, adsr2);
	}

	#endregion

	#region Sample Directory

	/// <summary>
	/// Gets the sample directory base address.
	/// </summary>
	public int SampleDirectoryAddress => _dspRegisters[0x5d] << 8;

	/// <summary>
	/// Gets the sample directory page (alias for VST UI).
	/// </summary>
	public int SampleDirectory => SampleDirectoryAddress;

	/// <summary>
	/// Gets/sets the FIR filter coefficients.
	/// </summary>
	public sbyte[] EchoFir {
		get => GetFirCoefficients();
		set => SetFirCoefficients(value);
	}

	/// <summary>
	/// Gets information about a sample from the directory.
	/// </summary>
	public SampleInfo GetSampleInfo(int sourceNumber) {
		if (sourceNumber < 0 || sourceNumber > 255) {
			throw new ArgumentOutOfRangeException(nameof(sourceNumber));
		}

		int dirAddr = SampleDirectoryAddress + (sourceNumber << 2);
		if (dirAddr + 3 >= _ram.Length) {
			return new SampleInfo { StartAddress = 0, LoopAddress = 0, Length = 0 };
		}

		ushort startAddress = (ushort)(_ram[dirAddr] | (_ram[dirAddr + 1] << 8));
		ushort loopAddress = (ushort)(_ram[dirAddr + 2] | (_ram[dirAddr + 3] << 8));

		// Calculate length by finding end block
		int length = CalculateBrrLength(startAddress);

		return new SampleInfo {
			StartAddress = startAddress,
			LoopAddress = loopAddress,
			Length = length
		};
	}

	/// <summary>
	/// Calculates the length of BRR data by finding the end block.
	/// </summary>
	private int CalculateBrrLength(int startAddress) {
		int addr = startAddress;
		int length = 0;
		int maxBlocks = 0x10000 / 9;

		for (int block = 0; block < maxBlocks && addr + 9 <= _ram.Length; block++) {
			byte header = _ram[addr];
			length += 9;

			if ((header & 0x01) != 0) { // End flag
				break;
			}

			addr += 9;
		}

		return length;
	}

	/// <summary>
	/// Gets the raw BRR data for a sample.
	/// </summary>
	public byte[] GetSampleBrr(int sourceNumber) {
		var info = GetSampleInfo(sourceNumber);
		if (info.Length == 0 || info.StartAddress + info.Length > _ram.Length) {
			return [];
		}

		var result = new byte[info.Length];
		_ram.AsSpan(info.StartAddress, info.Length).CopyTo(result);
		return result;
	}

	/// <summary>
	/// Sets sample directory entry.
	/// </summary>
	public void SetSampleInfo(int sourceNumber, ushort startAddress, ushort loopAddress) {
		if (sourceNumber < 0 || sourceNumber > 255) {
			throw new ArgumentOutOfRangeException(nameof(sourceNumber));
		}

		int dirAddr = SampleDirectoryAddress + (sourceNumber << 2);
		_ram[dirAddr] = (byte)(startAddress & 0xff);
		_ram[dirAddr + 1] = (byte)(startAddress >> 8);
		_ram[dirAddr + 2] = (byte)(loopAddress & 0xff);
		_ram[dirAddr + 3] = (byte)(loopAddress >> 8);
		IsModified = true;
	}

	#endregion

	#region BRR Sample Operations

	/// <summary>
	/// Extracts a BRR sample as decoded PCM data.
	/// </summary>
	public short[] ExtractSample(int sourceNumber) {
		var info = GetSampleInfo(sourceNumber);
		return DecodeBrr(info.StartAddress, info.LoopAddress);
	}

	/// <summary>
	/// Decodes BRR data starting at the given address.
	/// </summary>
	public short[] DecodeBrr(int startAddress, int loopAddress) {
		var samples = new List<short>();
		short prev1 = 0, prev2 = 0;
		int addr = startAddress;
		int maxBlocks = 0x10000 / 9; // Prevent infinite loops

		for (int block = 0; block < maxBlocks; block++) {
			if (addr + 9 > _ram.Length) break;

			byte header = _ram[addr];
			int shift = header >> 4;
			int filter = (header >> 2) & 0x03;
			bool end = (header & 0x01) != 0;
			bool loop = (header & 0x02) != 0;

			// Decode 16 samples from this block
			for (int i = 0; i < 16; i++) {
				int nibble = (i & 1) == 0
					? _ram[addr + 1 + (i >> 1)] >> 4
					: _ram[addr + 1 + (i >> 1)] & 0x0f;

				if (nibble >= 8) nibble -= 16;
				int sample = nibble << shift;

				sample = filter switch {
					0 => sample,
					1 => sample + prev1 + (-prev1 >> 4),
					2 => sample + (prev1 << 1) + ((-prev1 * 3) >> 5) - prev2 + (prev2 >> 4),
					3 => sample + (prev1 << 1) + ((-prev1 * 13) >> 6) - prev2 + ((prev2 * 3) >> 4),
					_ => sample,
				};

				sample = Math.Clamp(sample, short.MinValue, short.MaxValue);
				samples.Add((short)sample);

				prev2 = prev1;
				prev1 = (short)sample;
			}

			if (end) {
				if (loop && loopAddress != startAddress) {
					// Include one iteration of loop for reference
					addr = loopAddress;
				} else {
					break;
				}
			} else {
				addr += 9;
			}
		}

		return [.. samples];
	}

	/// <summary>
	/// Encodes PCM samples to BRR format and writes to RAM.
	/// </summary>
	public int EncodeBrr(ReadOnlySpan<short> samples, int targetAddress, bool loop = false, int loopPoint = 0) {
		int blocksNeeded = (samples.Length + 15) / 16;
		int outputSize = blocksNeeded * 9;

		if (targetAddress + outputSize > _ram.Length) {
			throw new ArgumentException("Not enough space in RAM for BRR data");
		}

		int addr = targetAddress;
		int sampleIndex = 0;
		short prev1 = 0, prev2 = 0;

		for (int block = 0; block < blocksNeeded; block++) {
			bool isLast = block == blocksNeeded - 1;
			bool isLoopStart = loop && (block * 16 == loopPoint);

			// Find best shift and filter for this block
			int bestShift = 0;
			int bestFilter = 0;
			int bestError = int.MaxValue;

			for (int shift = 0; shift <= 12; shift++) {
				for (int filter = 0; filter < 4; filter++) {
					int error = CalculateBlockError(samples, sampleIndex, shift, filter, prev1, prev2);
					if (error < bestError) {
						bestError = error;
						bestShift = shift;
						bestFilter = filter;
					}
				}
			}

			// Write header
			byte header = (byte)((bestShift << 4) | (bestFilter << 2));
			if (isLast) header |= 0x01; // End flag
			if (loop) header |= 0x02;   // Loop flag
			_ram[addr] = header;

			// Encode 16 samples
			short tempPrev1 = prev1, tempPrev2 = prev2;
			for (int i = 0; i < 16; i += 2) {
				int idx1 = sampleIndex + i;
				int idx2 = sampleIndex + i + 1;

				int sample1 = idx1 < samples.Length ? samples[idx1] : 0;
				int sample2 = idx2 < samples.Length ? samples[idx2] : 0;

				int nibble1 = EncodeNibble(sample1, bestShift, bestFilter, ref tempPrev1, ref tempPrev2);
				int nibble2 = EncodeNibble(sample2, bestShift, bestFilter, ref tempPrev1, ref tempPrev2);

				_ram[addr + 1 + (i >> 1)] = (byte)((nibble1 << 4) | (nibble2 & 0x0f));
			}

			prev1 = tempPrev1;
			prev2 = tempPrev2;
			sampleIndex += 16;
			addr += 9;
		}

		IsModified = true;
		return outputSize;
	}

	private int CalculateBlockError(ReadOnlySpan<short> samples, int startIndex, int shift, int filter, short prev1, short prev2) {
		int error = 0;
		short p1 = prev1, p2 = prev2;

		for (int i = 0; i < 16 && startIndex + i < samples.Length; i++) {
			int target = samples[startIndex + i];
			int predicted = filter switch {
				1 => p1 + (-p1 >> 4),
				2 => (p1 << 1) + ((-p1 * 3) >> 5) - p2 + (p2 >> 4),
				3 => (p1 << 1) + ((-p1 * 13) >> 6) - p2 + ((p2 * 3) >> 4),
				_ => 0,
			};

			int residual = target - predicted;
			int nibble = Math.Clamp(residual >> shift, -8, 7);
			int reconstructed = (nibble << shift) + predicted;

			error += Math.Abs(target - reconstructed);

			p2 = p1;
			p1 = (short)Math.Clamp(reconstructed, short.MinValue, short.MaxValue);
		}

		return error;
	}

	private static int EncodeNibble(int sample, int shift, int filter, ref short prev1, ref short prev2) {
		int predicted = filter switch {
			1 => prev1 + (-prev1 >> 4),
			2 => (prev1 << 1) + ((-prev1 * 3) >> 5) - prev2 + (prev2 >> 4),
			3 => (prev1 << 1) + ((-prev1 * 13) >> 6) - prev2 + ((prev2 * 3) >> 4),
			_ => 0,
		};

		int residual = sample - predicted;
		int nibble = Math.Clamp(residual >> shift, -8, 7);

		// Reconstruct and update history
		int reconstructed = (nibble << shift) + predicted;
		reconstructed = Math.Clamp(reconstructed, short.MinValue, short.MaxValue);

		prev2 = prev1;
		prev1 = (short)reconstructed;

		return nibble & 0x0f;
	}

	#endregion

	#region Helpers

	private static void ValidateVoice(int voice) {
		if (voice < 0 || voice >= 8) {
			throw new ArgumentOutOfRangeException(nameof(voice), "Voice must be 0-7");
		}
	}

	private static void WriteString(byte[] buffer, int offset, string? value, int maxLength) {
		if (string.IsNullOrEmpty(value)) return;

		var bytes = System.Text.Encoding.ASCII.GetBytes(value);
		int length = Math.Min(bytes.Length, maxLength);
		Array.Copy(bytes, 0, buffer, offset, length);
	}

	#endregion
}

/// <summary>
/// Information about a voice channel.
/// </summary>
public readonly struct VoiceInfo {
	public sbyte VolumeLeft { get; init; }
	public sbyte VolumeRight { get; init; }
	public ushort Pitch { get; init; }
	public byte SourceNumber { get; init; }
	public byte Adsr1 { get; init; }
	public byte Adsr2 { get; init; }
	public byte Gain { get; init; }
	public byte EnvX { get; init; }
	public byte OutX { get; init; }

	/// <summary>
	/// Gets the pitch as a frequency multiplier.
	/// </summary>
	public float PitchMultiplier => (Pitch & 0x3fff) / 4096f;

	/// <summary>
	/// Gets whether ADSR mode is enabled.
	/// </summary>
	public bool UseAdsr => (Adsr1 & 0x80) != 0;

	/// <summary>
	/// Gets attack rate (0-15).
	/// </summary>
	public int AttackRate => Adsr1 & 0x0f;

	/// <summary>
	/// Gets decay rate (0-7).
	/// </summary>
	public int DecayRate => (Adsr1 >> 4) & 0x07;

	/// <summary>
	/// Gets sustain level (0-7).
	/// </summary>
	public int SustainLevel => (Adsr2 >> 5) & 0x07;

	/// <summary>
	/// Gets sustain rate (0-31).
	/// </summary>
	public int SustainRate => Adsr2 & 0x1f;
}

/// <summary>
/// Information about a sample.
/// </summary>
public readonly struct SampleInfo {
	public ushort StartAddress { get; init; }
	public ushort LoopAddress { get; init; }
	public int Length { get; init; }

	/// <summary>
	/// Gets whether the sample has a loop point different from start.
	/// </summary>
	public bool HasLoop => LoopAddress != StartAddress && LoopAddress >= StartAddress;
}

/// <summary>
/// Metadata for SPC files (ID666 tag).
/// </summary>
public class SpcMetadata {
	public string? SongTitle { get; set; }
	public string? GameTitle { get; set; }
	public string? DumperName { get; set; }
	public string? Comments { get; set; }
	public string? ArtistName { get; set; }
	public int DurationSeconds { get; set; }
	public int FadeMilliseconds { get; set; }
}
