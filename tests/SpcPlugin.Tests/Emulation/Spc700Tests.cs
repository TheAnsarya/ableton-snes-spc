namespace SpcPlugin.Tests.Emulation;

using SpcPlugin.Core.Emulation;

public class Spc700Tests {
	[Fact]
	public void Reset_SetsInitialState() {
		// Arrange
		var cpu = new Spc700();

		// Act
		cpu.Reset();

		// Assert
		Assert.Equal(0, cpu.TotalCycles);
	}

	[Fact]
	public void Ram_Returns64KbBuffer() {
		// Arrange
		var cpu = new Spc700();

		// Act
		var ram = cpu.Ram;

		// Assert
		Assert.Equal(0x10000, ram.Length);
	}

	[Fact]
	public void LoadSpc_ThrowsOnSmallData() {
		// Arrange
		var cpu = new Spc700();
		byte[] smallData = new byte[100];

		// Act & Assert
		Assert.Throws<ArgumentException>(() => cpu.LoadSpc(smallData));
	}

	[Fact]
	public void Step_IncrementsCycles() {
		// Arrange
		var cpu = new Spc700();
		cpu.Reset();
		long initialCycles = cpu.TotalCycles;

		// Act
		cpu.Step();

		// Assert
		Assert.True(cpu.TotalCycles > initialCycles);
	}

	[Fact]
	public void Execute_RunsForTargetCycles() {
		// Arrange
		var cpu = new Spc700();
		cpu.Reset();

		// Act
		int executed = cpu.Execute(100);

		// Assert
		Assert.True(executed >= 100);
	}
}
