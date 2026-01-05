using SpcPlugin.Core.Analysis;
using SpcPlugin.Core.Formats;

namespace SpcPlugin.Core.Project;

/// <summary>
/// Manages SPCX project lifecycle including load, save, undo/redo, and change tracking.
/// Designed for integration with Ableton and other DAWs.
/// </summary>
public sealed class ProjectManager : IDisposable {
	private readonly Stack<ProjectSnapshot> _undoStack = new();
	private readonly Stack<ProjectSnapshot> _redoStack = new();

	private SpcxFile? _currentProject;
	private string? _currentPath;
	private bool _hasUnsavedChanges;

	/// <summary>
	/// Gets the currently loaded project.
	/// </summary>
	public SpcxFile? CurrentProject => _currentProject;

	/// <summary>
	/// Gets the current project file path (null if unsaved).
	/// </summary>
	public string? CurrentPath => _currentPath;

	/// <summary>
	/// Gets whether there are unsaved changes.
	/// </summary>
	public bool HasUnsavedChanges => _hasUnsavedChanges;

	/// <summary>
	/// Gets whether undo is available.
	/// </summary>
	public bool CanUndo => _undoStack.Count > 0;

	/// <summary>
	/// Gets whether redo is available.
	/// </summary>
	public bool CanRedo => _redoStack.Count > 0;

	/// <summary>
	/// Gets the project analyzer (updates after load).
	/// </summary>
	public SpcAnalyzer? Analyzer { get; private set; }

	/// <summary>
	/// Maximum number of undo steps to keep.
	/// </summary>
	public int MaxUndoSteps { get; set; } = 50;

	/// <summary>
	/// Event fired when the project changes.
	/// </summary>
	public event EventHandler<ProjectChangedEventArgs>? ProjectChanged;

	/// <summary>
	/// Event fired when undo/redo state changes.
	/// </summary>
	public event EventHandler? UndoRedoStateChanged;

	/// <summary>
	/// Creates a new empty project.
	/// </summary>
	public void NewProject(string name = "Untitled") {
		_currentProject = new SpcxFile {
			Manifest = {
				Name = name,
				CreatedDate = DateTime.UtcNow,
				ModifiedDate = DateTime.UtcNow,
			},
		};

		_currentPath = null;
		_hasUnsavedChanges = true;
		ClearUndoRedo();

		RunAnalysis();
		OnProjectChanged(ProjectChangeType.New);
	}

	/// <summary>
	/// Loads an SPCX project from disk.
	/// </summary>
	public void LoadProject(string path) {
		ArgumentNullException.ThrowIfNull(path);

		_currentProject = SpcxFile.Load(path);
		_currentPath = path;
		_hasUnsavedChanges = false;
		ClearUndoRedo();

		RunAnalysis();
		OnProjectChanged(ProjectChangeType.Loaded);
	}

	/// <summary>
	/// Imports an SPC file as a new project.
	/// </summary>
	public void ImportSpc(string spcPath) {
		ArgumentNullException.ThrowIfNull(spcPath);

		_currentProject = SpcxFile.ImportFromSpc(spcPath);
		_currentPath = null; // Not saved yet
		_hasUnsavedChanges = true;
		ClearUndoRedo();

		RunAnalysis();
		OnProjectChanged(ProjectChangeType.Imported);
	}

	/// <summary>
	/// Saves the current project to disk.
	/// </summary>
	public void SaveProject() {
		if (_currentProject == null) {
			throw new InvalidOperationException("No project loaded");
		}

		if (_currentPath == null) {
			throw new InvalidOperationException("No file path set. Use SaveProjectAs instead.");
		}

		_currentProject.Save(_currentPath);
		_hasUnsavedChanges = false;
		OnProjectChanged(ProjectChangeType.Saved);
	}

	/// <summary>
	/// Saves the current project to a new location.
	/// </summary>
	public void SaveProjectAs(string path) {
		ArgumentNullException.ThrowIfNull(path);

		if (_currentProject == null) {
			throw new InvalidOperationException("No project loaded");
		}

		_currentProject.Save(path);
		_currentPath = path;
		_hasUnsavedChanges = false;
		OnProjectChanged(ProjectChangeType.Saved);
	}

	/// <summary>
	/// Exports the current project to SPC format.
	/// </summary>
	public void ExportSpc(string path) {
		ArgumentNullException.ThrowIfNull(path);

		if (_currentProject == null) {
			throw new InvalidOperationException("No project loaded");
		}

		_currentProject.ExportToSpc(path);
		OnProjectChanged(ProjectChangeType.Exported);
	}

