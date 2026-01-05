using System.ComponentModel;
using System.Runtime.CompilerServices;
using SpcPlugin.Core.Audio;
using SpcPlugin.Core.Editing;
using SpcPlugin.Core.Midi;
using SpcPlugin.Core.Presets;
using SpcPlugin.Core.Project;

namespace SpcPlugin.Core.UI;

/// <summary>
/// Main view model for the SPC Plugin UI.
/// Implements INotifyPropertyChanged for data binding in UI frameworks.
/// </summary>
public class PluginViewModel : INotifyPropertyChanged, IDisposable {
	private List<SpcPreset> _presets = [];
	private ProjectManager? _projectManager;

	public event PropertyChangedEventHandler? PropertyChanged;

	public PluginViewModel(SpcEngine engine) {
		Engine = engine;
		MidiProcessor = new MidiProcessor(engine);
		PresetManager = new PresetManager(engine, MidiProcessor);

		// Initialize voice view models
		for (int i = 0; i < 8; i++) {
			Voices[i] = new VoiceViewModel(engine, i);
		}

		// Load factory presets
		_presets = PresetManager.GetFactoryPresets();
	}

	#region Properties

	/// <summary>
	/// The underlying SPC engine.
	/// </summary>
	public SpcEngine Engine { get; }

	/// <summary>
	/// The project manager for undo/redo and persistence.
	/// </summary>
	public ProjectManager? ProjectManager => _projectManager;

	/// <summary>
	/// The MIDI processor.
	/// </summary>
	public MidiProcessor MidiProcessor { get; }

	/// <summary>
	/// The preset manager.
	/// </summary>
	public PresetManager PresetManager { get; }

	/// <summary>
	/// Whether an SPC file is loaded.
	/// </summary>
	public bool IsLoaded { get; private set => SetField(ref field, value); }

	/// <summary>
	/// Path to the currently loaded SPC file.
	/// </summary>
	public string SpcFilePath { get; private set => SetField(ref field, value); } = "";

	/// <summary>
	/// Song title from ID666 tag.
	/// </summary>
	public string SongTitle { get; private set => SetField(ref field, value); } = "";

	/// <summary>
	/// Game title from ID666 tag.
	/// </summary>
	public string GameTitle { get; private set => SetField(ref field, value); } = "";

	/// <summary>
	/// Artist name from ID666 tag.
	/// </summary>
	public string Artist { get; private set => SetField(ref field, value); } = "";

	/// <summary>
	/// Whether playback is active.
	/// </summary>
	public bool IsPlaying {
		get => Engine.IsPlaying;
		set {
			if (value) Engine.Play();
			else Engine.Pause();
			OnPropertyChanged();
		}
	}

	/// <summary>
	/// Loop enable state.
	/// </summary>
	public bool LoopEnabled {
		get => Engine.LoopEnabled;
		set {
			Engine.LoopEnabled = value;
			OnPropertyChanged();
		}
	}

	/// <summary>
	/// Master volume (0-200%).
	/// </summary>
	public float MasterVolume {
		get => Engine.MasterVolume;
		set {
			Engine.MasterVolume = Math.Clamp(value, 0f, 2f);
			OnPropertyChanged();
			OnPropertyChanged(nameof(MasterVolumePercent));
		}
	}

	/// <summary>
	/// Master volume as percentage string.
	/// </summary>
	public string MasterVolumePercent => $"{MasterVolume * 100:F0}%";

	/// <summary>
	/// Current playback position in seconds.
	/// </summary>
	public double PositionSeconds => Engine.PositionSeconds;

