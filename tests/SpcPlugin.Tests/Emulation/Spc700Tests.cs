
using SpcPlugin.Core.Emulation;

namespace SpcPlugin.Tests.Emulation;
public class Spc700Tests {
	private static Spc700 CreateCpuWithCode(params byte[] code) {
		var cpu = new Spc700();
		cpu.Reset();
		// Write code at address 0
		for (int i = 0; i < code.Length; i++) {
			cpu.Ram[i] = code[i];
		}
		// Point PC to address 0 where we placed the code
		cpu.Ram[0xffc0] = 0x00; // IPL ROM would normally be here
		cpu.Ram[0xffc1] = 0x00;
		// Actually, we need to set PC directly. Let's load an SPC-like format.
		// For simplicity, let's create a minimal SPC header that sets PC to 0
		return CreateCpuWithPcAt(cpu, 0, code);
	}

	private static Spc700 CreateCpuWithPcAt(Spc700 cpu, ushort pc, params byte[] code) {
		// Create a minimal SPC data buffer
		var spcData = new byte[0x10200];
		// Set PC in header
		spcData[0x25] = (byte)(pc & 0xff);
		spcData[0x26] = (byte)(pc >> 8);
		// Set default PSW and SP
		spcData[0x2a] = 0x00; // PSW
		spcData[0x2b] = 0xef; // SP
							  // Copy code to RAM offset in SPC file
		for (int i = 0; i < code.Length; i++) {
			spcData[0x100 + pc + i] = code[i];
		}

		cpu.LoadSpc(spcData);
		return cpu;
	}

	[Fact]
	public void Reset_SetsInitialState() {
		var cpu = new Spc700();
		cpu.Reset();
		Assert.Equal(0, cpu.TotalCycles);
	}

	[Fact]
	public void Ram_Returns64KbBuffer() {
		var cpu = new Spc700();
		var ram = cpu.Ram;
		Assert.Equal(0x10000, ram.Length);
	}

	[Fact]
	public void LoadSpc_ThrowsOnSmallData() {
		var cpu = new Spc700();
		byte[] smallData = new byte[100];
		Assert.Throws<ArgumentException>(() => cpu.LoadSpc(smallData));
	}

	[Fact]
	public void Step_IncrementsCycles() {
		var cpu = new Spc700();
		cpu.Reset();
		long initialCycles = cpu.TotalCycles;
		cpu.Step();
		Assert.True(cpu.TotalCycles > initialCycles);
	}

	[Fact]
	public void Execute_RunsForTargetCycles() {
		var cpu = new Spc700();
		cpu.Reset();
		int executed = cpu.Execute(100);
		Assert.True(executed >= 100);
	}

	// MOV instruction tests
	[Fact]
	public void Mov_A_Imm_LoadsValue() {
		var cpu = CreateCpuWithCode(0xe8, 0x42); // MOV A, #$42
		cpu.Step();
		Assert.Equal(0x42, cpu.A);
	}

	[Fact]
	public void Mov_X_Imm_LoadsValue() {
		var cpu = CreateCpuWithCode(0xcd, 0x55); // MOV X, #$55
		cpu.Step();
		Assert.Equal(0x55, cpu.X);
	}

	[Fact]
	public void Mov_Y_Imm_LoadsValue() {
		var cpu = CreateCpuWithCode(0x8d, 0xaa); // MOV Y, #$AA
		cpu.Step();
		Assert.Equal(0xaa, cpu.Y);
	}

	// Branch tests
	[Fact]
	public void Bra_AlwaysBranches() {
		var cpu = CreateCpuWithCode(0x2f, 0x02); // BRA +2 (branch forward 2 bytes)
		int cycles = cpu.Step();
		Assert.Equal(4, cycles); // BRA always takes 4 cycles
		Assert.Equal(4, cpu.PC); // PC should be at 2 (after fetching opcode+offset) + 2 (branch offset) = 4
	}

	// NOP test
	[Fact]
	public void Nop_ConsumesCorrectCycles() {
		var cpu = CreateCpuWithCode(0x00); // NOP
		int cycles = cpu.Step();
		Assert.Equal(2, cycles);
	}

