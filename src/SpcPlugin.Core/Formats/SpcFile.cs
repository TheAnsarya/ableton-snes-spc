using System.Text;
using SpcPlugin.Core.Analysis;

namespace SpcPlugin.Core.Formats;

/// <summary>
/// Represents a complete SPC file with full import/export capabilities.
/// Supports both text and binary ID666 tag formats.
/// </summary>
public sealed class SpcFile {
	/// <summary>
	/// Standard SPC file size without extended info.
	/// </summary>
	public const int StandardFileSize = 0x10200;

	/// <summary>
	/// Extended SPC file size with extra metadata.
	/// </summary>
	public const int ExtendedFileSize = 0x10400;

	/// <summary>
	/// SPC file header signature.
	/// </summary>
	public const string HeaderSignature = "SNES-SPC700 Sound File Data v0.30";

	#region CPU State

	/// <summary>
	/// Program Counter (16-bit).
	/// </summary>
	public ushort PC { get; set; }

	/// <summary>
	/// Accumulator register.
	/// </summary>
	public byte A { get; set; }

	/// <summary>
	/// X index register.
	/// </summary>
	public byte X { get; set; }

	/// <summary>
	/// Y index register.
	/// </summary>
	public byte Y { get; set; }

	/// <summary>
	/// Program Status Word (flags).
	/// </summary>
	public byte PSW { get; set; }

	/// <summary>
	/// Stack Pointer.
	/// </summary>
	public byte SP { get; set; } = 0xEF; // Default stack location

	#endregion

	#region Memory

	/// <summary>
	/// 64KB Audio RAM.
	/// </summary>
	public byte[] Ram { get; } = new byte[0x10000];

	/// <summary>
	/// 128 DSP registers.
	/// </summary>
	public byte[] DspRegisters { get; } = new byte[128];

	/// <summary>
	/// 64 bytes extra RAM (rarely used).
	/// </summary>
	public byte[] ExtraRam { get; } = new byte[64];

	/// <summary>
	/// 64 bytes IPL ROM (bootstrap).
	/// </summary>
	public byte[] IplRom { get; } = new byte[64];

	#endregion

	#region ID666 Metadata

	/// <summary>
	/// Song title (max 32 chars).
	/// </summary>
	public string Title { get; set; } = "";

	/// <summary>
	/// Game title (max 32 chars).
	/// </summary>
	public string Game { get; set; } = "";

	/// <summary>
	/// Name of the person who dumped the SPC (max 16 chars).
	/// </summary>
	public string DumperName { get; set; } = "";

	/// <summary>
	/// Comments (max 32 chars).
	/// </summary>
	public string Comments { get; set; } = "";

	/// <summary>
	/// Artist/Composer name (max 32 chars).
	/// </summary>
	public string Artist { get; set; } = "";

	/// <summary>
	/// Date the SPC was dumped (YYYYMMDD format).
	/// </summary>
	public DateTime? DumpDate { get; set; }

	/// <summary>
	/// Song duration in seconds (before fade).
	/// </summary>
	public int DurationSeconds { get; set; }

	/// <summary>
	/// Fade out time in milliseconds.
	/// </summary>
	public int FadeMilliseconds { get; set; }

	/// <summary>
	/// Default channel disable flags (bit per channel).
	/// </summary>
	public byte ChannelDisables { get; set; }

	/// <summary>
	/// Emulator that was used to dump (0=unknown, 1=ZSNES, 2=Snes9x).
	/// </summary>
	public byte EmulatorUsed { get; set; }

	/// <summary>
	/// Whether ID666 tags are present in the file.
	/// </summary>
	public bool HasId666 { get; set; } = true;

	/// <summary>
	/// Use binary ID666 format (newer) instead of text format.
	/// </summary>
	public bool UseBinaryId666 { get; set; } = true;

	#endregion

	#region Analysis Results

	/// <summary>
	/// Detected sound driver type (populated after analysis).
	/// </summary>
	public SoundDriverType DetectedDriver { get; private set; }

	/// <summary>
	/// Extracted samples (populated after analysis).
	/// </summary>
	public List<SampleInfo> Samples { get; } = [];