	/// <summary>
	/// Position formatted as MM:SS.
	/// </summary>
	public string PositionFormatted {
		get {
			var ts = TimeSpan.FromSeconds(PositionSeconds);
			return $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}";
		}
	}

	/// <summary>
	/// Currently selected voice index (0-7).
	/// </summary>
	public int SelectedVoice { get; set {
			if (value >= 0 && value < 8) {
				SetField(ref field, value);
				OnPropertyChanged(nameof(SelectedVoiceViewModel));
			}
		} }

	/// <summary>
	/// View model for the selected voice.
	/// </summary>
	public VoiceViewModel SelectedVoiceViewModel => Voices[SelectedVoice];

	/// <summary>
	/// Voice view models for all 8 voices.
	/// </summary>
	public VoiceViewModel[] Voices { get; } = new VoiceViewModel[8];

	/// <summary>
	/// Available presets.
	/// </summary>
	public List<SpcPreset> Presets {
		get => _presets;
		set => SetField(ref _presets, value);
	}

	/// <summary>
	/// Selected preset index.
	/// </summary>
	public int SelectedPresetIndex { get; set {
			if (SetField(ref field, value) && value >= 0 && value < _presets.Count) {
				PresetManager.ApplyPreset(_presets[value]);
				RefreshAll();
			}
		} }

	/// <summary>
	/// DSP settings view model.
	/// </summary>
	public DspViewModel Dsp { get; private set; } = null!;

	/// <summary>
	/// Whether undo is available.
	/// </summary>
	public bool CanUndo => _projectManager?.CanUndo ?? false;

	/// <summary>
	/// Whether redo is available.
	/// </summary>
	public bool CanRedo => _projectManager?.CanRedo ?? false;

	/// <summary>
	/// Whether there are unsaved changes.
	/// </summary>
	public bool HasUnsavedChanges => _projectManager?.HasUnsavedChanges ?? false;

	/// <summary>
	/// The current project file path.
	/// </summary>
	public string? ProjectPath => _projectManager?.CurrentPath;

	#endregion

	#region Commands

	/// <summary>
	/// Loads an SPC file.
	/// </summary>
	public void LoadSpc(string filePath) {
		try {
			Engine.LoadSpcFile(filePath);
			SpcFilePath = filePath;
			IsLoaded = true;

			// Parse ID666 metadata
			ParseMetadata();

			// Initialize DSP view model
			if (Engine.Editor != null) {
				Dsp = new DspViewModel(Engine.Editor);
			}

			RefreshAll();
		} catch (Exception ex) {
			LastError = ex.Message;
			IsLoaded = false;
		}
	}

	/// <summary>
	/// Loads SPC data from bytes.
	/// </summary>
	public void LoadSpcData(byte[] data) {
		try {
			Engine.LoadSpc(data);
			SpcFilePath = "";
			IsLoaded = true;
			ParseMetadata();

			if (Engine.Editor != null) {
				Dsp = new DspViewModel(Engine.Editor);
			}

			RefreshAll();
		} catch (Exception ex) {
			LastError = ex.Message;
			IsLoaded = false;
		}
	}

	/// <summary>
	/// Play command.
	/// </summary>
	public void Play() => IsPlaying = true;

	/// <summary>
	/// Pause command.
	/// </summary>
	public void Pause() => IsPlaying = false;

	/// <summary>
	/// Stop command.
	/// </summary>
	public void Stop() {
		Engine.Stop();
		OnPropertyChanged(nameof(IsPlaying));
		OnPropertyChanged(nameof(PositionSeconds));
		OnPropertyChanged(nameof(PositionFormatted));
	}

	/// <summary>
	/// Toggle play/pause.
	/// </summary>
	public void TogglePlayPause() => IsPlaying = !IsPlaying;

	/// <summary>
	/// Seek to position.
	/// </summary>
	public void Seek(double seconds) {
		Engine.Seek(seconds);
		OnPropertyChanged(nameof(PositionSeconds));
		OnPropertyChanged(nameof(PositionFormatted));
	}

	/// <summary>
	/// Mute all voices.
	/// </summary>
	public void MuteAll() {
		Engine.MuteAll();
		foreach (var v in Voices) v.Refresh();
	}

	/// <summary>
	/// Unmute all voices.
	/// </summary>
	public void UnmuteAll() {
		Engine.UnmuteAll();
		foreach (var v in Voices) v.Refresh();
	}

	/// <summary>
	/// Clear all solo states.
	/// </summary>
	public void ClearSolo() {
		Engine.ClearSolo();
		foreach (var v in Voices) v.Refresh();
	}

	/// <summary>
	/// Saves the current state as a preset.
	/// </summary>
	public SpcPreset SaveAsPreset(string name) {
		var preset = PresetManager.CaptureCurrentState(name);
		_presets.Add(preset);
		OnPropertyChanged(nameof(Presets));
		return preset;
	}

	/// <summary>
	/// Export audio to WAV file.
	/// </summary>
	public void ExportToWav(string outputPath, double duration, double fadeOut = 2.0) {
		if (Engine.Editor == null) return;

		// Get current state as SPC data
		var spcData = Engine.Editor.ExportSpc();
		SpcExporter.ExportToWav(spcData, outputPath, duration, 44100, fadeOut);
	}

	/// <summary>
	/// Creates a new project.
	/// </summary>
	public void NewProject(string name = "Untitled") {
		_projectManager?.Dispose();
		_projectManager = new ProjectManager();
		_projectManager.NewProject(name);
		_projectManager.UndoRedoStateChanged += OnUndoRedoStateChanged;
		_projectManager.ProjectChanged += OnProjectManagerChanged;
		NotifyUndoRedoState();
	}

	/// <summary>
	/// Saves the project to the current path.
	/// </summary>
	public bool SaveProject() {
		if (_projectManager == null || _projectManager.CurrentPath == null) {
			return false;
		}

		_projectManager.SaveProject();
		OnPropertyChanged(nameof(HasUnsavedChanges));
		return true;
	}

	/// <summary>
	/// Saves the project to a new path.
	/// </summary>
	public void SaveProjectAs(string path) {
		_projectManager?.SaveProjectAs(path);
		OnPropertyChanged(nameof(HasUnsavedChanges));
		OnPropertyChanged(nameof(ProjectPath));
	}

	/// <summary>
	/// Loads a project from disk.
	/// </summary>
	public void LoadProject(string path) {
		_projectManager?.Dispose();
		_projectManager = new ProjectManager();
		_projectManager.LoadProject(path);
		_projectManager.UndoRedoStateChanged += OnUndoRedoStateChanged;
		_projectManager.ProjectChanged += OnProjectManagerChanged;
		NotifyUndoRedoState();

		// If project has embedded SPC data, sync with engine
		if (_projectManager.CurrentProject?.Ram != null) {
			SyncEngineFromProject();
		}
	}

	/// <summary>
	/// Imports an SPC file into a new project.
	/// </summary>
	public void ImportSpcToProject(string spcPath, string? projectName = null) {
		_projectManager?.Dispose();
		_projectManager = new ProjectManager();
		_projectManager.ImportSpc(spcPath);
		if (projectName != null && _projectManager.CurrentProject != null) {
			_projectManager.CurrentProject.Manifest.Name = projectName;
		}
		_projectManager.UndoRedoStateChanged += OnUndoRedoStateChanged;
		_projectManager.ProjectChanged += OnProjectManagerChanged;
		NotifyUndoRedoState();

		SyncEngineFromProject();
	}

	/// <summary>
	/// Undoes the last operation.
	/// </summary>
	public void Undo() {
		if (_projectManager?.CanUndo != true) return;

		_projectManager.Undo();
		SyncEngineFromProject();
		RefreshAll();
	}

	/// <summary>
	/// Redoes the last undone operation.
	/// </summary>
	public void Redo() {
		if (_projectManager?.CanRedo != true) return;

		_projectManager.Redo();
		SyncEngineFromProject();
		RefreshAll();
	}

	/// <summary>
	/// Syncs the engine state from the project manager's data.
	/// </summary>
	private void SyncEngineFromProject() {
		if (_projectManager?.CurrentProject == null || Engine.Editor == null) return;

		// Copy RAM and DSP registers from project to engine
		var project = _projectManager.CurrentProject;
		project.Ram.AsSpan().CopyTo(Engine.Editor.Ram);
		project.DspRegisters.AsSpan().CopyTo(Engine.Editor.DspRegisters);
	}

	/// <summary>
	/// Syncs the project manager's data from the engine state.
	/// </summary>
	private void SyncProjectFromEngine() {
		if (_projectManager?.CurrentProject == null || Engine.Editor == null) return;

		// This would be called after editing to update project state
		var project = _projectManager.CurrentProject;
		Engine.Editor.Ram.CopyTo(project.Ram);
		Engine.Editor.DspRegisters.CopyTo(project.DspRegisters);
	}

	private void OnUndoRedoStateChanged(object? sender, EventArgs e) {
		NotifyUndoRedoState();
	}

	private void OnProjectManagerChanged(object? sender, ProjectChangedEventArgs e) {
		OnPropertyChanged(nameof(HasUnsavedChanges));
	}

	private void NotifyUndoRedoState() {
		OnPropertyChanged(nameof(CanUndo));
		OnPropertyChanged(nameof(CanRedo));
		OnPropertyChanged(nameof(HasUnsavedChanges));
	}

	/// <summary>
	/// Last error message.
	/// </summary>
	public string LastError { get; private set; } = "";

	#endregion

	#region Internal

	private void ParseMetadata() {
		// Read ID666 metadata from SPC
		if (Engine.Editor == null) return;

		var ram = Engine.Editor.Ram;
		// ID666 is at offset 0x2E-0xAD in original SPC file
		// We'd need to store the original header or re-read from file
		SongTitle = "Unknown";
		GameTitle = "Unknown";
		Artist = "Unknown";
	}

	/// <summary>
	/// Refreshes all bound properties.
	/// </summary>
	public void RefreshAll() {
		OnPropertyChanged(nameof(IsPlaying));
		OnPropertyChanged(nameof(LoopEnabled));
		OnPropertyChanged(nameof(MasterVolume));
		OnPropertyChanged(nameof(MasterVolumePercent));
		OnPropertyChanged(nameof(PositionSeconds));
		OnPropertyChanged(nameof(PositionFormatted));

		foreach (var v in Voices) {
			v.Refresh();
		}

		Dsp?.Refresh();
	}

	/// <summary>
	/// Called periodically to update time-based properties.
	/// </summary>
	public void UpdateTime() {
		OnPropertyChanged(nameof(PositionSeconds));
		OnPropertyChanged(nameof(PositionFormatted));
	}

	protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
		if (EqualityComparer<T>.Default.Equals(field, value)) return false;
		field = value;
		OnPropertyChanged(propertyName);
		return true;
	}

	public void Dispose() {
		if (_projectManager != null) {
			_projectManager.UndoRedoStateChanged -= OnUndoRedoStateChanged;
			_projectManager.ProjectChanged -= OnProjectManagerChanged;
			_projectManager.Dispose();
		}
		Engine.Dispose();
	}

	#endregion
}

