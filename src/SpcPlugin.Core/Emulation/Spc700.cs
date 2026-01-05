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

	// CPU state
	private bool _stopped;
	private bool _sleeping;

	// DSP interface
	private SDsp? _dsp;

	/// <summary>
	/// Gets the total number of cycles executed.
	/// </summary>
	public long TotalCycles => _totalCycles;

	/// <summary>
	/// Gets a span view of the 64KB RAM.
	/// </summary>
	public Span<byte> Ram => _ram.AsSpan();

	/// <summary>
	/// Gets or sets the DSP reference for register access.
	/// </summary>
	public SDsp? Dsp {
		get => _dsp;
		set => _dsp = value;
	}

	// Test/debug accessors for registers
	public byte A => _a;
	public byte X => _x;
	public byte Y => _y;
	public ushort PC => _pc;
	public byte SP => _sp;
	public byte PSW => _psw;

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
		_stopped = false;
		_sleeping = false;
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

		// Load RAM (64KB at offset 0x100)
		spcData.Slice(ramOffset, 0x10000).CopyTo(_ram);

		// Load registers from header
		_pc = (ushort)(spcData[0x25] | (spcData[0x26] << 8));
		_a = spcData[0x27];
		_x = spcData[0x28];
		_y = spcData[0x29];
		_psw = spcData[0x2a];
		_sp = spcData[0x2b];

		_stopped = false;
		_sleeping = false;
	}

	/// <summary>
	/// Executes one CPU instruction.
	/// </summary>
	/// <returns>Number of cycles consumed.</returns>
	public int Step() {
		if (_stopped) return 2;
		if (_sleeping) return 2; // TODO: Wake on timer/interrupt

		byte opcode = ReadByte(_pc++);
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

	#region Memory Access

	private byte ReadByte(int address) {
		address &= 0xffff;

		// DSP registers at $00F2-$00F3
		if (address == 0x00f2) {
			return _ram[0x00f2]; // DSP address register
		}
		if (address == 0x00f3) {
			return _dsp?.ReadRegister(_ram[0x00f2]) ?? 0;
		}

		// IPL ROM at $FFC0-$FFFF when enabled
		if (address >= 0xffc0 && (_ram[0x00f1] & 0x80) != 0) {
			return _ipl[address - 0xffc0];
		}

		return _ram[address];
	}

	private void WriteByte(int address, byte value) {
		address &= 0xffff;

		// DSP registers at $00F2-$00F3
		if (address == 0x00f2) {
			_ram[0x00f2] = value;
			return;
		}
		if (address == 0x00f3) {
			_dsp?.WriteRegister(_ram[0x00f2], value);
			_ram[0x00f3] = value;
			return;
		}

		// Control register at $00F1
		if (address == 0x00f1) {
			// Handle control bits
			if ((value & 0x10) != 0) {
				// Clear input ports 0-1
				_ram[0x00f4] = 0;
				_ram[0x00f5] = 0;
			}
			if ((value & 0x20) != 0) {
				// Clear input ports 2-3
				_ram[0x00f6] = 0;
				_ram[0x00f7] = 0;
			}
		}

		_ram[address] = value;
	}

	private ushort ReadWord(int address) {
		byte lo = ReadByte(address);
		byte hi = ReadByte(address + 1);
		return (ushort)(lo | (hi << 8));
	}

	private void WriteWord(int address, ushort value) {
		WriteByte(address, (byte)(value & 0xff));
		WriteByte(address + 1, (byte)(value >> 8));
	}

	private byte FetchByte() => ReadByte(_pc++);
	private ushort FetchWord() {
		byte lo = FetchByte();
		byte hi = FetchByte();
		return (ushort)(lo | (hi << 8));
	}

	// Direct page addressing
	private int DirectPage => (_psw & 0x20) != 0 ? 0x100 : 0;
	private int DpAddress(byte offset) => DirectPage + offset;

	#endregion

	#region Stack Operations

	private void PushByte(byte value) {
		WriteByte(0x100 + _sp, value);
		_sp--;
	}

	private byte PopByte() {
		_sp++;
		return ReadByte(0x100 + _sp);
	}

	private void PushWord(ushort value) {
		PushByte((byte)(value >> 8));
		PushByte((byte)(value & 0xff));
	}

	private ushort PopWord() {
		byte lo = PopByte();
		byte hi = PopByte();
		return (ushort)(lo | (hi << 8));
	}

	#endregion

	#region Flag Helpers

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

	private void SetNZ(byte value) {
		NegativeFlag = (value & 0x80) != 0;
		ZeroFlag = value == 0;
	}

	private void SetNZ16(ushort value) {
		NegativeFlag = (value & 0x8000) != 0;
		ZeroFlag = value == 0;
	}

	#endregion

	#region ALU Operations

	private byte Adc(byte a, byte b) {
		int result = a + b + (CarryFlag ? 1 : 0);
		HalfCarryFlag = ((a & 0x0f) + (b & 0x0f) + (CarryFlag ? 1 : 0)) > 0x0f;
		OverflowFlag = ((~(a ^ b) & (a ^ result)) & 0x80) != 0;
		CarryFlag = result > 0xff;
		byte r = (byte)result;
		SetNZ(r);
		return r;
	}

	private byte Sbc(byte a, byte b) {
		int result = a - b - (CarryFlag ? 0 : 1);
		HalfCarryFlag = ((a & 0x0f) - (b & 0x0f) - (CarryFlag ? 0 : 1)) < 0;
		OverflowFlag = (((a ^ b) & (a ^ result)) & 0x80) != 0;
		CarryFlag = result >= 0;
		byte r = (byte)result;
		SetNZ(r);
		return r;
	}

	private void Cmp(byte a, byte b) {
		int result = a - b;
		CarryFlag = result >= 0;
		SetNZ((byte)result);
	}

	private byte And(byte a, byte b) {
		byte r = (byte)(a & b);
		SetNZ(r);
		return r;
	}

	private byte Or(byte a, byte b) {
		byte r = (byte)(a | b);
		SetNZ(r);
		return r;
	}

	private byte Eor(byte a, byte b) {
		byte r = (byte)(a ^ b);
		SetNZ(r);
		return r;
	}

	private byte Asl(byte value) {
		CarryFlag = (value & 0x80) != 0;
		byte r = (byte)(value << 1);
		SetNZ(r);
		return r;
	}

	private byte Lsr(byte value) {
		CarryFlag = (value & 0x01) != 0;
		byte r = (byte)(value >> 1);
		SetNZ(r);
		return r;
	}

	private byte Rol(byte value) {
		bool oldCarry = CarryFlag;
		CarryFlag = (value & 0x80) != 0;
		byte r = (byte)((value << 1) | (oldCarry ? 1 : 0));
		SetNZ(r);
		return r;
	}

	private byte Ror(byte value) {
		bool oldCarry = CarryFlag;
		CarryFlag = (value & 0x01) != 0;
		byte r = (byte)((value >> 1) | (oldCarry ? 0x80 : 0));
		SetNZ(r);
		return r;
	}

	private byte Inc(byte value) {
		byte r = (byte)(value + 1);
		SetNZ(r);
		return r;
	}

	private byte Dec(byte value) {
		byte r = (byte)(value - 1);
		SetNZ(r);
		return r;
	}

	#endregion

	#region Opcode Execution

	private int ExecuteOpcode(byte opcode) {
		return opcode switch {
			// MOV A, #imm
			0xe8 => Mov_A_Imm(),
			// MOV A, (X)
			0xe6 => Mov_A_IndX(),
			// MOV A, (X)+
			0xbf => Mov_A_IndXInc(),
			// MOV A, dp
			0xe4 => Mov_A_Dp(),
			// MOV A, dp+X
			0xf4 => Mov_A_DpX(),
			// MOV A, !abs
			0xe5 => Mov_A_Abs(),
			// MOV A, !abs+X
			0xf5 => Mov_A_AbsX(),
			// MOV A, !abs+Y
			0xf6 => Mov_A_AbsY(),
			// MOV A, [dp+X]
			0xe7 => Mov_A_IndDpX(),
			// MOV A, [dp]+Y
			0xf7 => Mov_A_IndDpY(),
			// MOV A, X
			0x7d => Mov_A_X(),
			// MOV A, Y
			0xdd => Mov_A_Y(),

			// MOV X, #imm
			0xcd => Mov_X_Imm(),
			// MOV X, dp
			0xf8 => Mov_X_Dp(),
			// MOV X, dp+Y
			0xf9 => Mov_X_DpY(),
			// MOV X, !abs
			0xe9 => Mov_X_Abs(),
			// MOV X, A
			0x5d => Mov_X_A(),
			// MOV X, SP
			0x9d => Mov_X_SP(),

			// MOV Y, #imm
			0x8d => Mov_Y_Imm(),
			// MOV Y, dp
			0xeb => Mov_Y_Dp(),
			// MOV Y, dp+X
			0xfb => Mov_Y_DpX(),
			// MOV Y, !abs
			0xec => Mov_Y_Abs(),
			// MOV Y, A
			0xfd => Mov_Y_A(),

			// MOV (X), A
			0xc6 => Mov_IndX_A(),
			// MOV (X)+, A
			0xaf => Mov_IndXInc_A(),
			// MOV dp, A
			0xc4 => Mov_Dp_A(),
			// MOV dp+X, A
			0xd4 => Mov_DpX_A(),
			// MOV !abs, A
			0xc5 => Mov_Abs_A(),
			// MOV !abs+X, A
			0xd5 => Mov_AbsX_A(),
			// MOV !abs+Y, A
			0xd6 => Mov_AbsY_A(),
			// MOV [dp+X], A
			0xc7 => Mov_IndDpX_A(),
			// MOV [dp]+Y, A
			0xd7 => Mov_IndDpY_A(),

			// MOV dp, X
			0xd8 => Mov_Dp_X(),
			// MOV dp+Y, X
			0xd9 => Mov_DpY_X(),
			// MOV !abs, X
			0xc9 => Mov_Abs_X(),

			// MOV dp, Y
			0xcb => Mov_Dp_Y(),
			// MOV dp+X, Y
			0xdb => Mov_DpX_Y(),
			// MOV !abs, Y
			0xcc => Mov_Abs_Y(),

			// MOV SP, X
			0xbd => Mov_SP_X(),

			// MOV dp, #imm
			0x8f => Mov_Dp_Imm(),
			// MOV dp, dp
			0xfa => Mov_Dp_Dp(),

			// ADC/SBC/CMP/AND/OR/EOR - A operations
			0x88 => Adc_A_Imm(),
			0x86 => Adc_A_IndX(),
			0x84 => Adc_A_Dp(),
			0x94 => Adc_A_DpX(),
			0x85 => Adc_A_Abs(),
			0x95 => Adc_A_AbsX(),
			0x96 => Adc_A_AbsY(),
			0x87 => Adc_A_IndDpX(),
			0x97 => Adc_A_IndDpY(),

			0xa8 => Sbc_A_Imm(),
			0xa6 => Sbc_A_IndX(),
			0xa4 => Sbc_A_Dp(),
			0xb4 => Sbc_A_DpX(),
			0xa5 => Sbc_A_Abs(),
			0xb5 => Sbc_A_AbsX(),
			0xb6 => Sbc_A_AbsY(),
			0xa7 => Sbc_A_IndDpX(),
			0xb7 => Sbc_A_IndDpY(),

			0x68 => Cmp_A_Imm(),
			0x66 => Cmp_A_IndX(),
			0x64 => Cmp_A_Dp(),
			0x74 => Cmp_A_DpX(),
			0x65 => Cmp_A_Abs(),
			0x75 => Cmp_A_AbsX(),
			0x76 => Cmp_A_AbsY(),
			0x67 => Cmp_A_IndDpX(),
			0x77 => Cmp_A_IndDpY(),

			0x28 => And_A_Imm(),
			0x26 => And_A_IndX(),
			0x24 => And_A_Dp(),
			0x34 => And_A_DpX(),
			0x25 => And_A_Abs(),
			0x35 => And_A_AbsX(),
			0x36 => And_A_AbsY(),
			0x27 => And_A_IndDpX(),
			0x37 => And_A_IndDpY(),

			0x08 => Or_A_Imm(),
			0x06 => Or_A_IndX(),
			0x04 => Or_A_Dp(),
			0x14 => Or_A_DpX(),
			0x05 => Or_A_Abs(),
			0x15 => Or_A_AbsX(),
			0x16 => Or_A_AbsY(),
			0x07 => Or_A_IndDpX(),
			0x17 => Or_A_IndDpY(),

			0x48 => Eor_A_Imm(),
			0x46 => Eor_A_IndX(),
			0x44 => Eor_A_Dp(),
			0x54 => Eor_A_DpX(),
			0x45 => Eor_A_Abs(),
			0x55 => Eor_A_AbsX(),
			0x56 => Eor_A_AbsY(),
			0x47 => Eor_A_IndDpX(),
			0x57 => Eor_A_IndDpY(),

			// CMP X/Y
			0xc8 => Cmp_X_Imm(),
			0x3e => Cmp_X_Dp(),
			0x1e => Cmp_X_Abs(),
			0xad => Cmp_Y_Imm(),
			0x7e => Cmp_Y_Dp(),
			0x5e => Cmp_Y_Abs(),

			// INC/DEC
			0xbc => Inc_A(),
			0x3d => Inc_X(),
			0xfc => Inc_Y(),
			0xab => Inc_Dp(),
			0xbb => Inc_DpX(),
			0xac => Inc_Abs(),

			0x9c => Dec_A(),
			0x1d => Dec_X(),
			0xdc => Dec_Y(),
			0x8b => Dec_Dp(),
			0x9b => Dec_DpX(),
			0x8c => Dec_Abs(),

			// ASL/LSR/ROL/ROR
			0x1c => Asl_A(),
			0x0b => Asl_Dp(),
			0x1b => Asl_DpX(),
			0x0c => Asl_Abs(),

			0x5c => Lsr_A(),
			0x4b => Lsr_Dp(),
			0x5b => Lsr_DpX(),
			0x4c => Lsr_Abs(),

			0x3c => Rol_A(),
			0x2b => Rol_Dp(),
			0x3b => Rol_DpX(),
			0x2c => Rol_Abs(),

			0x7c => Ror_A(),
			0x6b => Ror_Dp(),
			0x7b => Ror_DpX(),
			0x6c => Ror_Abs(),

			// Branches
			0x2f => Bra(),
			0xf0 => Beq(),
			0xd0 => Bne(),
			0xb0 => Bcs(),
			0x90 => Bcc(),
			0x70 => Bvs(),
			0x50 => Bvc(),
			0x30 => Bmi(),
			0x10 => Bpl(),

			// Jumps
			0x5f => Jmp_Abs(),
			0x1f => Jmp_AbsX(),
			0x3f => Call(),
			0x6f => Ret(),
			0x7f => Reti(),

			// Stack
			0x2d => Push_A(),
			0x4d => Push_X(),
			0x6d => Push_Y(),
			0x0d => Push_PSW(),
			0xae => Pop_A(),
			0xce => Pop_X(),
			0xee => Pop_Y(),
			0x8e => Pop_PSW(),

			// Flags
			0x60 => Clrc(),
			0x80 => Setc(),
			0xed => Notc(),
			0xe0 => Clrv(),
			0x20 => Clrp(),
			0x40 => Setp(),
			0xa0 => Ei(),
			0xc0 => Di(),

			// Bit operations
			0x02 => Set1_Dp(0),
			0x22 => Set1_Dp(1),
			0x42 => Set1_Dp(2),
			0x62 => Set1_Dp(3),
			0x82 => Set1_Dp(4),
			0xa2 => Set1_Dp(5),
			0xc2 => Set1_Dp(6),
			0xe2 => Set1_Dp(7),
			0x12 => Clr1_Dp(0),
			0x32 => Clr1_Dp(1),
			0x52 => Clr1_Dp(2),
			0x72 => Clr1_Dp(3),
			0x92 => Clr1_Dp(4),
			0xb2 => Clr1_Dp(5),
			0xd2 => Clr1_Dp(6),
			0xf2 => Clr1_Dp(7),

			// Bit branches (BBC/BBS)
			0x13 => Bbc(0),
			0x33 => Bbc(1),
			0x53 => Bbc(2),
			0x73 => Bbc(3),
			0x93 => Bbc(4),
			0xb3 => Bbc(5),
			0xd3 => Bbc(6),
			0xf3 => Bbc(7),
			0x03 => Bbs(0),
			0x23 => Bbs(1),
			0x43 => Bbs(2),
			0x63 => Bbs(3),
			0x83 => Bbs(4),
			0xa3 => Bbs(5),
			0xc3 => Bbs(6),
			0xe3 => Bbs(7),

			// Carry bit operations
			0x0a => Or1_C_Mem(),
			0x2a => Or1_C_NotMem(),
			0x4a => And1_C_Mem(),
			0x6a => And1_C_NotMem(),
			0x8a => Eor1_C_Mem(),
			0xaa => Mov1_C_Mem(),
			0xca => Mov1_Mem_C(),
			0xea => Not1_Mem(),

			// TSET1/TCLR1
			0x0e => Tset1_Abs(),
			0x4e => Tclr1_Abs(),

			// MUL/DIV
			0xcf => Mul_YA(),
			0x9e => Div_YA_X(),

			// 16-bit operations
			0x3a => Incw_Dp(),
			0x1a => Decw_Dp(),
			0x7a => Addw_YA_Dp(),
			0x9a => Subw_YA_Dp(),
			0x5a => Cmpw_YA_Dp(),
			0xba => Movw_YA_Dp(),
			0xda => Movw_Dp_YA(),

			// Decimal adjust
			0xdf => Daa(),
			0xbe => Das(),

			// XCN (exchange nibbles)
			0x9f => Xcn_A(),

			// CBNE (compare and branch if not equal)
			0x2e => Cbne_Dp(),
			0xde => Cbne_DpX(),

			// DBNZ (decrement and branch if not zero)
			0x6e => Dbnz_Dp(),
			0xfe => Dbnz_Y(),

			// TCALL (table call)
			0x01 => Tcall(0),
			0x11 => Tcall(1),
			0x21 => Tcall(2),
			0x31 => Tcall(3),
			0x41 => Tcall(4),
			0x51 => Tcall(5),
			0x61 => Tcall(6),
			0x71 => Tcall(7),
			0x81 => Tcall(8),
			0x91 => Tcall(9),
			0xa1 => Tcall(10),
			0xb1 => Tcall(11),
			0xc1 => Tcall(12),
			0xd1 => Tcall(13),
			0xe1 => Tcall(14),
			0xf1 => Tcall(15),

			// PCALL (page call)
			0x4f => Pcall(),

			// BRK
			0x0f => Brk(),

			// ADC/SBC dp,dp and dp,#imm
			0x89 => Adc_Dp_Dp(),
			0x98 => Adc_Dp_Imm(),
			0xa9 => Sbc_Dp_Dp(),
			0xb8 => Sbc_Dp_Imm(),

			// CMP dp,dp and dp,#imm
			0x69 => Cmp_Dp_Dp(),
			0x78 => Cmp_Dp_Imm(),

			// AND/OR/EOR dp,dp and dp,#imm
			0x29 => And_Dp_Dp(),
			0x38 => And_Dp_Imm(),
			0x09 => Or_Dp_Dp(),
			0x18 => Or_Dp_Imm(),
			0x49 => Eor_Dp_Dp(),
			0x58 => Eor_Dp_Imm(),

			// (X),(Y) operations
			0x99 => Adc_IndX_IndY(),
			0xb9 => Sbc_IndX_IndY(),
			0x79 => Cmp_IndX_IndY(),
			0x39 => And_IndX_IndY(),
			0x19 => Or_IndX_IndY(),
			0x59 => Eor_IndX_IndY(),

			// Special
			0x00 => Nop(),
			0xef => Sleep(),
			0xff => Stop(),
		};
	}

	#endregion

	#region MOV Instructions

	private int Mov_A_Imm() { _a = FetchByte(); SetNZ(_a); return 2; }
	private int Mov_A_IndX() { _a = ReadByte(DpAddress(_x)); SetNZ(_a); return 3; }
	private int Mov_A_IndXInc() { _a = ReadByte(DpAddress(_x)); _x++; SetNZ(_a); return 4; }
	private int Mov_A_Dp() { _a = ReadByte(DpAddress(FetchByte())); SetNZ(_a); return 3; }
	private int Mov_A_DpX() { _a = ReadByte(DpAddress((byte)(FetchByte() + _x))); SetNZ(_a); return 4; }
	private int Mov_A_Abs() { _a = ReadByte(FetchWord()); SetNZ(_a); return 4; }
	private int Mov_A_AbsX() { _a = ReadByte(FetchWord() + _x); SetNZ(_a); return 5; }
	private int Mov_A_AbsY() { _a = ReadByte(FetchWord() + _y); SetNZ(_a); return 5; }
	private int Mov_A_IndDpX() { _a = ReadByte(ReadWord(DpAddress((byte)(FetchByte() + _x)))); SetNZ(_a); return 6; }
	private int Mov_A_IndDpY() { _a = ReadByte(ReadWord(DpAddress(FetchByte())) + _y); SetNZ(_a); return 6; }
	private int Mov_A_X() { _a = _x; SetNZ(_a); return 2; }
	private int Mov_A_Y() { _a = _y; SetNZ(_a); return 2; }

	private int Mov_X_Imm() { _x = FetchByte(); SetNZ(_x); return 2; }
	private int Mov_X_Dp() { _x = ReadByte(DpAddress(FetchByte())); SetNZ(_x); return 3; }
	private int Mov_X_DpY() { _x = ReadByte(DpAddress((byte)(FetchByte() + _y))); SetNZ(_x); return 4; }
	private int Mov_X_Abs() { _x = ReadByte(FetchWord()); SetNZ(_x); return 4; }
	private int Mov_X_A() { _x = _a; SetNZ(_x); return 2; }
	private int Mov_X_SP() { _x = _sp; SetNZ(_x); return 2; }

	private int Mov_Y_Imm() { _y = FetchByte(); SetNZ(_y); return 2; }
	private int Mov_Y_Dp() { _y = ReadByte(DpAddress(FetchByte())); SetNZ(_y); return 3; }
	private int Mov_Y_DpX() { _y = ReadByte(DpAddress((byte)(FetchByte() + _x))); SetNZ(_y); return 4; }
	private int Mov_Y_Abs() { _y = ReadByte(FetchWord()); SetNZ(_y); return 4; }
	private int Mov_Y_A() { _y = _a; SetNZ(_y); return 2; }

	private int Mov_IndX_A() { WriteByte(DpAddress(_x), _a); return 4; }
	private int Mov_IndXInc_A() { WriteByte(DpAddress(_x), _a); _x++; return 4; }
	private int Mov_Dp_A() { WriteByte(DpAddress(FetchByte()), _a); return 4; }
	private int Mov_DpX_A() { WriteByte(DpAddress((byte)(FetchByte() + _x)), _a); return 5; }
	private int Mov_Abs_A() { WriteByte(FetchWord(), _a); return 5; }
	private int Mov_AbsX_A() { WriteByte(FetchWord() + _x, _a); return 6; }
	private int Mov_AbsY_A() { WriteByte(FetchWord() + _y, _a); return 6; }
	private int Mov_IndDpX_A() { WriteByte(ReadWord(DpAddress((byte)(FetchByte() + _x))), _a); return 7; }
	private int Mov_IndDpY_A() { WriteByte(ReadWord(DpAddress(FetchByte())) + _y, _a); return 7; }

	private int Mov_Dp_X() { WriteByte(DpAddress(FetchByte()), _x); return 4; }
	private int Mov_DpY_X() { WriteByte(DpAddress((byte)(FetchByte() + _y)), _x); return 5; }
	private int Mov_Abs_X() { WriteByte(FetchWord(), _x); return 5; }

	private int Mov_Dp_Y() { WriteByte(DpAddress(FetchByte()), _y); return 4; }
	private int Mov_DpX_Y() { WriteByte(DpAddress((byte)(FetchByte() + _x)), _y); return 5; }
	private int Mov_Abs_Y() { WriteByte(FetchWord(), _y); return 5; }

	private int Mov_SP_X() { _sp = _x; return 2; }

	private int Mov_Dp_Imm() { byte imm = FetchByte(); WriteByte(DpAddress(FetchByte()), imm); return 5; }
	private int Mov_Dp_Dp() { byte src = ReadByte(DpAddress(FetchByte())); WriteByte(DpAddress(FetchByte()), src); return 5; }

	#endregion

	#region ALU Instruction Implementations

	private int Adc_A_Imm() { _a = Adc(_a, FetchByte()); return 2; }
	private int Adc_A_IndX() { _a = Adc(_a, ReadByte(DpAddress(_x))); return 3; }
	private int Adc_A_Dp() { _a = Adc(_a, ReadByte(DpAddress(FetchByte()))); return 3; }
	private int Adc_A_DpX() { _a = Adc(_a, ReadByte(DpAddress((byte)(FetchByte() + _x)))); return 4; }
	private int Adc_A_Abs() { _a = Adc(_a, ReadByte(FetchWord())); return 4; }
	private int Adc_A_AbsX() { _a = Adc(_a, ReadByte(FetchWord() + _x)); return 5; }
	private int Adc_A_AbsY() { _a = Adc(_a, ReadByte(FetchWord() + _y)); return 5; }
	private int Adc_A_IndDpX() { _a = Adc(_a, ReadByte(ReadWord(DpAddress((byte)(FetchByte() + _x))))); return 6; }
	private int Adc_A_IndDpY() { _a = Adc(_a, ReadByte(ReadWord(DpAddress(FetchByte())) + _y)); return 6; }

	private int Sbc_A_Imm() { _a = Sbc(_a, FetchByte()); return 2; }
	private int Sbc_A_IndX() { _a = Sbc(_a, ReadByte(DpAddress(_x))); return 3; }
	private int Sbc_A_Dp() { _a = Sbc(_a, ReadByte(DpAddress(FetchByte()))); return 3; }
	private int Sbc_A_DpX() { _a = Sbc(_a, ReadByte(DpAddress((byte)(FetchByte() + _x)))); return 4; }
	private int Sbc_A_Abs() { _a = Sbc(_a, ReadByte(FetchWord())); return 4; }
	private int Sbc_A_AbsX() { _a = Sbc(_a, ReadByte(FetchWord() + _x)); return 5; }
	private int Sbc_A_AbsY() { _a = Sbc(_a, ReadByte(FetchWord() + _y)); return 5; }
	private int Sbc_A_IndDpX() { _a = Sbc(_a, ReadByte(ReadWord(DpAddress((byte)(FetchByte() + _x))))); return 6; }
	private int Sbc_A_IndDpY() { _a = Sbc(_a, ReadByte(ReadWord(DpAddress(FetchByte())) + _y)); return 6; }

	private int Cmp_A_Imm() { Cmp(_a, FetchByte()); return 2; }
	private int Cmp_A_IndX() { Cmp(_a, ReadByte(DpAddress(_x))); return 3; }
	private int Cmp_A_Dp() { Cmp(_a, ReadByte(DpAddress(FetchByte()))); return 3; }
	private int Cmp_A_DpX() { Cmp(_a, ReadByte(DpAddress((byte)(FetchByte() + _x)))); return 4; }
	private int Cmp_A_Abs() { Cmp(_a, ReadByte(FetchWord())); return 4; }
	private int Cmp_A_AbsX() { Cmp(_a, ReadByte(FetchWord() + _x)); return 5; }
	private int Cmp_A_AbsY() { Cmp(_a, ReadByte(FetchWord() + _y)); return 5; }
	private int Cmp_A_IndDpX() { Cmp(_a, ReadByte(ReadWord(DpAddress((byte)(FetchByte() + _x))))); return 6; }
	private int Cmp_A_IndDpY() { Cmp(_a, ReadByte(ReadWord(DpAddress(FetchByte())) + _y)); return 6; }

	private int And_A_Imm() { _a = And(_a, FetchByte()); return 2; }
	private int And_A_IndX() { _a = And(_a, ReadByte(DpAddress(_x))); return 3; }
	private int And_A_Dp() { _a = And(_a, ReadByte(DpAddress(FetchByte()))); return 3; }
	private int And_A_DpX() { _a = And(_a, ReadByte(DpAddress((byte)(FetchByte() + _x)))); return 4; }
	private int And_A_Abs() { _a = And(_a, ReadByte(FetchWord())); return 4; }
	private int And_A_AbsX() { _a = And(_a, ReadByte(FetchWord() + _x)); return 5; }
	private int And_A_AbsY() { _a = And(_a, ReadByte(FetchWord() + _y)); return 5; }
	private int And_A_IndDpX() { _a = And(_a, ReadByte(ReadWord(DpAddress((byte)(FetchByte() + _x))))); return 6; }
	private int And_A_IndDpY() { _a = And(_a, ReadByte(ReadWord(DpAddress(FetchByte())) + _y)); return 6; }

	private int Or_A_Imm() { _a = Or(_a, FetchByte()); return 2; }
	private int Or_A_IndX() { _a = Or(_a, ReadByte(DpAddress(_x))); return 3; }
	private int Or_A_Dp() { _a = Or(_a, ReadByte(DpAddress(FetchByte()))); return 3; }
	private int Or_A_DpX() { _a = Or(_a, ReadByte(DpAddress((byte)(FetchByte() + _x)))); return 4; }
	private int Or_A_Abs() { _a = Or(_a, ReadByte(FetchWord())); return 4; }
	private int Or_A_AbsX() { _a = Or(_a, ReadByte(FetchWord() + _x)); return 5; }
	private int Or_A_AbsY() { _a = Or(_a, ReadByte(FetchWord() + _y)); return 5; }
	private int Or_A_IndDpX() { _a = Or(_a, ReadByte(ReadWord(DpAddress((byte)(FetchByte() + _x))))); return 6; }
	private int Or_A_IndDpY() { _a = Or(_a, ReadByte(ReadWord(DpAddress(FetchByte())) + _y)); return 6; }

	private int Eor_A_Imm() { _a = Eor(_a, FetchByte()); return 2; }
	private int Eor_A_IndX() { _a = Eor(_a, ReadByte(DpAddress(_x))); return 3; }
	private int Eor_A_Dp() { _a = Eor(_a, ReadByte(DpAddress(FetchByte()))); return 3; }
	private int Eor_A_DpX() { _a = Eor(_a, ReadByte(DpAddress((byte)(FetchByte() + _x)))); return 4; }
	private int Eor_A_Abs() { _a = Eor(_a, ReadByte(FetchWord())); return 4; }
	private int Eor_A_AbsX() { _a = Eor(_a, ReadByte(FetchWord() + _x)); return 5; }
	private int Eor_A_AbsY() { _a = Eor(_a, ReadByte(FetchWord() + _y)); return 5; }
	private int Eor_A_IndDpX() { _a = Eor(_a, ReadByte(ReadWord(DpAddress((byte)(FetchByte() + _x))))); return 6; }
	private int Eor_A_IndDpY() { _a = Eor(_a, ReadByte(ReadWord(DpAddress(FetchByte())) + _y)); return 6; }

	private int Cmp_X_Imm() { Cmp(_x, FetchByte()); return 2; }
	private int Cmp_X_Dp() { Cmp(_x, ReadByte(DpAddress(FetchByte()))); return 3; }
	private int Cmp_X_Abs() { Cmp(_x, ReadByte(FetchWord())); return 4; }
	private int Cmp_Y_Imm() { Cmp(_y, FetchByte()); return 2; }
	private int Cmp_Y_Dp() { Cmp(_y, ReadByte(DpAddress(FetchByte()))); return 3; }
	private int Cmp_Y_Abs() { Cmp(_y, ReadByte(FetchWord())); return 4; }

	#endregion

	#region Inc/Dec/Shift Instructions

	private int Inc_A() { _a = Inc(_a); return 2; }
	private int Inc_X() { _x = Inc(_x); return 2; }
	private int Inc_Y() { _y = Inc(_y); return 2; }
	private int Inc_Dp() { int addr = DpAddress(FetchByte()); WriteByte(addr, Inc(ReadByte(addr))); return 4; }
	private int Inc_DpX() { int addr = DpAddress((byte)(FetchByte() + _x)); WriteByte(addr, Inc(ReadByte(addr))); return 5; }
	private int Inc_Abs() { int addr = FetchWord(); WriteByte(addr, Inc(ReadByte(addr))); return 5; }

	private int Dec_A() { _a = Dec(_a); return 2; }
	private int Dec_X() { _x = Dec(_x); return 2; }
	private int Dec_Y() { _y = Dec(_y); return 2; }
	private int Dec_Dp() { int addr = DpAddress(FetchByte()); WriteByte(addr, Dec(ReadByte(addr))); return 4; }
	private int Dec_DpX() { int addr = DpAddress((byte)(FetchByte() + _x)); WriteByte(addr, Dec(ReadByte(addr))); return 5; }
	private int Dec_Abs() { int addr = FetchWord(); WriteByte(addr, Dec(ReadByte(addr))); return 5; }

	private int Asl_A() { _a = Asl(_a); return 2; }
	private int Asl_Dp() { int addr = DpAddress(FetchByte()); WriteByte(addr, Asl(ReadByte(addr))); return 4; }
	private int Asl_DpX() { int addr = DpAddress((byte)(FetchByte() + _x)); WriteByte(addr, Asl(ReadByte(addr))); return 5; }
	private int Asl_Abs() { int addr = FetchWord(); WriteByte(addr, Asl(ReadByte(addr))); return 5; }

	private int Lsr_A() { _a = Lsr(_a); return 2; }
	private int Lsr_Dp() { int addr = DpAddress(FetchByte()); WriteByte(addr, Lsr(ReadByte(addr))); return 4; }
	private int Lsr_DpX() { int addr = DpAddress((byte)(FetchByte() + _x)); WriteByte(addr, Lsr(ReadByte(addr))); return 5; }
	private int Lsr_Abs() { int addr = FetchWord(); WriteByte(addr, Lsr(ReadByte(addr))); return 5; }

	private int Rol_A() { _a = Rol(_a); return 2; }
	private int Rol_Dp() { int addr = DpAddress(FetchByte()); WriteByte(addr, Rol(ReadByte(addr))); return 4; }
	private int Rol_DpX() { int addr = DpAddress((byte)(FetchByte() + _x)); WriteByte(addr, Rol(ReadByte(addr))); return 5; }
	private int Rol_Abs() { int addr = FetchWord(); WriteByte(addr, Rol(ReadByte(addr))); return 5; }

	private int Ror_A() { _a = Ror(_a); return 2; }
	private int Ror_Dp() { int addr = DpAddress(FetchByte()); WriteByte(addr, Ror(ReadByte(addr))); return 4; }
	private int Ror_DpX() { int addr = DpAddress((byte)(FetchByte() + _x)); WriteByte(addr, Ror(ReadByte(addr))); return 5; }
	private int Ror_Abs() { int addr = FetchWord(); WriteByte(addr, Ror(ReadByte(addr))); return 5; }

	#endregion

	#region Branch/Jump Instructions

	private int Branch(bool condition) {
		sbyte offset = (sbyte)FetchByte();
		if (condition) {
			_pc = (ushort)(_pc + offset);
			return 4;
		}
		return 2;
	}

	private int Bra() => Branch(true);
	private int Beq() => Branch(ZeroFlag);
	private int Bne() => Branch(!ZeroFlag);
	private int Bcs() => Branch(CarryFlag);
	private int Bcc() => Branch(!CarryFlag);
	private int Bvs() => Branch(OverflowFlag);
	private int Bvc() => Branch(!OverflowFlag);
	private int Bmi() => Branch(NegativeFlag);
	private int Bpl() => Branch(!NegativeFlag);

	private int Jmp_Abs() { _pc = FetchWord(); return 3; }
	private int Jmp_AbsX() { _pc = (ushort)(FetchWord() + _x); return 6; }

	private int Call() {
		ushort addr = FetchWord();
		PushWord(_pc);
		_pc = addr;
		return 8;
	}

	private int Ret() { _pc = PopWord(); return 5; }
	private int Reti() { _psw = PopByte(); _pc = PopWord(); return 6; }

	#endregion

	#region Stack Instructions

	private int Push_A() { PushByte(_a); return 4; }
	private int Push_X() { PushByte(_x); return 4; }
	private int Push_Y() { PushByte(_y); return 4; }
	private int Push_PSW() { PushByte(_psw); return 4; }
	private int Pop_A() { _a = PopByte(); return 4; }
	private int Pop_X() { _x = PopByte(); return 4; }
	private int Pop_Y() { _y = PopByte(); return 4; }
	private int Pop_PSW() { _psw = PopByte(); return 4; }

	#endregion

	#region Flag Instructions

	private int Clrc() { CarryFlag = false; return 2; }
	private int Setc() { CarryFlag = true; return 2; }
	private int Notc() { CarryFlag = !CarryFlag; return 3; }
	private int Clrv() { OverflowFlag = false; HalfCarryFlag = false; return 2; }
	private int Clrp() { DirectPageFlag = false; return 2; }
	private int Setp() { DirectPageFlag = true; return 2; }
	private int Ei() { InterruptFlag = true; return 3; }
	private int Di() { InterruptFlag = false; return 3; }

	#endregion

	#region Special Instructions

	private int Nop() => 2;
	private int Sleep() { _sleeping = true; return 3; }
	private int Stop() { _stopped = true; return 2; }

	private int UnimplementedOpcode(byte opcode) {
		// Log or handle unimplemented opcode
		// For now, just consume 2 cycles and continue
		return 2;
	}

	#endregion

	#region Bit Operations

	private int Set1_Dp(int bit) {
		int addr = DpAddress(FetchByte());
		WriteByte(addr, (byte)(ReadByte(addr) | (1 << bit)));
		return 4;
	}

	private int Clr1_Dp(int bit) {
		int addr = DpAddress(FetchByte());
		WriteByte(addr, (byte)(ReadByte(addr) & ~(1 << bit)));
		return 4;
	}

	private int Bbs(int bit) {
		byte dp = FetchByte();
		sbyte offset = (sbyte)FetchByte();
		if ((ReadByte(DpAddress(dp)) & (1 << bit)) != 0) {
			_pc = (ushort)(_pc + offset);
			return 7;
		}
		return 5;
	}

	private int Bbc(int bit) {
		byte dp = FetchByte();
		sbyte offset = (sbyte)FetchByte();
		if ((ReadByte(DpAddress(dp)) & (1 << bit)) == 0) {
			_pc = (ushort)(_pc + offset);
			return 7;
		}
		return 5;
	}

	private (int addr, int bit) FetchMemBit() {
		ushort word = FetchWord();
		return (word & 0x1fff, word >> 13);
	}

	private int Or1_C_Mem() {
		var (addr, bit) = FetchMemBit();
		if ((ReadByte(addr) & (1 << bit)) != 0) CarryFlag = true;
		return 5;
	}

	private int Or1_C_NotMem() {
		var (addr, bit) = FetchMemBit();
		if ((ReadByte(addr) & (1 << bit)) == 0) CarryFlag = true;
		return 5;
	}

	private int And1_C_Mem() {
		var (addr, bit) = FetchMemBit();
		if ((ReadByte(addr) & (1 << bit)) == 0) CarryFlag = false;
		return 4;
	}

	private int And1_C_NotMem() {
		var (addr, bit) = FetchMemBit();
		if ((ReadByte(addr) & (1 << bit)) != 0) CarryFlag = false;
		return 4;
	}

	private int Eor1_C_Mem() {
		var (addr, bit) = FetchMemBit();
		if ((ReadByte(addr) & (1 << bit)) != 0) CarryFlag = !CarryFlag;
		return 5;
	}

	private int Mov1_C_Mem() {
		var (addr, bit) = FetchMemBit();
		CarryFlag = (ReadByte(addr) & (1 << bit)) != 0;
		return 4;
	}

	private int Mov1_Mem_C() {
		var (addr, bit) = FetchMemBit();
		byte val = ReadByte(addr);
		if (CarryFlag) val |= (byte)(1 << bit);
		else val &= (byte)~(1 << bit);
		WriteByte(addr, val);
		return 6;
	}

	private int Not1_Mem() {
		var (addr, bit) = FetchMemBit();
		WriteByte(addr, (byte)(ReadByte(addr) ^ (1 << bit)));
		return 5;
	}

	private int Tset1_Abs() {
		int addr = FetchWord();
		byte val = ReadByte(addr);
		SetNZ((byte)(_a - val));
		WriteByte(addr, (byte)(val | _a));
		return 6;
	}

	private int Tclr1_Abs() {
		int addr = FetchWord();
		byte val = ReadByte(addr);
		SetNZ((byte)(_a - val));
		WriteByte(addr, (byte)(val & ~_a));
		return 6;
	}

	#endregion

	#region MUL/DIV Operations

	private int Mul_YA() {
		ushort result = (ushort)(_y * _a);
		_a = (byte)(result & 0xff);
		_y = (byte)(result >> 8);
		SetNZ(_y);
		return 9;
	}

	private int Div_YA_X() {
		if (_x == 0) {
			// Division by zero
			_a = 0xff;
			_y = 0xff;
			OverflowFlag = true;
			HalfCarryFlag = true;
		} else {
			ushort ya = (ushort)((_y << 8) | _a);
			OverflowFlag = _y >= _x;
			HalfCarryFlag = (_y & 0x0f) >= (_x & 0x0f);
			_a = (byte)(ya / _x);
			_y = (byte)(ya % _x);
		}
		SetNZ(_a);
		return 12;
	}

	#endregion

	#region 16-bit Operations

	private ushort GetYA() => (ushort)((_y << 8) | _a);
	private void SetYA(ushort value) { _a = (byte)(value & 0xff); _y = (byte)(value >> 8); }

	private int Incw_Dp() {
		int addr = DpAddress(FetchByte());
		ushort val = (ushort)(ReadWord(addr) + 1);
		WriteWord(addr, val);
		SetNZ16(val);
		return 6;
	}

	private int Decw_Dp() {
		int addr = DpAddress(FetchByte());
		ushort val = (ushort)(ReadWord(addr) - 1);
		WriteWord(addr, val);
		SetNZ16(val);
		return 6;
	}

	private int Addw_YA_Dp() {
		ushort ya = GetYA();
		ushort mem = ReadWord(DpAddress(FetchByte()));
		int result = ya + mem;
		CarryFlag = result > 0xffff;
		OverflowFlag = ((~(ya ^ mem) & (ya ^ result)) & 0x8000) != 0;
		HalfCarryFlag = ((ya & 0x0fff) + (mem & 0x0fff)) > 0x0fff;
		SetYA((ushort)result);
		SetNZ16(GetYA());
		return 5;
	}

	private int Subw_YA_Dp() {
		ushort ya = GetYA();
		ushort mem = ReadWord(DpAddress(FetchByte()));
		int result = ya - mem;
		CarryFlag = result >= 0;
		OverflowFlag = (((ya ^ mem) & (ya ^ result)) & 0x8000) != 0;
		HalfCarryFlag = ((ya & 0x0fff) - (mem & 0x0fff)) >= 0;
		SetYA((ushort)result);
		SetNZ16(GetYA());
		return 5;
	}

	private int Cmpw_YA_Dp() {
		ushort ya = GetYA();
		ushort mem = ReadWord(DpAddress(FetchByte()));
		int result = ya - mem;
		CarryFlag = result >= 0;
		SetNZ16((ushort)result);
		return 4;
	}

	private int Movw_YA_Dp() {
		SetYA(ReadWord(DpAddress(FetchByte())));
		SetNZ16(GetYA());
		return 5;
	}

	private int Movw_Dp_YA() {
		WriteWord(DpAddress(FetchByte()), GetYA());
		return 5;
	}

	#endregion

	#region Decimal Adjust

	private int Daa() {
		if ((_a & 0x0f) > 9 || HalfCarryFlag) {
			_a += 6;
			if (_a < 6) CarryFlag = true;
		}
		if (_a > 0x9f || CarryFlag) {
			_a += 0x60;
			CarryFlag = true;
		}
		SetNZ(_a);
		return 3;
	}

	private int Das() {
		if ((_a & 0x0f) > 9 || !HalfCarryFlag) {
			_a -= 6;
		}
		if (_a > 0x9f || !CarryFlag) {
			_a -= 0x60;
			CarryFlag = false;
		}
		SetNZ(_a);
		return 3;
	}

	#endregion

	#region Misc Instructions

	private int Xcn_A() {
		_a = (byte)((_a >> 4) | (_a << 4));
		SetNZ(_a);
		return 5;
	}

	private int Cbne_Dp() {
		byte dp = FetchByte();
		sbyte offset = (sbyte)FetchByte();
		if (_a != ReadByte(DpAddress(dp))) {
			_pc = (ushort)(_pc + offset);
			return 7;
		}
		return 5;
	}

	private int Cbne_DpX() {
		byte dp = FetchByte();
		sbyte offset = (sbyte)FetchByte();
		if (_a != ReadByte(DpAddress((byte)(dp + _x)))) {
			_pc = (ushort)(_pc + offset);
			return 8;
		}
		return 6;
	}

	private int Dbnz_Dp() {
		byte dp = FetchByte();
		sbyte offset = (sbyte)FetchByte();
		int addr = DpAddress(dp);
		byte val = (byte)(ReadByte(addr) - 1);
		WriteByte(addr, val);
		if (val != 0) {
			_pc = (ushort)(_pc + offset);
			return 7;
		}
		return 5;
	}

	private int Dbnz_Y() {
		sbyte offset = (sbyte)FetchByte();
		_y--;
		if (_y != 0) {
			_pc = (ushort)(_pc + offset);
			return 6;
		}
		return 4;
	}

	private int Tcall(int n) {
		PushWord(_pc);
		_pc = ReadWord(0xffde - (n * 2));
		return 8;
	}

	private int Pcall() {
		byte page = FetchByte();
		PushWord(_pc);
		_pc = (ushort)(0xff00 | page);
		return 6;
	}

	private int Brk() {
		PushWord(_pc);
		PushByte(_psw);
		_pc = ReadWord(0xffde);
		BreakFlag = true;
		InterruptFlag = false;
		return 8;
	}

	#endregion

	#region Memory-to-Memory ALU Operations

	private int Adc_Dp_Dp() {
		byte src = ReadByte(DpAddress(FetchByte()));
		int dstAddr = DpAddress(FetchByte());
		WriteByte(dstAddr, Adc(ReadByte(dstAddr), src));
		return 6;
	}

	private int Adc_Dp_Imm() {
		byte imm = FetchByte();
		int addr = DpAddress(FetchByte());
		WriteByte(addr, Adc(ReadByte(addr), imm));
		return 5;
	}

	private int Sbc_Dp_Dp() {
		byte src = ReadByte(DpAddress(FetchByte()));
		int dstAddr = DpAddress(FetchByte());
		WriteByte(dstAddr, Sbc(ReadByte(dstAddr), src));
		return 6;
	}

	private int Sbc_Dp_Imm() {
		byte imm = FetchByte();
		int addr = DpAddress(FetchByte());
		WriteByte(addr, Sbc(ReadByte(addr), imm));
		return 5;
	}

	private int Cmp_Dp_Dp() {
		byte src = ReadByte(DpAddress(FetchByte()));
		int dstAddr = DpAddress(FetchByte());
		Cmp(ReadByte(dstAddr), src);
		return 6;
	}

	private int Cmp_Dp_Imm() {
		byte imm = FetchByte();
		int addr = DpAddress(FetchByte());
		Cmp(ReadByte(addr), imm);
		return 5;
	}

	private int And_Dp_Dp() {
		byte src = ReadByte(DpAddress(FetchByte()));
		int dstAddr = DpAddress(FetchByte());
		WriteByte(dstAddr, And(ReadByte(dstAddr), src));
		return 6;
	}

	private int And_Dp_Imm() {
		byte imm = FetchByte();
		int addr = DpAddress(FetchByte());
		WriteByte(addr, And(ReadByte(addr), imm));
		return 5;
	}

	private int Or_Dp_Dp() {
		byte src = ReadByte(DpAddress(FetchByte()));
		int dstAddr = DpAddress(FetchByte());
		WriteByte(dstAddr, Or(ReadByte(dstAddr), src));
		return 6;
	}

	private int Or_Dp_Imm() {
		byte imm = FetchByte();
		int addr = DpAddress(FetchByte());
		WriteByte(addr, Or(ReadByte(addr), imm));
		return 5;
	}

	private int Eor_Dp_Dp() {
		byte src = ReadByte(DpAddress(FetchByte()));
		int dstAddr = DpAddress(FetchByte());
		WriteByte(dstAddr, Eor(ReadByte(dstAddr), src));
		return 6;
	}

	private int Eor_Dp_Imm() {
		byte imm = FetchByte();
		int addr = DpAddress(FetchByte());
		WriteByte(addr, Eor(ReadByte(addr), imm));
		return 5;
	}

	private int Adc_IndX_IndY() {
		byte val = Adc(ReadByte(DpAddress(_x)), ReadByte(DpAddress(_y)));
		WriteByte(DpAddress(_x), val);
		return 5;
	}

	private int Sbc_IndX_IndY() {
		byte val = Sbc(ReadByte(DpAddress(_x)), ReadByte(DpAddress(_y)));
		WriteByte(DpAddress(_x), val);
		return 5;
	}

	private int Cmp_IndX_IndY() {
		Cmp(ReadByte(DpAddress(_x)), ReadByte(DpAddress(_y)));
		return 5;
	}

	private int And_IndX_IndY() {
		byte val = And(ReadByte(DpAddress(_x)), ReadByte(DpAddress(_y)));
		WriteByte(DpAddress(_x), val);
		return 5;
	}

	private int Or_IndX_IndY() {
		byte val = Or(ReadByte(DpAddress(_x)), ReadByte(DpAddress(_y)));
		WriteByte(DpAddress(_x), val);
		return 5;
	}

	private int Eor_IndX_IndY() {
		byte val = Eor(ReadByte(DpAddress(_x)), ReadByte(DpAddress(_y)));
		WriteByte(DpAddress(_x), val);
		return 5;
	}

	#endregion
}