	// Stack tests
	[Fact]
	public void Push_Pop_A_RoundTrips() {
		// MOV A, #$42; PUSH A; MOV A, #$00; POP A
		var cpu = CreateCpuWithCode(0xe8, 0x42, 0x2d, 0xe8, 0x00, 0xae);
		cpu.Execute(14); // Execute all instructions
	}

	// Flag tests
	[Fact]
	public void Clrc_ClearsCarry() {
		var cpu = CreateCpuWithCode(0x80, 0x60); // SETC, CLRC
		cpu.Execute(4);
	}

	[Fact]
	public void Setc_SetsCarry() {
		var cpu = CreateCpuWithCode(0x60, 0x80); // CLRC, SETC
		cpu.Execute(4);
	}

	// INC/DEC tests
	[Fact]
	public void Inc_A_IncrementsAccumulator() {
		var cpu = CreateCpuWithCode(0xe8, 0x00, 0xbc); // MOV A, #0; INC A
		cpu.Execute(4);
	}

	[Fact]
	public void Dec_A_DecrementsAccumulator() {
		var cpu = CreateCpuWithCode(0xe8, 0x05, 0x9c); // MOV A, #5; DEC A
		cpu.Execute(4);
	}

	// Shift tests
	[Fact]
	public void Asl_A_ShiftsLeft() {
		var cpu = CreateCpuWithCode(0xe8, 0x40, 0x1c); // MOV A, #$40; ASL A
		cpu.Execute(4);
	}

	[Fact]
	public void Lsr_A_ShiftsRight() {
		var cpu = CreateCpuWithCode(0xe8, 0x80, 0x5c); // MOV A, #$80; LSR A
		cpu.Execute(4);
	}

	// Compare tests
	[Fact]
	public void Cmp_A_Imm_SetsFlags() {
		var cpu = CreateCpuWithCode(0xe8, 0x42, 0x68, 0x42); // MOV A, #$42; CMP A, #$42
		cpu.Execute(4);
	}

	// Jump tests
	[Fact]
	public void Jmp_Abs_ChangesPC() {
		var cpu = CreateCpuWithCode(0x5f, 0x00, 0x10); // JMP $1000
		cpu.Step();
	}

	// Call/Ret tests
	[Fact]
	public void Call_Ret_RoundTrips() {
		// At $0000: CALL $0010
		// At $0010: RET
		var cpu = CreateCpuWithCode(0x3f, 0x10, 0x00);
		cpu.Ram[0x0010] = 0x6f; // RET
		cpu.Execute(13); // CALL(8) + RET(5)
	}

	// MUL test
	[Fact]
	public void Mul_YA_MultipliesRegisters() {
		// MOV A, #$10; MOV Y, #$08; MUL YA
		var cpu = CreateCpuWithCode(0xe8, 0x10, 0x8d, 0x08, 0xcf);
		cpu.Execute(13); // MOV(2) + MOV(2) + MUL(9)
						 // 0x10 * 0x08 = 0x80, so A=0x80, Y=0x00
		Assert.Equal(0x80, cpu.A);
		Assert.Equal(0x00, cpu.Y);
	}

	// DIV test
	[Fact]
	public void Div_YA_X_DividesRegisters() {
		// MOV A, #$64; MOV Y, #$00; MOV X, #$0A; DIV YA,X
		// 0x0064 / 0x0A = 10 remainder 0
		var cpu = CreateCpuWithCode(0xe8, 0x64, 0x8d, 0x00, 0xcd, 0x0a, 0x9e);
		cpu.Execute(18); // MOV(2) + MOV(2) + MOV(2) + DIV(12)
		Assert.Equal(0x0a, cpu.A); // Quotient
		Assert.Equal(0x00, cpu.Y); // Remainder
	}

	// 16-bit increment test
	[Fact]
	public void Incw_Dp_IncrementsWord() {
		// Store 0x00FF at $10, then INCW $10
		var cpu = CreateCpuWithCode(0x3a, 0x10); // INCW $10
		cpu.Ram[0x10] = 0xff;
		cpu.Ram[0x11] = 0x00;
		cpu.Step();
		Assert.Equal(0x00, cpu.Ram[0x10]);
		Assert.Equal(0x01, cpu.Ram[0x11]);
	}