	/// <summary>
	/// Memory usage analysis (populated after analysis).
	/// </summary>
	public MemoryUsage? MemoryAnalysis { get; private set; }

	#endregion

	/// <summary>
	/// Loads an SPC file from disk.
	/// </summary>
	/// <param name="path">Path to SPC file.</param>
	/// <returns>Loaded SPC file.</returns>
	public static SpcFile Load(string path) {
		byte[] data = File.ReadAllBytes(path);
		return Load(data);
	}

	/// <summary>
	/// Loads an SPC file from raw data.
	/// </summary>
	/// <param name="data">Raw SPC file bytes.</param>
	/// <returns>Loaded SPC file.</returns>
	public static SpcFile Load(ReadOnlySpan<byte> data) {
		if (data.Length < StandardFileSize) {
			throw new ArgumentException($"SPC file too small ({data.Length} bytes, need {StandardFileSize})", nameof(data));
		}

		// Validate header
		string header = Encoding.ASCII.GetString(data[..33]);
		if (!header.StartsWith("SNES-SPC700")) {
			throw new ArgumentException("Invalid SPC header signature", nameof(data));
		}

		var spc = new SpcFile();

		// Check for ID666 tag presence
		spc.HasId666 = data[0x23] == 0x1a;

		// Load CPU registers
		spc.PC = (ushort)(data[0x25] | (data[0x26] << 8));
		spc.A = data[0x27];
		spc.X = data[0x28];
		spc.Y = data[0x29];
		spc.PSW = data[0x2a];
		spc.SP = data[0x2b];

		// Load ID666 metadata if present
		if (spc.HasId666) {
			spc.LoadId666(data);
		}

		// Load RAM (64KB at offset 0x100)
		data.Slice(0x100, 0x10000).CopyTo(spc.Ram);

		// Load DSP registers (128 bytes at offset 0x10100)
		data.Slice(0x10100, 128).CopyTo(spc.DspRegisters);

		// Load extra RAM if present
		if (data.Length >= 0x10180 + 64) {
			data.Slice(0x10180, 64).CopyTo(spc.ExtraRam);
		}

		// Load IPL ROM if present
		if (data.Length >= 0x101c0 + 64) {
			data.Slice(0x101c0, 64).CopyTo(spc.IplRom);
		}

		return spc;
	}

	/// <summary>
	/// Saves the SPC file to disk.
	/// </summary>
	/// <param name="path">Output path.</param>
	public void Save(string path) {
		byte[] data = ToBytes();
		File.WriteAllBytes(path, data);
	}

	/// <summary>
	/// Converts to raw SPC file bytes.
	/// </summary>
	/// <returns>SPC file data.</returns>
	public byte[] ToBytes() {
		byte[] data = new byte[StandardFileSize];

		// Write header
		Encoding.ASCII.GetBytes(HeaderSignature).CopyTo(data.AsSpan());

		// Reserved bytes
		data[0x21] = 0x1a;
		data[0x22] = 0x1a;

		// ID666 presence flag
		data[0x23] = (byte)(HasId666 ? 0x1a : 0x1b);

		// Version minor
		data[0x24] = 30;

		// CPU registers
		data[0x25] = (byte)(PC & 0xff);
		data[0x26] = (byte)(PC >> 8);
		data[0x27] = A;
		data[0x28] = X;
		data[0x29] = Y;
		data[0x2a] = PSW;
		data[0x2b] = SP;

		// Write ID666 metadata
		if (HasId666) {
			WriteId666(data);
		}

		// Copy RAM
		Ram.CopyTo(data.AsSpan(0x100));

		// Copy DSP registers
		DspRegisters.CopyTo(data.AsSpan(0x10100));

		// Copy extra RAM
		ExtraRam.CopyTo(data.AsSpan(0x10180));

		// Copy IPL ROM
		IplRom.CopyTo(data.AsSpan(0x101c0));

		return data;
	}

