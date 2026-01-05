using SpcPlugin.Core.Project;

namespace SpcPlugin.Tests.Project;

public class ProjectManagerTests {
	[Fact]
	public void NewProject_CreatesEmptyProject() {
		// Arrange
		using var manager = new ProjectManager();

		// Act
		manager.NewProject("Test Project");

		// Assert
		Assert.NotNull(manager.CurrentProject);
		Assert.Equal("Test Project", manager.CurrentProject.Manifest.Name);
		Assert.True(manager.HasUnsavedChanges);
		Assert.Null(manager.CurrentPath);
	}

	[Fact]
	public void NewProject_ClearsUndoRedo() {
		// Arrange
		using var manager = new ProjectManager();
		manager.NewProject("Test");

		// Act
		manager.BeginModification("Change 1");
		manager.NewProject("New Test");

		// Assert
		Assert.False(manager.CanUndo);
		Assert.False(manager.CanRedo);
	}

	[Fact]
	public void BeginModification_AddsToUndoStack() {
		// Arrange
		using var manager = new ProjectManager();
		manager.NewProject("Test");

		// Act
		manager.BeginModification("Test Change");

		// Assert
		Assert.True(manager.CanUndo);
	}

	[Fact]
	public void BeginModification_ClearsRedoStack() {
		// Arrange
		using var manager = new ProjectManager();
		manager.NewProject("Test");
		manager.BeginModification("Change 1");
		manager.Undo();
		Assert.True(manager.CanRedo);

		// Act
		manager.BeginModification("Change 2");

		// Assert
		Assert.False(manager.CanRedo);
	}

	[Fact]
	public void Undo_RestoresPreviousState() {
		// Arrange
		using var manager = new ProjectManager();
		manager.NewProject("Test");

		// Make a modification
		var originalRam = manager.GetRam().ToArray();
		manager.BeginModification("Change RAM");
		manager.CurrentProject!.Ram[0x1000] = 0xff;

		// Act
		manager.Undo();

		// Assert
		Assert.Equal(originalRam[0x1000], manager.CurrentProject.Ram[0x1000]);
		Assert.True(manager.CanRedo);
	}

	[Fact]
	public void Redo_RestoresUndoneState() {
		// Arrange
		using var manager = new ProjectManager();
		manager.NewProject("Test");

		manager.BeginModification("Change RAM");
		manager.CurrentProject!.Ram[0x1000] = 0xff;
		manager.Undo();

		// Act
		manager.Redo();

		// Assert
		Assert.Equal(0xff, manager.CurrentProject.Ram[0x1000]);
		Assert.False(manager.CanRedo);
	}

	[Fact]
	public void MaxUndoSteps_LimitsStackSize() {
		// Arrange
		using var manager = new ProjectManager {
			MaxUndoSteps = 5,
		};
		manager.NewProject("Test");

		// Act - Add more than max steps
		for (int i = 0; i < 10; i++) {
			manager.BeginModification($"Change {i}");
		}

		// Assert - Count undos possible
		int undoCount = 0;
		while (manager.CanUndo) {
			manager.Undo();
			undoCount++;
		}

		Assert.Equal(5, undoCount);
	}

	[Fact]
	public void CloseProject_ClearsAllState() {
		// Arrange
		using var manager = new ProjectManager();
		manager.NewProject("Test");
		manager.BeginModification("Change");

		// Act
		manager.CloseProject();

		// Assert
		Assert.Null(manager.CurrentProject);
		Assert.Null(manager.CurrentPath);
		Assert.False(manager.HasUnsavedChanges);
		Assert.False(manager.CanUndo);
		Assert.False(manager.CanRedo);
		Assert.Null(manager.Analyzer);
	}

	[Fact]
	public void ModifyDspRegister_UpdatesRegister() {
		// Arrange
		using var manager = new ProjectManager();
		manager.NewProject("Test");

		// Act
		manager.ModifyDspRegister(0x0c, 0x7f, "Set main volume");

		// Assert
		Assert.Equal(0x7f, manager.CurrentProject!.DspRegisters[0x0c]);
		Assert.True(manager.CanUndo);
	}

	[Fact]
	public void GetSpcData_NoProject_Throws() {
		// Arrange
		using var manager = new ProjectManager();

		// Act & Assert
		Assert.Throws<InvalidOperationException>(() => manager.GetSpcData());
	}

	[Fact]
	public void GetRam_NoProject_ReturnsEmpty() {
		// Arrange
		using var manager = new ProjectManager();

		// Act
		var ram = manager.GetRam();

		// Assert
		Assert.True(ram.IsEmpty);
	}

	[Fact]
	public void GetDspRegisters_NoProject_ReturnsEmpty() {
		// Arrange
		using var manager = new ProjectManager();

		// Act
		var dsp = manager.GetDspRegisters();

		// Assert
		Assert.True(dsp.IsEmpty);
	}

	[Fact]
	public void ProjectChanged_Event_FiredOnNew() {
		// Arrange
		using var manager = new ProjectManager();
		ProjectChangeType? receivedType = null;
		manager.ProjectChanged += (_, e) => receivedType = e.ChangeType;

		// Act
		manager.NewProject("Test");

		// Assert
		Assert.Equal(ProjectChangeType.New, receivedType);
	}

	[Fact]
	public void ProjectChanged_Event_FiredOnModify() {
		// Arrange
		using var manager = new ProjectManager();
		manager.NewProject("Test");

		ProjectChangeType? receivedType = null;
		manager.ProjectChanged += (_, e) => receivedType = e.ChangeType;

		// Act
		manager.ModifyDspRegister(0, 0x7f, "Test");

		// Assert
		Assert.Equal(ProjectChangeType.Modified, receivedType);
	}

	[Fact]
	public void UndoRedoStateChanged_Event_FiredOnModification() {
		// Arrange
		using var manager = new ProjectManager();
		manager.NewProject("Test");

		bool eventFired = false;
		manager.UndoRedoStateChanged += (_, _) => eventFired = true;

		// Act
		manager.BeginModification("Test");

		// Assert
		Assert.True(eventFired);
	}

	[Fact]
	public void NewProject_RunsAnalysis() {
		// Arrange
		using var manager = new ProjectManager();

		// Act
		manager.NewProject("Test");

		// Assert
		Assert.NotNull(manager.Analyzer);
	}

	[Fact]
	public void Dispose_CleansUp() {
		// Arrange
		var manager = new ProjectManager();
		manager.NewProject("Test");

		// Act
		manager.Dispose();

		// Assert
		Assert.Null(manager.CurrentProject);
	}
}