/// <summary>
/// View model for a single voice channel.
/// </summary>
public class VoiceViewModel : INotifyPropertyChanged {
	private readonly SpcEngine _engine;

	public event PropertyChangedEventHandler? PropertyChanged;

	public VoiceViewModel(SpcEngine engine, int voiceIndex) {
		_engine = engine;
		Index = voiceIndex;
	}

	/// <summary>
	/// Voice index (0-7).
	/// </summary>
	public int Index { get; }

	/// <summary>
	/// Display name.
	/// </summary>
	public string Name => $"Voice {Index + 1}";

	/// <summary>
	/// Whether the voice is muted.
	/// </summary>
	public bool IsMuted {
		get => _engine.GetVoiceMuted(Index);
		set {
			_engine.SetVoiceMuted(Index, value);
			OnPropertyChanged();
		}
	}

	/// <summary>
	/// Whether the voice is soloed.
	/// </summary>
	public bool IsSolo {
		get => _engine.GetVoiceSolo(Index);
		set {
			_engine.SetVoiceSolo(Index, value);
			OnPropertyChanged();
		}
	}

	/// <summary>
	/// Voice volume (0-1).
	/// </summary>
	public float Volume {
		get => _engine.GetVoiceVolume(Index);
		set {
			_engine.SetVoiceVolume(Index, Math.Clamp(value, 0f, 1f));
			OnPropertyChanged();
			OnPropertyChanged(nameof(VolumePercent));
		}
	}

