namespace SpcPlugin.Core.Analysis;

/// <summary>
/// Analyzes SPC files to extract structure information such as sample locations,
/// sequence data, and sound driver type.
/// </summary>
public sealed class SpcAnalyzer {
	private readonly byte[] _ram = new byte[0x10000];
	private readonly byte[] _dspRegisters = new byte[128];

	/// <summary>
	/// Gets the detected sound driver type.
	/// </summary>
	public SoundDriverType DriverType { get; private set; }

	/// <summary>
	/// Gets the extracted samples from the SPC.
	/// </summary>
	public List<SampleInfo> Samples { get; } = [];

	/// <summary>
	/// Gets memory usage information.
	/// </summary>
	public MemoryUsage Memory { get; private set; } = new();

	/// <summary>
	/// Gets the echo configuration.
	/// </summary>
	public EchoConfiguration Echo { get; private set; } = new();

	/// <summary>
	/// Analyzes an SPC file and extracts structural information.
	/// </summary>
	public void Analyze(ReadOnlySpan<byte> spcData) {
		if (spcData.Length < 0x10200) {
			throw new ArgumentException("SPC data too small", nameof(spcData));
		}

		spcData.Slice(0x100, 0x10000).CopyTo(_ram);
		spcData.Slice(0x10100, 128).CopyTo(_dspRegisters);

		DetectDriver();
		ExtractSamples();
		AnalyzeMemory();
		AnalyzeEcho();
	}

	/// <summary>
	/// Detects the sound driver used in the SPC.
	/// </summary>
	private void DetectDriver() {
		// Check for N-SPC (Nintendo's driver)
		if (CheckNSpc()) {
			DriverType = SoundDriverType.NSPC;
			return;
		}

		// Check for Akao (Square's driver)
		if (CheckAkao()) {
			DriverType = SoundDriverType.Akao;
			return;
		}

		// Check for HAL Laboratory's driver
		if (CheckHalLab()) {
			DriverType = SoundDriverType.HalLab;
			return;
		}

		// Check for Capcom's driver
		if (CheckCapcom()) {
			DriverType = SoundDriverType.Capcom;
			return;
		}

		// Check for Konami's driver
		if (CheckKonami()) {
			DriverType = SoundDriverType.Konami;
			return;
		}

		DriverType = SoundDriverType.Unknown;
	}

	private bool CheckNSpc() {
		// N-SPC typically starts at $0200 with recognizable code patterns
		// Look for common N-SPC signatures
		var signature1 = new byte[] { 0xcd, 0x00, 0xbd, 0x00 };
		var signature2 = new byte[] { 0x8f, 0x00, 0x00, 0xcd };

		return MatchSignature(_ram, 0x200, signature1, 0xff) ||
			   MatchSignature(_ram, 0x200, signature2, 0xff);
	}

	private bool CheckAkao() {
		// Akao (Square) has distinctive initialization patterns
		// Often uses $0500 as the main routine start
		var signature = new byte[] { 0xef, 0xe4, 0x00, 0xc4 };
		return MatchSignature(_ram, 0x500, signature, 0xff);
	}

	private bool CheckHalLab() {
		// HAL Laboratory's driver (Kirby, EarthBound)
		var signature = new byte[] { 0x8f, 0x6c, 0xf2, 0x8f };
		return MatchSignature(_ram, 0x200, signature, 0xff);
	}

	private bool CheckCapcom() {
		// Capcom's CPS driver
		var signature = new byte[] { 0xcd, 0xef, 0xbd, 0xe8 };
		return MatchSignature(_ram, 0x200, signature, 0xff);
	}

	private bool CheckKonami() {
		// Konami's driver (Castlevania, TMNT, etc.)
		var signature = new byte[] { 0x20, 0x8f, 0x00, 0x00 };
		return MatchSignature(_ram, 0x200, signature, 0xff);
	}

