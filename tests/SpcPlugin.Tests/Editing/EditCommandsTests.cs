using SpcPlugin.Core.Editing;

namespace SpcPlugin.Tests.Editing;

/// <summary>
/// Tests for SPC edit commands.
/// </summary>
public class EditCommandsTests {
	[Fact]
	public void MemoryEditCommand_Execute_AppliesNewData() {
		// Arrange
		var memory = new byte[100];
		var oldData = new byte[] { 1, 2, 3 };
		var newData = new byte[] { 10, 20, 30 };
		Array.Copy(oldData, 0, memory, 50, oldData.Length);
		var command = new MemoryEditCommand(memory, 50, "Test Edit", oldData, newData);

		// Act
		command.Execute();

		// Assert
		Assert.Equal(10, memory[50]);
		Assert.Equal(20, memory[51]);
		Assert.Equal(30, memory[52]);
	}

	[Fact]
	public void MemoryEditCommand_Undo_RestoresOldData() {
		// Arrange
		var memory = new byte[100];
		var oldData = new byte[] { 1, 2, 3 };
		var newData = new byte[] { 10, 20, 30 };
		Array.Copy(oldData, 0, memory, 50, oldData.Length);
		var command = new MemoryEditCommand(memory, 50, "Test Edit", oldData, newData);
		command.Execute();

		// Act
		command.Undo();

		// Assert
		Assert.Equal(1, memory[50]);
		Assert.Equal(2, memory[51]);
		Assert.Equal(3, memory[52]);
	}

	[Fact]
	public void CompoundCommand_Execute_ExecutesAllCommands() {
		// Arrange
		var memory = new byte[100];
		var cmd1 = new MemoryEditCommand(memory, 0, "Edit 1", [0], [1]);
		var cmd2 = new MemoryEditCommand(memory, 10, "Edit 2", [0], [2]);
		var cmd3 = new MemoryEditCommand(memory, 20, "Edit 3", [0], [3]);
		var compound = new CompoundCommand("Multiple Edits", cmd1, cmd2, cmd3);

		// Act
		compound.Execute();

		// Assert
		Assert.Equal(1, memory[0]);
		Assert.Equal(2, memory[10]);
		Assert.Equal(3, memory[20]);
	}

	[Fact]
	public void CompoundCommand_Undo_UndoesInReverseOrder() {
		// Arrange
		var memory = new byte[100];
		memory[0] = 0;
		memory[10] = 0;
		memory[20] = 0;
		var cmd1 = new MemoryEditCommand(memory, 0, "Edit 1", [0], [1]);
		var cmd2 = new MemoryEditCommand(memory, 10, "Edit 2", [0], [2]);
		var cmd3 = new MemoryEditCommand(memory, 20, "Edit 3", [0], [3]);
		var compound = new CompoundCommand("Multiple Edits", cmd1, cmd2, cmd3);
		compound.Execute();

		// Act
		compound.Undo();

		// Assert
		Assert.Equal(0, memory[0]);
		Assert.Equal(0, memory[10]);
		Assert.Equal(0, memory[20]);
	}

	[Fact]
	public void VoiceVolumeCommand_Description_IsCorrect() {
		// Arrange
		var memory = new byte[256];
		var command = new VoiceVolumeCommand(memory, 0, 0, 0, 0, 100, 100);

		// Assert
		Assert.Contains("Voice 0", command.Description);
		Assert.Contains("Volume", command.Description);
	}

	[Fact]
	public void VoicePitchCommand_Description_IsCorrect() {
		// Arrange
		var memory = new byte[256];
		var command = new VoicePitchCommand(memory, 3, 0, 0x1000, 0x2000);

		// Assert
		Assert.Contains("Voice 3", command.Description);
		Assert.Contains("Pitch", command.Description);
	}

	[Fact]
	public void DspRegisterCommand_AppliesCorrectly() {
		// Arrange
		var memory = new byte[256];
		memory[0x6C] = 0x00;
		var command = new DspRegisterCommand(memory, 0x6C, "Main Volume Left", 0x00, 0x7F);

		// Act
		command.Execute();

		// Assert
		Assert.Equal(0x7F, memory[0x6C]);
	}

	[Fact]
	public void SampleDirectoryCommand_AppliesCorrectly() {
		// Arrange
		var memory = new byte[0x10000];
		var command = new SampleDirectoryCommand(memory, 0x200, 0, 0x0000, 0x0000, 0x1000, 0x1100);

		// Act
		command.Execute();

		// Assert
		Assert.Equal(0x00, memory[0x200]); // Start low byte
		Assert.Equal(0x10, memory[0x201]); // Start high byte
		Assert.Equal(0x00, memory[0x202]); // Loop low byte
		Assert.Equal(0x11, memory[0x203]); // Loop high byte
	}
}