	/// <summary>
	/// Volume as percentage string.
	/// </summary>
	public string VolumePercent => $"{Volume * 100:F0}%";

	/// <summary>
	/// Source (sample) number being played.
	/// </summary>
	public int SourceNumber {
		get {
			if (_engine.Editor == null) return 0;
			return _engine.Editor.GetVoiceInfo(Index).SourceNumber;
		}
	}

	/// <summary>
	/// Current pitch value.
	/// </summary>
	public int Pitch {
		get {
			if (_engine.Editor == null) return 0;
			return _engine.Editor.GetVoiceInfo(Index).Pitch;
		}
	}

	/// <summary>
	/// Envelope level (0-127).
	/// </summary>
	public int EnvelopeLevel {
		get {
			if (_engine.Editor == null) return 0;
			return _engine.Editor.GetVoiceInfo(Index).EnvX;
		}
	}

	/// <summary>
	/// Toggle mute state.
	/// </summary>
	public void ToggleMute() => IsMuted = !IsMuted;

	/// <summary>
	/// Toggle solo state.
	/// </summary>
	public void ToggleSolo() => IsSolo = !IsSolo;

	/// <summary>
	/// Refresh all properties.
	/// </summary>
	public void Refresh() {
		OnPropertyChanged(nameof(IsMuted));
		OnPropertyChanged(nameof(IsSolo));
		OnPropertyChanged(nameof(Volume));
		OnPropertyChanged(nameof(VolumePercent));
		OnPropertyChanged(nameof(SourceNumber));
		OnPropertyChanged(nameof(Pitch));
		OnPropertyChanged(nameof(EnvelopeLevel));
	}

	protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}

