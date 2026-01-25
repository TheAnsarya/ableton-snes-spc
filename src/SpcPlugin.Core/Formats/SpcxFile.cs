
using System.IO.Compression;
using System.Text.Json;
using SpcPlugin.Core.Analysis;

namespace SpcPlugin.Core.Formats;
/// <summary>
/// SPCX project file format handler.
/// SPCX is a ZIP-based format containing SPC state, metadata, and editor settings.
/// </summary>
public sealed class SpcxFile {
	private const string ManifestPath = "manifest.json";
	private const string RamPath = "spc/ram.bin";
	private const string DspPath = "spc/dsp.bin";
	private const string CpuPath = "spc/cpu.json";
	private const string MetadataPath = "metadata.json";
	private const string SettingsPath = "settings.json";
	private const string AnalysisPath = "analysis.json";

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
	/// Editor settings and preferences.
	/// </summary>
	public SpcxEditorSettings Settings { get; set; } = new();

	/// <summary>
	/// Cached analysis results (driver detection, samples, etc.).
	/// </summary>
	public SpcxAnalysisResult? Analysis { get; set; }

	/// <summary>
	/// Loads an SPCX project file.
	/// </summary>
	/// <param name="path">Path to .spcx file.</param>
	/// <returns>Loaded project.</returns>
	public static SpcxFile Load(string path) {
		using var archive = ZipFile.OpenRead(path);
		var project = new SpcxFile();
		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

		// Load manifest
		var manifestEntry = archive.GetEntry(ManifestPath)
			?? throw new InvalidDataException("Missing manifest.json");
		using (var stream = manifestEntry.Open()) {
			project.Manifest = JsonSerializer.Deserialize<SpcxManifest>(stream, options)
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

		// Load CPU state
		var cpuEntry = archive.GetEntry(CpuPath);
		if (cpuEntry != null) {
			using var stream = cpuEntry.Open();
			project.CpuState = JsonSerializer.Deserialize<SpcxCpuState>(stream, options)
				?? new SpcxCpuState();
		}

		// Load metadata
		var metaEntry = archive.GetEntry(MetadataPath);
		if (metaEntry != null) {
			using var stream = metaEntry.Open();
			project.Metadata = JsonSerializer.Deserialize<SpcxMetadata>(stream, options)
				?? new SpcxMetadata();
		}

		// Load editor settings
		var settingsEntry = archive.GetEntry(SettingsPath);
		if (settingsEntry != null) {
			using var stream = settingsEntry.Open();
			project.Settings = JsonSerializer.Deserialize<SpcxEditorSettings>(stream, options)
				?? new SpcxEditorSettings();
		}

		// Load analysis results
		var analysisEntry = archive.GetEntry(AnalysisPath);
		if (analysisEntry != null) {
			using var stream = analysisEntry.Open();
			project.Analysis = JsonSerializer.Deserialize<SpcxAnalysisResult>(stream, options);
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

		// Write CPU state
		var cpuEntry = archive.CreateEntry(CpuPath);
		using (var stream = cpuEntry.Open()) {
			JsonSerializer.Serialize(stream, CpuState, options);
		}

		// Write metadata
		var metaEntry = archive.CreateEntry(MetadataPath);
		using (var stream = metaEntry.Open()) {
			JsonSerializer.Serialize(stream, Metadata, options);
		}

		// Write editor settings
		var settingsEntry = archive.CreateEntry(SettingsPath);
		using (var stream = settingsEntry.Open()) {
			JsonSerializer.Serialize(stream, Settings, options);
		}

		// Write analysis results if available
		if (Analysis != null) {
			var analysisEntry = archive.CreateEntry(AnalysisPath);
			using var stream = analysisEntry.Open();
			JsonSerializer.Serialize(stream, Analysis, options);
		}
	}

	/// <summary>
	/// Creates an SPCX project from an SPC file.
	/// </summary>
	/// <param name="spcPath">Path to source SPC file.</param>
	/// <param name="analyze">Whether to run analysis on import.</param>
	/// <returns>New SPCX project.</returns>
	public static SpcxFile ImportFromSpc(string spcPath, bool analyze = true) {
		var spcFile = SpcFile.Load(spcPath);
		return ImportFromSpc(spcFile, Path.GetFileNameWithoutExtension(spcPath), analyze);
	}

	/// <summary>
	/// Creates an SPCX project from SPC data.
	/// </summary>
	/// <param name="spcData">Raw SPC file data.</param>
	/// <param name="name">Project name.</param>
	/// <param name="analyze">Whether to run analysis on import.</param>
	/// <returns>New SPCX project.</returns>
	public static SpcxFile ImportFromSpc(ReadOnlySpan<byte> spcData, string name, bool analyze = true) {
		var spcFile = SpcFile.Load(spcData);
		return ImportFromSpc(spcFile, name, analyze);
	}

	/// <summary>
	/// Creates an SPCX project from an SpcFile object.
	/// </summary>
	/// <param name="spcFile">Loaded SPC file.</param>
	/// <param name="name">Project name.</param>
	/// <param name="analyze">Whether to run analysis.</param>
	/// <returns>New SPCX project.</returns>
	public static SpcxFile ImportFromSpc(SpcFile spcFile, string name, bool analyze = true) {
		var project = new SpcxFile {
			Manifest = {
				Name = name,
				CreatedDate = DateTime.UtcNow,
				ModifiedDate = DateTime.UtcNow,
			},
		};

		// Copy RAM and DSP
		spcFile.Ram.CopyTo(project.Ram, 0);
		spcFile.DspRegisters.CopyTo(project.DspRegisters, 0);

		// Copy CPU state
		project.CpuState = new SpcxCpuState {
			PC = spcFile.PC,
			A = spcFile.A,
			X = spcFile.X,
			Y = spcFile.Y,
			PSW = spcFile.PSW,
			SP = spcFile.SP,
		};

		// Copy metadata
		project.Metadata = new SpcxMetadata {
			Title = spcFile.Title,
			Game = spcFile.Game,
			Artist = spcFile.Artist,
			DumperName = spcFile.DumperName,
			Comments = spcFile.Comments,
			DurationMs = spcFile.DurationSeconds * 1000,
			FadeMs = spcFile.FadeMilliseconds,
		};

		// Run analysis if requested
		if (analyze) {
			project.RunAnalysis();
		}

		return project;
	}

	/// <summary>
	/// Runs analysis on the loaded SPC data to detect driver and extract samples.
	/// </summary>
	public void RunAnalysis() {
		var analyzer = new SpcAnalyzer();

		// Build SPC data for analyzer
		byte[] spcData = new byte[SpcFile.StandardFileSize];
		Ram.CopyTo(spcData.AsSpan(0x100));
		DspRegisters.CopyTo(spcData.AsSpan(0x10100));

		analyzer.Analyze(spcData);

		Analysis = new SpcxAnalysisResult {
			DetectedDriver = analyzer.DriverType.ToString(),
			SampleCount = analyzer.Samples.Count,
			Samples = analyzer.Samples.Select(s => new SpcxSampleInfo {
				Index = s.Index,
				StartAddress = s.StartAddress,
				EndAddress = s.EndAddress,
				LoopAddress = s.LoopAddress,
				Size = s.Size,
				HasLoop = s.HasLoop,
			}).ToList(),
			MemoryUsage = new SpcxMemoryUsage {
				SampleBytes = analyzer.Memory.SampleBytes,
				EchoBytes = analyzer.Memory.EchoBytes,
				DriverBytes = analyzer.Memory.DriverBytes,
				FreeBytes = analyzer.Memory.FreeBytes,
			},
		};
	}

	/// <summary>
	/// Exports the project as a standard SPC file.
	/// </summary>
	/// <param name="path">Output path.</param>
	public void ExportToSpc(string path) {
		var spcFile = ToSpcFile();
		spcFile.Save(path);
	}

	/// <summary>
	/// Exports the project as raw SPC data.
	/// </summary>
	/// <returns>SPC file bytes.</returns>
	public byte[] ExportToSpcBytes() {
		var spcFile = ToSpcFile();
		return spcFile.ToBytes();
	}

	/// <summary>
	/// Converts the project to an SpcFile object.
	/// </summary>
	/// <returns>SpcFile with project data.</returns>
	public SpcFile ToSpcFile() {
		var spcFile = new SpcFile {
			// CPU state
			PC = CpuState.PC,
			A = CpuState.A,
			X = CpuState.X,
			Y = CpuState.Y,
			PSW = CpuState.PSW,
			SP = CpuState.SP,

			// Metadata
			Title = Metadata.Title ?? "",
			Game = Metadata.Game ?? "",
			Artist = Metadata.Artist ?? "",
			DumperName = Metadata.DumperName ?? "",
			Comments = Metadata.Comments ?? "",
			DurationSeconds = (Metadata.DurationMs ?? 0) / 1000,
			FadeMilliseconds = Metadata.FadeMs ?? 0,
			HasId666 = true,
			UseBinaryId666 = true,
		};

		// Copy RAM and DSP
		Ram.CopyTo(spcFile.Ram, 0);
		DspRegisters.CopyTo(spcFile.DspRegisters, 0);

		return spcFile;
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
	public byte SP { get; set; } = 0xEF;
}

/// <summary>
/// Editor settings saved with the project.
/// </summary>
public sealed class SpcxEditorSettings {
	/// <summary>
	/// Voice mute states (8 voices).
	/// </summary>
	public bool[] VoiceMutes { get; set; } = new bool[8];

	/// <summary>
	/// Voice solo states (8 voices).
	/// </summary>
	public bool[] VoiceSolos { get; set; } = new bool[8];

	/// <summary>
	/// Voice volume levels (8 voices, 0.0-1.0).
	/// </summary>
	public float[] VoiceVolumes { get; set; } = [1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f];

	/// <summary>
	/// Master volume (0.0-1.0).
	/// </summary>
	public float MasterVolume { get; set; } = 1.0f;

	/// <summary>
	/// Whether looping is enabled.
	/// </summary>
	public bool LoopEnabled { get; set; } = true;

	/// <summary>
	/// Playback position in samples.
	/// </summary>
	public long PlaybackPosition { get; set; }

	/// <summary>
	/// Currently active view/tab index.
	/// </summary>
	public int ActiveViewIndex { get; set; }

	/// <summary>
	/// Waveform view zoom level.
	/// </summary>
	public float WaveformZoom { get; set; } = 1.0f;

	/// <summary>
	/// Selected sample index for editing.
	/// </summary>
	public int SelectedSampleIndex { get; set; } = -1;
}

/// <summary>
/// Cached analysis results for the project.
/// </summary>
public sealed class SpcxAnalysisResult {
	/// <summary>
	/// Detected sound driver name.
	/// </summary>
	public string DetectedDriver { get; set; } = "Unknown";

	/// <summary>
	/// Number of samples found.
	/// </summary>
	public int SampleCount { get; set; }

	/// <summary>
	/// Extracted sample information.
	/// </summary>
	public List<SpcxSampleInfo> Samples { get; set; } = [];

	/// <summary>
	/// Memory usage breakdown.
	/// </summary>
	public SpcxMemoryUsage? MemoryUsage { get; set; }
}

/// <summary>
/// Sample information for SPCX storage.
/// </summary>
public sealed class SpcxSampleInfo {
	public int Index { get; set; }
	public int StartAddress { get; set; }
	public int EndAddress { get; set; }
	public int LoopAddress { get; set; }
	public int Size { get; set; }
	public bool HasLoop { get; set; }
}

/// <summary>
/// Memory usage information for SPCX storage.
/// </summary>
public sealed class SpcxMemoryUsage {
	public int SampleBytes { get; set; }
	public int EchoBytes { get; set; }
	public int DriverBytes { get; set; }
	public int FreeBytes { get; set; }
}