	/// <summary>
	/// Analyzes the SPC to detect driver type and extract samples.
	/// </summary>
	public void Analyze() {
		var analyzer = new SpcAnalyzer();

		// Build minimal SPC data for analyzer
		byte[] spcData = new byte[StandardFileSize];
		Ram.CopyTo(spcData.AsSpan(0x100));
		DspRegisters.CopyTo(spcData.AsSpan(0x10100));

		analyzer.Analyze(spcData);

		DetectedDriver = analyzer.DriverType;
		Samples.Clear();
		Samples.AddRange(analyzer.Samples);
		MemoryAnalysis = analyzer.Memory;
	}

	/// <summary>
	/// Creates a copy of this SPC file.
	/// </summary>
	public SpcFile Clone() {
		var copy = new SpcFile {
			PC = PC,
			A = A,
			X = X,
			Y = Y,
			PSW = PSW,
			SP = SP,
			Title = Title,
			Game = Game,
			DumperName = DumperName,
			Comments = Comments,
			Artist = Artist,
			DumpDate = DumpDate,
			DurationSeconds = DurationSeconds,
			FadeMilliseconds = FadeMilliseconds,
			ChannelDisables = ChannelDisables,
			EmulatorUsed = EmulatorUsed,
			HasId666 = HasId666,
			UseBinaryId666 = UseBinaryId666,
		};

		Ram.CopyTo(copy.Ram, 0);
		DspRegisters.CopyTo(copy.DspRegisters, 0);
		ExtraRam.CopyTo(copy.ExtraRam, 0);
		IplRom.CopyTo(copy.IplRom, 0);

		return copy;
	}

	#region Private Methods

	private void LoadId666(ReadOnlySpan<byte> data) {
		// Detect text vs binary format
		// In binary format, offset 0x9e is a 32-bit date (YYYYMMDD)
		// In text format, it's an ASCII date string

		bool isBinary = DetectBinaryId666(data);
		UseBinaryId666 = isBinary;

		if (isBinary) {
			LoadBinaryId666(data);
		} else {
			LoadTextId666(data);
		}
	}

	private static bool DetectBinaryId666(ReadOnlySpan<byte> data) {
		// Check if the date field looks like binary (year > 1900)
		// Binary format stores date as 4-byte integer at offset 0x9e
		int dateValue = data[0x9e] | (data[0x9f] << 8) | (data[0xa0] << 16) | (data[0xa1] << 24);
		int year = dateValue / 10000;
		return year >= 1990 && year <= 2100;
	}

	private void LoadTextId666(ReadOnlySpan<byte> data) {
		Title = ReadString(data, 0x2e, 32);
		Game = ReadString(data, 0x4e, 32);
		DumperName = ReadString(data, 0x6e, 16);
		Comments = ReadString(data, 0x7e, 32);
		Artist = ReadString(data, 0xb1, 32);

		// Parse text date (DD/MM/YYYY or MM/DD/YYYY)
		string dateStr = ReadString(data, 0x9e, 11);
		if (DateTime.TryParse(dateStr, out var date)) {
			DumpDate = date;
		}

		// Parse play length (ASCII digits)
		string lenStr = ReadString(data, 0xa9, 3);
		if (int.TryParse(lenStr, out int len)) {
			DurationSeconds = len;
		}

		// Parse fade length (ASCII digits)
		string fadeStr = ReadString(data, 0xac, 5);
		if (int.TryParse(fadeStr, out int fade)) {
			FadeMilliseconds = fade;
		}

		ChannelDisables = data[0xd1];
		EmulatorUsed = data[0xd2];
	}