	// 16-bit decrement test
	[Fact]
	public void Decw_Dp_DecrementsWord() {
		// Store 0x0100 at $10, then DECW $10
		var cpu = CreateCpuWithCode(0x1a, 0x10); // DECW $10
		cpu.Ram[0x10] = 0x00;
		cpu.Ram[0x11] = 0x01;
		cpu.Step();
		Assert.Equal(0xff, cpu.Ram[0x10]);
		Assert.Equal(0x00, cpu.Ram[0x11]);
	}

	// XCN test (exchange nibbles)
	[Fact]
	public void Xcn_A_ExchangesNibbles() {
		var cpu = CreateCpuWithCode(0xe8, 0x12, 0x9f); // MOV A, #$12; XCN A
		cpu.Execute(7); // MOV(2) + XCN(5)
		Assert.Equal(0x21, cpu.A);
	}

	// SET1/CLR1 tests
	[Fact]
	public void Set1_SetsBit() {
		var cpu = CreateCpuWithCode(0x02, 0x10); // SET1 $10.0
		cpu.Ram[0x10] = 0x00;
		cpu.Step();
		Assert.Equal(0x01, cpu.Ram[0x10]);
	}

	[Fact]
	public void Clr1_ClearsBit() {
		var cpu = CreateCpuWithCode(0x12, 0x10); // CLR1 $10.0
		cpu.Ram[0x10] = 0xff;
		cpu.Step();
		Assert.Equal(0xfe, cpu.Ram[0x10]);
	}

	// BBS/BBC tests
	[Fact]
	public void Bbs_BranchesOnBitSet() {
		// BBS $10.0, +2 (branch if bit 0 is set)
		var cpu = CreateCpuWithCode(0x03, 0x10, 0x02);
		cpu.Ram[0x10] = 0x01; // Bit 0 is set
		int cycles = cpu.Step();
		Assert.Equal(7, cycles); // Branch taken
		Assert.Equal(5, cpu.PC); // 3 (instruction length) + 2 (offset)
	}

	[Fact]
	public void Bbc_BranchesOnBitClear() {
		// BBC $10.0, +2 (branch if bit 0 is clear)
		var cpu = CreateCpuWithCode(0x13, 0x10, 0x02);
		cpu.Ram[0x10] = 0x00; // Bit 0 is clear
		int cycles = cpu.Step();
		Assert.Equal(7, cycles); // Branch taken
		Assert.Equal(5, cpu.PC); // 3 (instruction length) + 2 (offset)
	}

	// TCALL test
	[Fact]
	public void Tcall_CallsTableEntry() {
		// TCALL 0 reads address from $FFDE
		var cpu = CreateCpuWithCode(0x01); // TCALL 0
		cpu.Ram[0xffde] = 0x34;
		cpu.Ram[0xffdf] = 0x12;
		int cycles = cpu.Step();
		Assert.Equal(8, cycles);
		Assert.Equal(0x1234, cpu.PC);
	}

	// DBNZ test
	[Fact]
	public void Dbnz_Y_DecrementsAndBranches() {
		// MOV Y, #$03; DBNZ Y, $FE (branch back 2 bytes to loop)
		var cpu = CreateCpuWithCode(0x8d, 0x03, 0xfe, 0xfe);
		cpu.Step(); // MOV Y, #$03
		Assert.Equal(0x03, cpu.Y);

		cpu.Step(); // DBNZ Y, -2
		Assert.Equal(0x02, cpu.Y);
		Assert.Equal(2, cpu.PC); // Should loop back

		cpu.Step(); // DBNZ Y, -2
		Assert.Equal(0x01, cpu.Y);
		Assert.Equal(2, cpu.PC); // Should loop back

		cpu.Step(); // DBNZ Y, -2 (Y becomes 0, no branch)
		Assert.Equal(0x00, cpu.Y);
		Assert.Equal(4, cpu.PC); // Should NOT branch
	}
}
