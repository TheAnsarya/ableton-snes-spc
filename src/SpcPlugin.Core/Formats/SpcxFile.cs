namespace SpcPlugin.Core.Formats;

using System.IO.Compression;
using System.Text.Json;

/// <summary>
/// SPCX project file format handler.
/// SPCX is a ZIP-based format containing SPC state, metadata, and editor settings.
/// </summary>
public sealed class SpcxFile {
	private const string ManifestPath = "manifest.json";
	private const string RamPath = "spc/ram.bin";
	private const string DspPath = "spc/dsp.bin";
	private const string MetadataPath = "metadata.json";

	/// <summary>
	/// Project manifest with version and content info.
	/// </summary>
	public SpcxManifest Manifest { get; set; } = new();

	/// <summary>
	/// Song metadata (title, artist, game, etc.).
	/// </summary>
	public SpcxMetadata Metadata { get; set; } = new();

	/// <summary>
	/// SPC700 64KB RAM contents.
	/// </summary>
	public byte[] Ram { get; set; } = new byte[0x10000];

	/// <summary>
	/// DSP register state (128 bytes).
	/// </summary>
	public byte[] DspRegisters { get; set; } = new byte[128];

	/// <summary>
	/// CPU register state.
	/// </summary>
	public SpcxCpuState CpuState { get; set; } = new();

	/// <summary>
	/// Loads an SPCX project file.
	/// </summary>
	/// <param name="path">Path to .spcx file.</param>
	/// <returns>Loaded project.</returns>
	public static SpcxFile Load(string path) {
		using var archive = ZipFile.OpenRead(path);
		var project = new SpcxFile();

		// Load manifest
		var manifestEntry = archive.GetEntry(ManifestPath)
			?? throw new InvalidDataException("Missing manifest.json");
		using (var stream = manifestEntry.Open()) {
			project.Manifest = JsonSerializer.Deserialize<SpcxManifest>(stream)
				?? throw new InvalidDataException("Invalid manifest.json");
		}

		// Validate version
		if (project.Manifest.Version > SpcxManifest.CurrentVersion) {
			throw new InvalidDataException(
				$"SPCX version {project.Manifest.Version} not supported (max: {SpcxManifest.CurrentVersion})");
		}

		// Load RAM
		var ramEntry = archive.GetEntry(RamPath);
		if (ramEntry != null) {
			using var stream = ramEntry.Open();
			stream.ReadExactly(project.Ram);
		}

		// Load DSP registers
		var dspEntry = archive.GetEntry(DspPath);
		if (dspEntry != null) {
			using var stream = dspEntry.Open();
			stream.ReadExactly(project.DspRegisters);
		}

		// Load metadata
		var metaEntry = archive.GetEntry(MetadataPath);
		if (metaEntry != null) {
			using var stream = metaEntry.Open();
			project.Metadata = JsonSerializer.Deserialize<SpcxMetadata>(stream)
				?? new SpcxMetadata();
		}

		return project;
	}

	/// <summary>
	/// Saves the project to an SPCX file.
	/// </summary>
	/// <param name="path">Output path.</param>
	public void Save(string path) {
		using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
		var options = new JsonSerializerOptions { WriteIndented = true };

		// Write manifest
		Manifest.ModifiedDate = DateTime.UtcNow;
		var manifestEntry = archive.CreateEntry(ManifestPath);
		using (var stream = manifestEntry.Open()) {
			JsonSerializer.Serialize(stream, Manifest, options);
		}

		// Write RAM
		var ramEntry = archive.CreateEntry(RamPath, CompressionLevel.Optimal);
		using (var stream = ramEntry.Open()) {
			stream.Write(Ram);
		}

		// Write DSP registers
		var dspEntry = archive.CreateEntry(DspPath);
		using (var stream = dspEntry.Open()) {
			stream.Write(DspRegisters);
		}

		// Write metadata
		var metaEntry = archive.CreateEntry(MetadataPath);
		using (var stream = metaEntry.Open()) {
			JsonSerializer.Serialize(stream, Metadata, options);
		}
	}

	/// <summary>
	/// Creates an SPCX project from an SPC file.
	/// </summary>
	/// <param name="spcPath">Path to source SPC file.</param>
	/// <returns>New SPCX project.</returns>
	public static SpcxFile ImportFromSpc(string spcPath) {
		byte[] spcData = File.ReadAllBytes(spcPath);
		return ImportFromSpc(spcData, Path.GetFileNameWithoutExtension(spcPath));
	}