	private void LoadBinaryId666(ReadOnlySpan<byte> data) {
		Title = ReadString(data, 0x2e, 32);
		Game = ReadString(data, 0x4e, 32);
		DumperName = ReadString(data, 0x6e, 16);
		Comments = ReadString(data, 0x7e, 32);
		Artist = ReadString(data, 0xb0, 32);

		// Binary date (YYYYMMDD as 32-bit)
		int dateValue = data[0x9e] | (data[0x9f] << 8) | (data[0xa0] << 16) | (data[0xa1] << 24);
		if (dateValue > 0) {
			int year = dateValue / 10000;
			int month = (dateValue / 100) % 100;
			int day = dateValue % 100;
			if (year >= 1990 && year <= 2100 && month >= 1 && month <= 12 && day >= 1 && day <= 31) {
				try {
					DumpDate = new DateTime(year, month, day);
				} catch {
					// Invalid date
				}
			}
		}

		// Binary play length (24-bit, seconds)
		DurationSeconds = data[0xa9] | (data[0xaa] << 8) | (data[0xab] << 16);

		// Binary fade length (32-bit, ms)
		FadeMilliseconds = data[0xac] | (data[0xad] << 8) | (data[0xae] << 16) | (data[0xaf] << 24);

		ChannelDisables = data[0xd0];
		EmulatorUsed = data[0xd1];
	}

	private void WriteId666(byte[] data) {
		if (UseBinaryId666) {
			WriteBinaryId666(data);
		} else {
			WriteTextId666(data);
		}
	}

	private void WriteTextId666(byte[] data) {
		WriteString(data, 0x2e, 32, Title);
		WriteString(data, 0x4e, 32, Game);
		WriteString(data, 0x6e, 16, DumperName);
		WriteString(data, 0x7e, 32, Comments);
		WriteString(data, 0xb1, 32, Artist);

		// Text date
		if (DumpDate.HasValue) {
			string dateStr = DumpDate.Value.ToString("MM/dd/yyyy");
			WriteString(data, 0x9e, 11, dateStr);
		}

		// Play length (ASCII)
		WriteString(data, 0xa9, 3, DurationSeconds.ToString());

		// Fade length (ASCII)
		WriteString(data, 0xac, 5, FadeMilliseconds.ToString());

		data[0xd1] = ChannelDisables;
		data[0xd2] = EmulatorUsed;
	}

	private void WriteBinaryId666(byte[] data) {
		WriteString(data, 0x2e, 32, Title);
		WriteString(data, 0x4e, 32, Game);
		WriteString(data, 0x6e, 16, DumperName);
		WriteString(data, 0x7e, 32, Comments);
		WriteString(data, 0xb0, 32, Artist);

		// Binary date (YYYYMMDD)
		if (DumpDate.HasValue) {
			int dateValue = DumpDate.Value.Year * 10000 + DumpDate.Value.Month * 100 + DumpDate.Value.Day;
			data[0x9e] = (byte)(dateValue & 0xff);
			data[0x9f] = (byte)((dateValue >> 8) & 0xff);
			data[0xa0] = (byte)((dateValue >> 16) & 0xff);
			data[0xa1] = (byte)((dateValue >> 24) & 0xff);
		}

		// Binary play length (24-bit)
		data[0xa9] = (byte)(DurationSeconds & 0xff);
		data[0xaa] = (byte)((DurationSeconds >> 8) & 0xff);
		data[0xab] = (byte)((DurationSeconds >> 16) & 0xff);

		// Binary fade length (32-bit)
		data[0xac] = (byte)(FadeMilliseconds & 0xff);
		data[0xad] = (byte)((FadeMilliseconds >> 8) & 0xff);
		data[0xae] = (byte)((FadeMilliseconds >> 16) & 0xff);
		data[0xaf] = (byte)((FadeMilliseconds >> 24) & 0xff);

		data[0xd0] = ChannelDisables;
		data[0xd1] = EmulatorUsed;
	}

	private static string ReadString(ReadOnlySpan<byte> data, int offset, int maxLen) {
		int end = offset;
		while (end < offset + maxLen && end < data.Length && data[end] != 0) {
			end++;
		}
		return Encoding.ASCII.GetString(data[offset..end]).Trim();
	}

	private static void WriteString(byte[] data, int offset, int maxLen, string value) {
		value ??= "";
		byte[] bytes = Encoding.ASCII.GetBytes(value);
		int len = Math.Min(bytes.Length, maxLen - 1);
		Array.Copy(bytes, 0, data, offset, len);
		// Null terminate
		if (offset + len < data.Length) {
			data[offset + len] = 0;
		}
	}

	#endregion
}
