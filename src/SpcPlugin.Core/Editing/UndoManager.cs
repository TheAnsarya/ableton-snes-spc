namespace SpcPlugin.Core.Editing;

/// <summary>
/// Represents a reversible editing command for the undo/redo system.
/// </summary>
public interface IEditCommand {
	/// <summary>Human-readable description of the command.</summary>
	string Description { get; }

	/// <summary>Executes the command.</summary>
	void Execute();

	/// <summary>Reverses the command.</summary>
	void Undo();

	/// <summary>Re-executes the command after undo.</summary>
	void Redo() => Execute();
}

/// <summary>
/// Manages undo/redo history for SPC editing operations.
/// </summary>
public sealed class UndoManager {
	private readonly List<IEditCommand> _undoStack = [];
	private readonly List<IEditCommand> _redoStack = [];
	private readonly int _maxHistory;
	private bool _isExecuting;

	/// <summary>
	/// Creates a new undo manager.
	/// </summary>
	/// <param name="maxHistory">Maximum number of commands to keep in history.</param>
	public UndoManager(int maxHistory = 100) {
		_maxHistory = maxHistory;
	}

	/// <summary>Event raised when undo/redo state changes.</summary>
	public event EventHandler? StateChanged;

	/// <summary>Event raised when a command is executed.</summary>
	public event EventHandler<CommandExecutedEventArgs>? CommandExecuted;

	/// <summary>Whether undo is available.</summary>
	public bool CanUndo => _undoStack.Count > 0;

	/// <summary>Whether redo is available.</summary>
	public bool CanRedo => _redoStack.Count > 0;

	/// <summary>Number of commands in undo stack.</summary>
	public int UndoCount => _undoStack.Count;

	/// <summary>Number of commands in redo stack.</summary>
	public int RedoCount => _redoStack.Count;

	/// <summary>Description of next undo operation.</summary>
	public string? UndoDescription => CanUndo ? _undoStack[^1].Description : null;

	/// <summary>Description of next redo operation.</summary>
	public string? RedoDescription => CanRedo ? _redoStack[^1].Description : null;

	/// <summary>
	/// Executes a command and adds it to the undo history.
	/// </summary>
	/// <param name="command">Command to execute.</param>
	public void Execute(IEditCommand command) {
		if (_isExecuting) return;

		try {
			_isExecuting = true;
			command.Execute();

			_undoStack.Add(command);
			_redoStack.Clear();

			// Trim history if needed
			while (_undoStack.Count > _maxHistory) {
				_undoStack.RemoveAt(0);
			}

			OnStateChanged();
			CommandExecuted?.Invoke(this, new CommandExecutedEventArgs(command, CommandAction.Execute));
		} finally {
			_isExecuting = false;
		}
	}

	/// <summary>
	/// Undoes the last command.
	/// </summary>
	/// <returns>True if a command was undone.</returns>
	public bool Undo() {
		if (!CanUndo || _isExecuting) return false;

		try {
			_isExecuting = true;
			var command = _undoStack[^1];
			_undoStack.RemoveAt(_undoStack.Count - 1);

			command.Undo();
			_redoStack.Add(command);

			OnStateChanged();
			CommandExecuted?.Invoke(this, new CommandExecutedEventArgs(command, CommandAction.Undo));
			return true;
		} finally {
			_isExecuting = false;
		}
	}

	/// <summary>
	/// Redoes the last undone command.
	/// </summary>
	/// <returns>True if a command was redone.</returns>
	public bool Redo() {
		if (!CanRedo || _isExecuting) return false;

		try {
			_isExecuting = true;
			var command = _redoStack[^1];
			_redoStack.RemoveAt(_redoStack.Count - 1);

			command.Redo();
			_undoStack.Add(command);

			OnStateChanged();
			CommandExecuted?.Invoke(this, new CommandExecutedEventArgs(command, CommandAction.Redo));
			return true;
		} finally {
			_isExecuting = false;
		}
	}

	/// <summary>
	/// Clears all undo/redo history.
	/// </summary>
	public void Clear() {
		_undoStack.Clear();
		_redoStack.Clear();
		OnStateChanged();
	}

	/// <summary>
	/// Gets the undo history descriptions (most recent first).
	/// </summary>
	public IEnumerable<string> GetUndoHistory() {
		for (int i = _undoStack.Count - 1; i >= 0; i--) {
			yield return _undoStack[i].Description;
		}
	}

	/// <summary>
	/// Gets the redo history descriptions (most recent first).
	/// </summary>
	public IEnumerable<string> GetRedoHistory() {
		for (int i = _redoStack.Count - 1; i >= 0; i--) {
			yield return _redoStack[i].Description;
		}
	}

	private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Specifies the action type for a command event.
/// </summary>
public enum CommandAction {
	Execute,
	Undo,
	Redo,
}

/// <summary>
/// Event arguments for command execution events.
/// </summary>
public sealed class CommandExecutedEventArgs : EventArgs {
	public IEditCommand Command { get; }
	public CommandAction Action { get; }

	public CommandExecutedEventArgs(IEditCommand command, CommandAction action) {
		Command = command;
		Action = action;
	}
}