	/// <summary>
	/// Closes the current project.
	/// </summary>
	public void CloseProject() {
		_currentProject = null;
		_currentPath = null;
		_hasUnsavedChanges = false;
		Analyzer = null;
		ClearUndoRedo();
		OnProjectChanged(ProjectChangeType.Closed);
	}

	#region Undo/Redo

	/// <summary>
	/// Creates a snapshot before making changes (call before modifications).
	/// </summary>
	public void BeginModification(string description) {
		if (_currentProject == null) return;

		var snapshot = new ProjectSnapshot {
			Description = description,
			Timestamp = DateTime.UtcNow,
			Ram = (byte[])_currentProject.Ram.Clone(),
			DspRegisters = (byte[])_currentProject.DspRegisters.Clone(),
		};

		_undoStack.Push(snapshot);

		// Limit stack size
		while (_undoStack.Count > MaxUndoSteps) {
			// Remove oldest (bottom of stack)
			var temp = new Stack<ProjectSnapshot>();
			while (_undoStack.Count > 1) {
				temp.Push(_undoStack.Pop());
			}

			_undoStack.Clear();
			while (temp.Count > 0) {
				_undoStack.Push(temp.Pop());
			}
		}

		// Clear redo stack on new modification
		_redoStack.Clear();

		_hasUnsavedChanges = true;
		UndoRedoStateChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>
	/// Undoes the last modification.
	/// </summary>
	public void Undo() {
		if (_currentProject == null || _undoStack.Count == 0) return;

		// Save current state to redo stack
		var redoSnapshot = new ProjectSnapshot {
			Description = "Redo",
			Timestamp = DateTime.UtcNow,
			Ram = (byte[])_currentProject.Ram.Clone(),
			DspRegisters = (byte[])_currentProject.DspRegisters.Clone(),
		};
		_redoStack.Push(redoSnapshot);

		// Restore from undo stack
		var snapshot = _undoStack.Pop();
		snapshot.Ram.CopyTo(_currentProject.Ram, 0);
		snapshot.DspRegisters.CopyTo(_currentProject.DspRegisters, 0);

		RunAnalysis();
		OnProjectChanged(ProjectChangeType.Modified);
		UndoRedoStateChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>
	/// Redoes the last undone modification.
	/// </summary>
	public void Redo() {
		if (_currentProject == null || _redoStack.Count == 0) return;

		// Save current state to undo stack
		var undoSnapshot = new ProjectSnapshot {
			Description = "Undo",
			Timestamp = DateTime.UtcNow,
			Ram = (byte[])_currentProject.Ram.Clone(),
			DspRegisters = (byte[])_currentProject.DspRegisters.Clone(),
		};
		_undoStack.Push(undoSnapshot);

		// Restore from redo stack
		var snapshot = _redoStack.Pop();
		snapshot.Ram.CopyTo(_currentProject.Ram, 0);
		snapshot.DspRegisters.CopyTo(_currentProject.DspRegisters, 0);

		RunAnalysis();
		OnProjectChanged(ProjectChangeType.Modified);
		UndoRedoStateChanged?.Invoke(this, EventArgs.Empty);
	}

	private void ClearUndoRedo() {
		_undoStack.Clear();
		_redoStack.Clear();
		UndoRedoStateChanged?.Invoke(this, EventArgs.Empty);
	}

	#endregion

	#region State Query

	/// <summary>
	/// Gets the current project as raw SPC data for engine loading.
	/// </summary>
	public byte[] GetSpcData() {
		if (_currentProject == null) {
			throw new InvalidOperationException("No project loaded");
		}

		// Build SPC data in memory
		byte[] spcData = new byte[0x10200];

		// Write header
		"SNES-SPC700 Sound File Data v0.30"u8.CopyTo(spcData.AsSpan(0, 33));
		spcData[0x21] = 0x1a;
		spcData[0x22] = 0x1a;
		spcData[0x23] = 0x1a; // ID666 present

		// CPU registers
		spcData[0x25] = (byte)(_currentProject.CpuState.PC & 0xff);
		spcData[0x26] = (byte)(_currentProject.CpuState.PC >> 8);
		spcData[0x27] = _currentProject.CpuState.A;
		spcData[0x28] = _currentProject.CpuState.X;
		spcData[0x29] = _currentProject.CpuState.Y;
		spcData[0x2a] = _currentProject.CpuState.PSW;
		spcData[0x2b] = _currentProject.CpuState.SP;

		// Copy RAM
		_currentProject.Ram.CopyTo(spcData.AsSpan(0x100));

		// Copy DSP registers
		_currentProject.DspRegisters.CopyTo(spcData.AsSpan(0x10100));

		return spcData;
	}

	/// <summary>
	/// Gets a read-only view of the RAM.
	/// </summary>
	public ReadOnlySpan<byte> GetRam() {
		if (_currentProject == null) {
			return ReadOnlySpan<byte>.Empty;
		}

		return _currentProject.Ram;
	}

	/// <summary>
	/// Gets a read-only view of DSP registers.
	/// </summary>
	public ReadOnlySpan<byte> GetDspRegisters() {
		if (_currentProject == null) {
			return ReadOnlySpan<byte>.Empty;
		}

		return _currentProject.DspRegisters;
	}

	#endregion

	#region Modifications

	/// <summary>
	/// Modifies RAM contents.
	/// </summary>
	public void ModifyRam(int address, ReadOnlySpan<byte> data, string description) {
		if (_currentProject == null) return;

		BeginModification(description);
		data.CopyTo(_currentProject.Ram.AsSpan(address));
		RunAnalysis();
		OnProjectChanged(ProjectChangeType.Modified);
	}

	/// <summary>
	/// Modifies a DSP register.
	/// </summary>
	public void ModifyDspRegister(int register, byte value, string description) {
		if (_currentProject == null) return;

		BeginModification(description);
		_currentProject.DspRegisters[register] = value;
		RunAnalysis();
		OnProjectChanged(ProjectChangeType.Modified);
	}

	/// <summary>
	/// Replaces a sample in the project.
	/// </summary>
	public void ReplaceSample(int sampleIndex, byte[] brrData, string description) {
		if (_currentProject == null || Analyzer == null) return;

		var sample = Analyzer.Samples.FirstOrDefault(s => s.Index == sampleIndex);
		if (sample == null) {
			throw new ArgumentException($"Sample {sampleIndex} not found", nameof(sampleIndex));
		}

		// Check size constraint
		int sizeDiff = brrData.Length - sample.Size;
		if (sizeDiff > Analyzer.Memory.FreeBytes) {
			throw new InvalidOperationException(
				$"Sample too large. Need {brrData.Length} bytes but only {Analyzer.Memory.FreeBytes + sample.Size} available.");
		}

		BeginModification(description);

		// For simplicity, replace in-place if same size or smaller
		// A full implementation would relocate samples if needed
		if (brrData.Length <= sample.Size) {
			brrData.CopyTo(_currentProject.Ram, sample.StartAddress);
			// Zero out remaining bytes
			Array.Clear(_currentProject.Ram, sample.StartAddress + brrData.Length, sample.Size - brrData.Length);
		} else {
			throw new InvalidOperationException("Sample relocation not yet implemented. New sample must be same size or smaller.");
		}

		RunAnalysis();
		OnProjectChanged(ProjectChangeType.Modified);
	}

	#endregion

	private void RunAnalysis() {
		if (_currentProject == null) {
			Analyzer = null;
			return;
		}

		// Build SPC data for analysis
		var spcData = new byte[0x10200];
		"SNES-SPC700 Sound File Data v0.30"u8.CopyTo(spcData.AsSpan(0, 33));
		spcData[0x21] = 0x1a;
		spcData[0x22] = 0x1a;
		_currentProject.Ram.CopyTo(spcData, 0x100);
		_currentProject.DspRegisters.CopyTo(spcData, 0x10100);

		Analyzer = new SpcAnalyzer();
		Analyzer.Analyze(spcData);
	}

	private void OnProjectChanged(ProjectChangeType changeType) {
		ProjectChanged?.Invoke(this, new ProjectChangedEventArgs(changeType));
	}

	public void Dispose() {
		CloseProject();
	}
}

/// <summary>
/// Snapshot of project state for undo/redo.
/// </summary>
internal sealed class ProjectSnapshot {
	public required string Description { get; init; }
	public required DateTime Timestamp { get; init; }
	public required byte[] Ram { get; init; }
	public required byte[] DspRegisters { get; init; }
}

/// <summary>
/// Types of project changes.
/// </summary>
public enum ProjectChangeType {
	New,
	Loaded,
	Imported,
	Modified,
	Saved,
	Exported,
	Closed,
}

/// <summary>
/// Event args for project changed events.
/// </summary>
public sealed class ProjectChangedEventArgs : EventArgs {
	public ProjectChangeType ChangeType { get; }

	public ProjectChangedEventArgs(ProjectChangeType changeType) {
		ChangeType = changeType;
	}
}