/// <summary>
/// View model for DSP global settings.
/// </summary>
public class DspViewModel : INotifyPropertyChanged {
	private readonly SpcEditor _editor;

	public event PropertyChangedEventHandler? PropertyChanged;

	public DspViewModel(SpcEditor editor) {
		_editor = editor;
	}

	/// <summary>
	/// Main volume left (-128 to 127).
	/// </summary>
	public int MainVolumeLeft {
		get => _editor.MainVolume.Left;
		set {
			var (Left, Right) = _editor.MainVolume;
			_editor.MainVolume = ((sbyte)Math.Clamp(value, -128, 127), Right);
			OnPropertyChanged();
		}
	}

	/// <summary>
	/// Main volume right (-128 to 127).
	/// </summary>
	public int MainVolumeRight {
		get => _editor.MainVolume.Right;
		set {
			var (Left, Right) = _editor.MainVolume;
			_editor.MainVolume = (Left, (sbyte)Math.Clamp(value, -128, 127));
			OnPropertyChanged();
		}
	}

	/// <summary>
	/// Echo volume left (-128 to 127).
	/// </summary>
	public int EchoVolumeLeft {
		get => _editor.EchoVolume.Left;
		set {
			var (Left, Right) = _editor.EchoVolume;
			_editor.EchoVolume = ((sbyte)Math.Clamp(value, -128, 127), Right);
			OnPropertyChanged();
		}
	}

	/// <summary>
	/// Echo volume right (-128 to 127).
	/// </summary>
	public int EchoVolumeRight {
		get => _editor.EchoVolume.Right;
		set {
			var (Left, Right) = _editor.EchoVolume;
			_editor.EchoVolume = (Left, (sbyte)Math.Clamp(value, -128, 127));
			OnPropertyChanged();
		}
	}

	/// <summary>
	/// Echo feedback (-128 to 127).
	/// </summary>
	public int EchoFeedback {
		get => _editor.EchoFeedback;
		set {
			_editor.EchoFeedback = (sbyte)Math.Clamp(value, -128, 127);
			OnPropertyChanged();
		}
	}

	/// <summary>
	/// Echo delay (0-15, each unit = 16ms).
	/// </summary>
	public int EchoDelay {
		get => _editor.EchoDelay;
		set {
			_editor.EchoDelay = (byte)Math.Clamp(value, 0, 15);
			OnPropertyChanged();
			OnPropertyChanged(nameof(EchoDelayMs));
		}
	}

	/// <summary>
	/// Echo delay in milliseconds.
	/// </summary>
	public int EchoDelayMs => EchoDelay * 16;

	/// <summary>
	/// Echo enable bitmask.
	/// </summary>
	public byte EchoEnable {
		get => _editor.EchoEnable;
		set {
			_editor.EchoEnable = value;
			OnPropertyChanged();
		}
	}

	/// <summary>
	/// Noise enable bitmask.
	/// </summary>
	public byte NoiseEnable {
		get => _editor.NoiseEnable;
		set {
			_editor.NoiseEnable = value;
			OnPropertyChanged();
		}
	}

	/// <summary>
	/// Pitch modulation bitmask.
	/// </summary>
	public byte PitchModulation {
		get => _editor.PitchModulation;
		set {
			_editor.PitchModulation = value;
			OnPropertyChanged();
		}
	}

	/// <summary>
	/// Gets whether echo is enabled for a specific voice.
	/// </summary>
	public bool IsEchoEnabled(int voice) => (EchoEnable & (1 << voice)) != 0;

	/// <summary>
	/// Sets echo enable for a specific voice.
	/// </summary>
	public void SetEchoEnabled(int voice, bool enabled) {
		if (enabled)
			EchoEnable |= (byte)(1 << voice);
		else
			EchoEnable &= (byte)~(1 << voice);
	}

	/// <summary>
	/// Refresh all properties.
	/// </summary>
	public void Refresh() {
		OnPropertyChanged(nameof(MainVolumeLeft));
		OnPropertyChanged(nameof(MainVolumeRight));
		OnPropertyChanged(nameof(EchoVolumeLeft));
		OnPropertyChanged(nameof(EchoVolumeRight));
		OnPropertyChanged(nameof(EchoFeedback));
		OnPropertyChanged(nameof(EchoDelay));
		OnPropertyChanged(nameof(EchoDelayMs));
		OnPropertyChanged(nameof(EchoEnable));
		OnPropertyChanged(nameof(NoiseEnable));
		OnPropertyChanged(nameof(PitchModulation));
	}

	protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
