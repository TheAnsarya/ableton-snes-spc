using SpcPlugin.Core.Editing;

namespace SpcPlugin.Tests.Editing;

/// <summary>
/// Tests for the undo/redo system.
/// </summary>
public class UndoManagerTests {
	[Fact]
	public void Execute_AddsToUndoStack() {
		// Arrange
		var manager = new UndoManager();
		var command = new TestCommand("Test");

		// Act
		manager.Execute(command);

		// Assert
		Assert.True(manager.CanUndo);
		Assert.False(manager.CanRedo);
		Assert.Equal(1, manager.UndoCount);
		Assert.Equal("Test", manager.UndoDescription);
	}

	[Fact]
	public void Undo_MovesToRedoStack() {
		// Arrange
		var manager = new UndoManager();
		var command = new TestCommand("Test");
		manager.Execute(command);

		// Act
		bool result = manager.Undo();

		// Assert
		Assert.True(result);
		Assert.False(manager.CanUndo);
		Assert.True(manager.CanRedo);
		Assert.Equal("Test", manager.RedoDescription);
	}

	[Fact]
	public void Redo_MovesBackToUndoStack() {
		// Arrange
		var manager = new UndoManager();
		var command = new TestCommand("Test");
		manager.Execute(command);
		manager.Undo();

		// Act
		bool result = manager.Redo();

		// Assert
		Assert.True(result);
		Assert.True(manager.CanUndo);
		Assert.False(manager.CanRedo);
	}

	[Fact]
	public void Execute_ClearsRedoStack() {
		// Arrange
		var manager = new UndoManager();
		manager.Execute(new TestCommand("First"));
		manager.Undo();
		Assert.True(manager.CanRedo);

		// Act
		manager.Execute(new TestCommand("Second"));

		// Assert
		Assert.False(manager.CanRedo);
	}

	[Fact]
	public void Execute_RespectMaxHistory() {
		// Arrange
		var manager = new UndoManager(maxHistory: 5);

		// Act
		for (int i = 0; i < 10; i++) {
			manager.Execute(new TestCommand($"Command {i}"));
		}

		// Assert
		Assert.Equal(5, manager.UndoCount);
	}

	[Fact]
	public void Clear_RemovesAllHistory() {
		// Arrange
		var manager = new UndoManager();
		manager.Execute(new TestCommand("Test"));
		manager.Undo();

		// Act
		manager.Clear();

		// Assert
		Assert.False(manager.CanUndo);
		Assert.False(manager.CanRedo);
	}

	[Fact]
	public void Undo_WhenEmpty_ReturnsFalse() {
		// Arrange
		var manager = new UndoManager();

		// Act & Assert
		Assert.False(manager.Undo());
	}

	[Fact]
	public void Redo_WhenEmpty_ReturnsFalse() {
		// Arrange
		var manager = new UndoManager();

		// Act & Assert
		Assert.False(manager.Redo());
	}

	[Fact]
	public void GetUndoHistory_ReturnsDescriptions() {
		// Arrange
		var manager = new UndoManager();
		manager.Execute(new TestCommand("First"));
		manager.Execute(new TestCommand("Second"));
		manager.Execute(new TestCommand("Third"));

		// Act
		var history = manager.GetUndoHistory().ToList();

		// Assert
		Assert.Equal(3, history.Count);
		Assert.Equal("Third", history[0]);  // Most recent first
		Assert.Equal("Second", history[1]);
		Assert.Equal("First", history[2]);
	}

	[Fact]
	public void StateChanged_RaisedOnOperations() {
		// Arrange
		var manager = new UndoManager();
		int eventCount = 0;
		manager.StateChanged += (_, _) => eventCount++;

		// Act
		manager.Execute(new TestCommand("Test"));
		manager.Undo();
		manager.Redo();
		manager.Clear();

		// Assert
		Assert.Equal(4, eventCount);
	}

	[Fact]
	public void CommandExecuted_RaisedWithCorrectAction() {
		// Arrange
		var manager = new UndoManager();
		var actions = new List<CommandAction>();
		manager.CommandExecuted += (_, e) => actions.Add(e.Action);

		// Act
		manager.Execute(new TestCommand("Test"));
		manager.Undo();
		manager.Redo();

		// Assert
		Assert.Equal(3, actions.Count);
		Assert.Equal(CommandAction.Execute, actions[0]);
		Assert.Equal(CommandAction.Undo, actions[1]);
		Assert.Equal(CommandAction.Redo, actions[2]);
	}

	private class TestCommand : IEditCommand {
		public string Description { get; }
		public int ExecuteCount { get; private set; }
		public int UndoCount { get; private set; }

		public TestCommand(string description) {
			Description = description;
		}

		public void Execute() => ExecuteCount++;
		public void Undo() => UndoCount++;
	}
}