	/// <summary>
	/// Creates an SPCX project from SPC data.
	/// </summary>
	/// <param name="spcData">Raw SPC file data.</param>
	/// <param name="name">Project name.</param>
	/// <returns>New SPCX project.</returns>
	public static SpcxFile ImportFromSpc(ReadOnlySpan<byte> spcData, string name) {
		if (spcData.Length < 0x10200) {
			throw new ArgumentException("Invalid SPC file", nameof(spcData));
		}

		var project = new SpcxFile {
			Manifest = {
				Name = name,
				CreatedDate = DateTime.UtcNow,
				ModifiedDate = DateTime.UtcNow,
			},
		};

		// Copy RAM (64KB at offset 0x100)
		spcData.Slice(0x100, 0x10000).CopyTo(project.Ram);

		// Copy DSP registers (128 bytes at offset 0x10100)
		spcData.Slice(0x10100, 128).CopyTo(project.DspRegisters);

		// Extract CPU state from SPC header
		project.CpuState = new SpcxCpuState {
			PC = (ushort)(spcData[0x25] | (spcData[0x26] << 8)),
			A = spcData[0x27],
			X = spcData[0x28],
			Y = spcData[0x29],
			PSW = spcData[0x2a],
			SP = spcData[0x2b],
		};

		// Extract ID666 metadata if present
		if (spcData[0x23] == 0x1a) {
			project.Metadata = ExtractId666(spcData);
		}

		return project;
	}

	/// <summary>
	/// Exports the project as a standard SPC file.
	/// </summary>
	/// <param name="path">Output path.</param>
	public void ExportToSpc(string path) {
		byte[] spcData = new byte[0x10200];

		// Write header
		spcData[0] = (byte)'S';
		spcData[1] = (byte)'N';
		spcData[2] = (byte)'E';
		spcData[3] = (byte)'S';
		spcData[4] = (byte)'-';
		spcData[5] = (byte)'S';
		spcData[6] = (byte)'P';
		spcData[7] = (byte)'C';
		spcData[8] = (byte)'7';
		spcData[9] = (byte)'0';
		spcData[10] = (byte)'0';
		spcData[11] = (byte)' ';
		spcData[12] = (byte)'S';
		spcData[13] = (byte)'o';
		spcData[14] = (byte)'u';
		spcData[15] = (byte)'n';
		spcData[16] = (byte)'d';
		spcData[17] = (byte)' ';
		spcData[18] = (byte)'F';
		spcData[19] = (byte)'i';
		spcData[20] = (byte)'l';
		spcData[21] = (byte)'e';
		spcData[22] = (byte)' ';
		spcData[23] = (byte)'D';
		spcData[24] = (byte)'a';
		spcData[25] = (byte)'t';
		spcData[26] = (byte)'a';
		spcData[27] = (byte)' ';
		spcData[28] = (byte)'v';
		spcData[29] = (byte)'0';
		spcData[30] = (byte)'.';
		spcData[31] = (byte)'3';
		spcData[32] = (byte)'0';

		// Mark as having ID666 tag
		spcData[0x23] = 0x1a;

		// CPU registers
		spcData[0x25] = (byte)(CpuState.PC & 0xff);
		spcData[0x26] = (byte)(CpuState.PC >> 8);
		spcData[0x27] = CpuState.A;
		spcData[0x28] = CpuState.X;
		spcData[0x29] = CpuState.Y;
		spcData[0x2a] = CpuState.PSW;
		spcData[0x2b] = CpuState.SP;

		// Copy RAM
		Ram.CopyTo(spcData.AsSpan(0x100));

		// Copy DSP registers
		DspRegisters.CopyTo(spcData.AsSpan(0x10100));

		File.WriteAllBytes(path, spcData);
	}

	private static SpcxMetadata ExtractId666(ReadOnlySpan<byte> spcData) {
		static string ReadString(ReadOnlySpan<byte> data, int offset, int maxLen) {
			int end = offset;
			while (end < offset + maxLen && data[end] != 0) end++;
			return System.Text.Encoding.ASCII.GetString(data[offset..end]).Trim();
		}

		return new SpcxMetadata {
			Title = ReadString(spcData, 0x2e, 32),
			Game = ReadString(spcData, 0x4e, 32),
			DumperName = ReadString(spcData, 0x6e, 16),
			Comments = ReadString(spcData, 0x7e, 32),
			Artist = ReadString(spcData, 0xb1, 32),
		};
	}
}

/// <summary>
/// SPCX manifest containing project info.
/// </summary>
public sealed class SpcxManifest {
	public const int CurrentVersion = 1;

	public int Version { get; set; } = CurrentVersion;
	public string Name { get; set; } = "";
	public string? Description { get; set; }
	public DateTime CreatedDate { get; set; }
	public DateTime ModifiedDate { get; set; }
}

/// <summary>
/// Song metadata compatible with ID666 tags.
/// </summary>
public sealed class SpcxMetadata {
	public string? Title { get; set; }
	public string? Artist { get; set; }
	public string? Game { get; set; }
	public string? DumperName { get; set; }
	public string? Comments { get; set; }
	public int? DurationMs { get; set; }
	public int? FadeMs { get; set; }
}

/// <summary>
/// SPC700 CPU register state.
/// </summary>
public sealed class SpcxCpuState {
	public ushort PC { get; set; }
	public byte A { get; set; }
	public byte X { get; set; }
	public byte Y { get; set; }
	public byte PSW { get; set; }
	public byte SP { get; set; }
}