	private static bool MatchSignature(byte[] data, int offset, byte[] signature, byte mask) {
		if (offset + signature.Length > data.Length) return false;

		for (int i = 0; i < signature.Length; i++) {
			if ((data[offset + i] & mask) != (signature[i] & mask)) {
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Extracts all samples from the SPC by parsing the sample directory.
	/// </summary>
	private void ExtractSamples() {
		Samples.Clear();

		// Get sample directory page from DSP register $5D (DIR)
		int dirPage = _dspRegisters[0x5d] << 8;

		// Scan all 256 possible sample entries
		var usedSamples = new HashSet<int>();

		// First, find which samples are actually referenced by voices
		for (int v = 0; v < 8; v++) {
			int srcn = _dspRegisters[(v << 4) | 0x04];
			usedSamples.Add(srcn);
		}

		// Extract each used sample
		foreach (int srcn in usedSamples) {
			if (srcn >= 256) continue;

			int dirEntry = dirPage + (srcn * 4);
			if (dirEntry + 4 > 0x10000) continue;

			int sampleStart = _ram[dirEntry] | (_ram[dirEntry + 1] << 8);
			int loopStart = _ram[dirEntry + 2] | (_ram[dirEntry + 3] << 8);

			// Find sample end by looking for BRR end flag
			int sampleEnd = FindBrrEnd(sampleStart);

			if (sampleEnd > sampleStart) {
				Samples.Add(new SampleInfo {
					Index = srcn,
					StartAddress = sampleStart,
					EndAddress = sampleEnd,
					LoopAddress = loopStart,
					Size = sampleEnd - sampleStart,
					HasLoop = loopStart >= sampleStart && loopStart < sampleEnd,
				});
			}
		}
	}

	private int FindBrrEnd(int startAddr) {
		int addr = startAddr;

		// BRR blocks are 9 bytes each
		// First byte is header: bit 0 = end flag, bit 1 = loop flag
		while (addr < 0x10000 - 9) {
			byte header = _ram[addr];

			// Move to next block
			addr += 9;

			// Check end flag
			if ((header & 0x01) != 0) {
				return addr;
			}

			// Safety limit - samples shouldn't be larger than 16KB typically
			if (addr - startAddr > 16384) break;
		}

		return addr;
	}

	/// <summary>
	/// Analyzes memory usage patterns.
	/// </summary>
	private void AnalyzeMemory() {
		Memory = new MemoryUsage();

		// Calculate sample RAM usage
		foreach (var sample in Samples) {
			Memory.SampleBytes += sample.Size;
		}

		// Echo buffer size: EDL × 2048 bytes (stereo)
		int edl = _dspRegisters[0x6d] & 0x0f;
		Memory.EchoBytes = edl == 0 ? 4 : edl * 2048;

		// Calculate echo buffer location
		int esa = _dspRegisters[0x5d] << 8;
		Memory.EchoStartAddress = esa;
		Memory.EchoEndAddress = esa + Memory.EchoBytes;

		// Estimate driver code size (rough estimation)
		// Usually driver code is in the first 2-8KB
		Memory.DriverBytes = EstimateDriverSize();

		// Free memory is what's left
		Memory.FreeBytes = 0x10000 - Memory.SampleBytes - Memory.EchoBytes - Memory.DriverBytes;
	}

	private int EstimateDriverSize() {
		// Look for first sample start address as a rough guide
		int firstSample = 0x10000;
		foreach (var sample in Samples) {
			if (sample.StartAddress < firstSample) {
				firstSample = sample.StartAddress;
			}
		}

		// Driver is typically before samples
		return Math.Min(firstSample, 0x2000); // Cap at 8KB
	}

	/// <summary>
	/// Analyzes echo/reverb configuration.
	/// </summary>
	private void AnalyzeEcho() {
		int edl = _dspRegisters[0x6d] & 0x0f;

		Echo = new EchoConfiguration {
			Enabled = (_dspRegisters[0x6c] & 0x20) == 0, // Bit 5 of FLG disables echo
			Delay = edl,
			DelayMs = edl * 16,
			Feedback = (sbyte)_dspRegisters[0x0d],
			VolumeLeft = (sbyte)_dspRegisters[0x2c],
			VolumeRight = (sbyte)_dspRegisters[0x3c],
			EnabledVoices = _dspRegisters[0x3d],
			FirCoefficients = [
				(sbyte)_dspRegisters[0x0f],
				(sbyte)_dspRegisters[0x1f],
				(sbyte)_dspRegisters[0x2f],
				(sbyte)_dspRegisters[0x3f],
				(sbyte)_dspRegisters[0x4f],
				(sbyte)_dspRegisters[0x5f],
				(sbyte)_dspRegisters[0x6f],
				(sbyte)_dspRegisters[0x7f],
			],
		};
	}

	/// <summary>
	/// Extracts raw BRR sample data.
	/// </summary>
	public byte[] ExtractSampleBrr(int sampleIndex) {
		var sample = Samples.FirstOrDefault(s => s.Index == sampleIndex);
		if (sample == null) {
			return [];
		}

		var brr = new byte[sample.Size];
		Array.Copy(_ram, sample.StartAddress, brr, 0, sample.Size);
		return brr;
	}

	/// <summary>
	/// Gets voice information for all 8 voices.
	/// </summary>
	public VoiceAnalysis[] GetVoiceInfo() {
		var voices = new VoiceAnalysis[8];

		for (int v = 0; v < 8; v++) {
			int baseReg = v << 4;

			voices[v] = new VoiceAnalysis {
				VoiceIndex = v,
				VolumeLeft = (sbyte)_dspRegisters[baseReg | 0x00],
				VolumeRight = (sbyte)_dspRegisters[baseReg | 0x01],
				Pitch = (ushort)(_dspRegisters[baseReg | 0x02] | (_dspRegisters[baseReg | 0x03] << 8)),
				SourceNumber = _dspRegisters[baseReg | 0x04],
				AdsrEnabled = (_dspRegisters[baseReg | 0x05] & 0x80) != 0,
				AttackRate = _dspRegisters[baseReg | 0x05] & 0x0f,
				DecayRate = (_dspRegisters[baseReg | 0x05] >> 4) & 0x07,
				SustainRate = _dspRegisters[baseReg | 0x06] & 0x1f,
				SustainLevel = (_dspRegisters[baseReg | 0x06] >> 5) & 0x07,
				GainValue = _dspRegisters[baseReg | 0x07],
				HasEcho = (_dspRegisters[0x3d] & (1 << v)) != 0,
				HasPitchMod = (_dspRegisters[0x1d] & (1 << v)) != 0,
				UsesNoise = (_dspRegisters[0x2d] & (1 << v)) != 0,
			};
		}

		return voices;
	}
}

/// <summary>
/// Known SNES sound driver types.
/// </summary>
public enum SoundDriverType {
	Unknown,
	NSPC,       // Nintendo (Super Mario World, Zelda, etc.)
	Akao,       // Square (Final Fantasy, Chrono Trigger)
	HalLab,     // HAL Laboratory (Kirby, EarthBound)
	Capcom,     // Capcom (Mega Man X, Street Fighter)
	Konami,     // Konami (Castlevania, TMNT)
	Rare,       // Rare (Donkey Kong Country)
	Enix,       // Enix (Dragon Quest, ActRaiser)
	Custom,     // Custom/unknown driver
}

/// <summary>
/// Information about an extracted sample.
/// </summary>
public sealed class SampleInfo {
	public int Index { get; init; }
	public int StartAddress { get; init; }
	public int EndAddress { get; init; }
	public int LoopAddress { get; init; }
	public int Size { get; init; }
	public bool HasLoop { get; init; }
	public string Name { get; set; } = "";
}

/// <summary>
/// Memory usage breakdown.
/// </summary>
public sealed class MemoryUsage {
	public int SampleBytes { get; set; }
	public int EchoBytes { get; set; }
	public int DriverBytes { get; set; }
	public int FreeBytes { get; set; }
	public int EchoStartAddress { get; set; }
	public int EchoEndAddress { get; set; }

	public int TotalUsed => SampleBytes + EchoBytes + DriverBytes;
	public double SamplePercent => SampleBytes / 65536.0 * 100;
	public double EchoPercent => EchoBytes / 65536.0 * 100;
	public double FreePercent => FreeBytes / 65536.0 * 100;
}

/// <summary>
/// Echo/reverb configuration.
/// </summary>
public sealed class EchoConfiguration {
	public bool Enabled { get; init; }
	public int Delay { get; init; }
	public int DelayMs { get; init; }
	public sbyte Feedback { get; init; }
	public sbyte VolumeLeft { get; init; }
	public sbyte VolumeRight { get; init; }
	public byte EnabledVoices { get; init; }
	public sbyte[] FirCoefficients { get; init; } = new sbyte[8];

	public int BufferSize => Delay == 0 ? 4 : Delay * 2048;
}

/// <summary>
/// Per-voice analysis information.
/// </summary>
public sealed class VoiceAnalysis {
	public int VoiceIndex { get; init; }
	public sbyte VolumeLeft { get; init; }
	public sbyte VolumeRight { get; init; }
	public ushort Pitch { get; init; }
	public byte SourceNumber { get; init; }
	public bool AdsrEnabled { get; init; }
	public int AttackRate { get; init; }
	public int DecayRate { get; init; }
	public int SustainRate { get; init; }
	public int SustainLevel { get; init; }
	public byte GainValue { get; init; }
	public bool HasEcho { get; init; }
	public bool HasPitchMod { get; init; }
	public bool UsesNoise { get; init; }

	/// <summary>
	/// Gets the frequency in Hz based on pitch value.
	/// </summary>
	public double FrequencyHz => Pitch * 32000.0 / 4096.0;

	/// <summary>
	/// Gets the approximate MIDI note number.
	/// </summary>
	public int ApproximateMidiNote {
		get {
			if (Pitch == 0) return 0;
			double freq = FrequencyHz;
			// MIDI note = 12 * log2(freq / 440) + 69
			return (int)Math.Round(12 * Math.Log2(freq / 440.0) + 69);
		}
	}
}
