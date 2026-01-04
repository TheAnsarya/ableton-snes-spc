namespace SpcPlugin.Core.Emulation;

/// <summary>
/// SPC700 CPU emulator for the SNES audio processing unit.
/// The SPC700 is an 8-bit CPU running at ~1.024 MHz that controls the S-DSP.
/// </summary>
public sealed class Spc700 {
	private readonly byte[] _ram = new byte[0x10000]; // 64KB RAM
	private readonly byte[] _ipl = new byte[64];      // IPL ROM (boot code)

	// Registers
	private ushort _pc;  // Program counter
	private byte _a;     // Accumulator
	private byte _x;     // X index
	private byte _y;     // Y index
	private byte _sp;    // Stack pointer
	private byte _psw;   // Program status word

	// Cycle tracking
	private long _totalCycles;

	/// <summary>
	/// Gets the total number of cycles executed.
	/// </summary>
	public long TotalCycles => _totalCycles;

	/// <summary>
	/// Gets a span view of the 64KB RAM.
	/// </summary>
	public Span<byte> Ram => _ram.AsSpan();

	/// <summary>
	/// Resets the CPU to initial state.
	/// </summary>
	public void Reset() {
		_pc = 0xffc0; // Start of IPL ROM
		_a = 0;
		_x = 0;
		_y = 0;
		_sp = 0xef;
		_psw = 0;
		_totalCycles = 0;
	}

	/// <summary>
	/// Loads an SPC file's data into the emulator state.
	/// </summary>
	/// <param name="spcData">Raw SPC file data.</param>
	public void LoadSpc(ReadOnlySpan<byte> spcData) {
		if (spcData.Length < 0x10200) {
			throw new ArgumentException("SPC data too small", nameof(spcData));
		}

		// SPC file format offsets
		const int ramOffset = 0x100;
		const int dspOffset = 0x10100;

		// Load RAM (64KB at offset 0x100)
		spcData.Slice(ramOffset, 0x10000).CopyTo(_ram);

		// Load registers from header
		_pc = (ushort)(spcData[0x25] | (spcData[0x26] << 8));
		_a = spcData[0x27];
		_x = spcData[0x28];
		_y = spcData[0x29];
		_psw = spcData[0x2a];
		_sp = spcData[0x2b];
	}

	/// <summary>
	/// Executes one CPU instruction.
	/// </summary>
	/// <returns>Number of cycles consumed.</returns>
	public int Step() {
		byte opcode = _ram[_pc++];
		int cycles = ExecuteOpcode(opcode);
		_totalCycles += cycles;
		return cycles;
	}

	/// <summary>
	/// Executes CPU instructions for the specified number of cycles.
	/// </summary>
	/// <param name="targetCycles">Target cycle count to execute.</param>
	/// <returns>Actual cycles executed.</returns>
	public int Execute(int targetCycles) {
		int executed = 0;
		while (executed < targetCycles) {
			executed += Step();
		}
		return executed;
	}

	private int ExecuteOpcode(byte opcode) {
		// TODO: Implement full SPC700 instruction set
		// This is a stub that returns typical cycle counts
		return opcode switch {
			0x00 => 2,  // NOP
			0x2f => 2,  // BRA (branch always)
			0xef => 3,  // SLEEP
			0xff => 3,  // STOP
			_ => 2,     // Default cycle count
		};
	}

	// PSW flag helpers
	private bool GetFlag(int bit) => (_psw & (1 << bit)) != 0;
	private void SetFlag(int bit, bool value) {
		if (value) {
			_psw |= (byte)(1 << bit);
		} else {
			_psw &= (byte)~(1 << bit);
		}
	}

	private bool CarryFlag { get => GetFlag(0); set => SetFlag(0, value); }
	private bool ZeroFlag { get => GetFlag(1); set => SetFlag(1, value); }
	private bool InterruptFlag { get => GetFlag(2); set => SetFlag(2, value); }
	private bool HalfCarryFlag { get => GetFlag(3); set => SetFlag(3, value); }
	private bool BreakFlag { get => GetFlag(4); set => SetFlag(4, value); }
	private bool DirectPageFlag { get => GetFlag(5); set => SetFlag(5, value); }
	private bool OverflowFlag { get => GetFlag(6); set => SetFlag(6, value); }
	private bool NegativeFlag { get => GetFlag(7); set => SetFlag(7, value); }
}
