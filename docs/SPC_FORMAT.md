# SPC File Format Specification

This document provides a comprehensive specification of the SPC file format used for storing SNES audio state snapshots.

## Overview

The SPC file format is a memory dump of the SNES audio hardware state, allowing playback of SNES music outside of the original console. It captures the complete state of:

- **SPC700 CPU** - The audio coprocessor
- **S-DSP** - The digital signal processor
- **Audio RAM (ARAM)** - 64KB of dedicated audio memory
- **IPL ROM** - 64 bytes of bootstrap code
- **Extra RAM** - Additional 64 bytes

## File Structure

| Offset | Size | Description |
|--------|------|-------------|
| `$00` | 33 | Header string |
| `$21` | 2 | Reserved (should be `$1a1a`) |
| `$23` | 1 | ID666 tag presence (`$1a` = has ID666) |
| `$24` | 1 | Version minor |
| `$25` | 2 | PC (Program Counter) |
| `$27` | 1 | A register |
| `$28` | 1 | X register |
| `$29` | 1 | Y register |
| `$2a` | 1 | PSW (Program Status Word) |
| `$2b` | 1 | SP (Stack Pointer) |
| `$2c` | 2 | Reserved |
| `$2e` | 178 | ID666 tag (if present) |
| `$100` | 65536 | Audio RAM (64KB) |
| `$10100` | 128 | DSP registers |
| `$10180` | 64 | Extra RAM (unused by most SPCs) |
| `$101c0` | 64 | IPL ROM (bootstrap code) |

**Total file size:** 66,048 bytes (without extended info) or 67,104+ bytes (with extended info)

## Header

The file begins with the following ASCII string (33 bytes):

```
SNES-SPC700 Sound File Data v0.30
```

Note: The version may vary (`v0.10`, `v0.20`, `v0.30`, etc.)

## SPC700 CPU Registers

Located at offsets `$25-$2b`:

| Register | Offset | Size | Description |
|----------|--------|------|-------------|
| PC | `$25` | 2 | Program Counter (little-endian) |
| A | `$27` | 1 | Accumulator |
| X | `$28` | 1 | X Index Register |
| Y | `$29` | 1 | Y Index Register |
| PSW | `$2a` | 1 | Program Status Word |
| SP | `$2b` | 1 | Stack Pointer |

### PSW (Program Status Word) Flags

| Bit | Flag | Description |
|-----|------|-------------|
| 7 | N | Negative |
| 6 | V | Overflow |
| 5 | P | Direct Page |
| 4 | B | Break |
| 3 | H | Half-carry |
| 2 | I | Interrupt enable |
| 1 | Z | Zero |
| 0 | C | Carry |

## ID666 Tag

The ID666 tag contains metadata about the song. There are two formats: **Text** (original) and **Binary** (newer).

### Text Format (Offset `$2e`, 178 bytes)

| Offset | Size | Description |
|--------|------|-------------|
| `$2e` | 32 | Song title |
| `$4e` | 32 | Game title |
| `$6e` | 16 | Name of dumper |
| `$7e` | 32 | Comments |
| `$9e` | 11 | Date (DD/MM/YYYY or MM/DD/YYYY) |
| `$a9` | 3 | Play length (seconds, ASCII) |
| `$ac` | 5 | Fade length (milliseconds, ASCII) |
| `$b1` | 32 | Artist name |
| `$d1` | 1 | Default channel disables |
| `$d2` | 1 | Emulator used (0=unknown, 1=ZSNES, 2=Snes9x) |
| `$d3` | 45 | Reserved |

### Binary Format (Offset `$2e`, 178 bytes)

| Offset | Size | Description |
|--------|------|-------------|
| `$2e` | 32 | Song title (null-terminated) |
| `$4e` | 32 | Game title (null-terminated) |
| `$6e` | 16 | Name of dumper (null-terminated) |
| `$7e` | 32 | Comments (null-terminated) |
| `$9e` | 4 | Date (32-bit, YYYYMMDD) |
| `$a2` | 7 | Unused |
| `$a9` | 3 | Play length (24-bit, seconds) |
| `$ac` | 4 | Fade length (32-bit, milliseconds) |
| `$b0` | 32 | Artist name (null-terminated) |
| `$d0` | 1 | Default channel disables |
| `$d1` | 1 | Emulator used |
| `$d2` | 46 | Reserved |

## Audio RAM (ARAM)

The 64KB Audio RAM (`$100-$100FF`) contains:

- Program code for the SPC700
- Sample data (BRR encoded)
- Sample directory
- Music data (patterns, sequences)
- Echo buffer

### Sample Directory

The sample directory location is specified by DSP register `$5D` (DIR). Each entry is 4 bytes:

| Offset | Size | Description |
|--------|------|-------------|
| 0 | 2 | Sample start address (little-endian) |
| 2 | 2 | Loop point address (little-endian) |

Maximum of 256 directory entries (1KB total).

### BRR Sample Format

BRR (Bit Rate Reduction) is the compression format used for samples. Each BRR block:

- **9 bytes** per block
- **16 samples** decoded per block
- **~4:1 compression** ratio

#### BRR Block Header (First Byte)

| Bits | Description |
|------|-------------|
| 7-4 | Range (shift amount, 0-12) |
| 3-2 | Filter (0-3) |
| 1 | Loop flag |
| 0 | End flag |

#### BRR Filters

| Filter | Formula |
|--------|---------|
| 0 | `sample = input` |
| 1 | `sample = input + old * 15/16` |
| 2 | `sample = input + old * 61/32 - older * 15/16` |
| 3 | `sample = input + old * 115/64 - older * 13/16` |

#### BRR Sample Data (Bytes 1-8)

Each byte contains two 4-bit signed nibbles:
- High nibble: Even sample
- Low nibble: Odd sample

Decoding:
```
raw = nibble (sign-extended to 16 bits)
sample = (raw << range) >> 1
sample += filter_contribution
sample = clamp(sample, -32768, 32767)
```

## DSP Registers

Located at offset `$10100` (128 bytes). The DSP has 128 registers organized as:

### Global Registers

| Address | Name | Description |
|---------|------|-------------|
| `$0c` | MVOL_L | Main volume left (-128 to 127) |
| `$1c` | MVOL_R | Main volume right (-128 to 127) |
| `$2c` | EVOL_L | Echo volume left (-128 to 127) |
| `$3c` | EVOL_R | Echo volume right (-128 to 127) |
| `$4c` | KON | Key on (write triggers voices) |
| `$5c` | KOFF | Key off (write releases voices) |
| `$6c` | FLG | Flags (reset, mute, echo, noise clock) |
| `$7c` | ENDX | End flags (read-only, which voices ended) |
| `$0d` | EFB | Echo feedback (-128 to 127) |
| `$2d` | PMON | Pitch modulation enable |
| `$3d` | NON | Noise enable |
| `$4d` | EON | Echo enable |
| `$5d` | DIR | Sample directory page (×$100) |
| `$6d` | ESA | Echo buffer start page (×$100) |
| `$7d` | EDL | Echo delay (×16ms, 0-15) |
| `$xf` | FIR | Echo FIR filter coefficients (8 bytes) |

### Per-Voice Registers (×8 voices)

| Offset | Name | Description |
|--------|------|-------------|
| `$x0` | VOL_L | Volume left (-128 to 127) |
| `$x1` | VOL_R | Volume right (-128 to 127) |
| `$x2` | P_LO | Pitch low byte |
| `$x3` | P_HI | Pitch high byte (14-bit total) |
| `$x4` | SRCN | Source number (sample index) |
| `$x5` | ADSR1 | ADSR settings 1 |
| `$x6` | ADSR2 | ADSR settings 2 |
| `$x7` | GAIN | Gain settings |
| `$x8` | ENVX | Current envelope value (read-only) |
| `$x9` | OUTX | Current sample output (read-only) |

Where `x` is the voice number (0-7).

### ADSR Format

**ADSR1 (`$x5`)**:
| Bits | Description |
|------|-------------|
| 7 | ADSR enable (0=GAIN mode, 1=ADSR mode) |
| 6-4 | Decay rate (0-7) |
| 3-0 | Attack rate (0-15) |

**ADSR2 (`$x6`)**:
| Bits | Description |
|------|-------------|
| 7-5 | Sustain level (0-7) |
| 4-0 | Sustain rate (0-31) |

### GAIN Format (`$x7`)

When ADSR is disabled, GAIN controls the envelope:

| Mode | Bits 7-5 | Description |
|------|----------|-------------|
| Direct | `0xx` | Direct value (bits 6-0) |
| Linear Dec | `100` | Linear decrease, rate = bits 4-0 |
| Exp Dec | `101` | Exponential decrease, rate = bits 4-0 |
| Linear Inc | `110` | Linear increase, rate = bits 4-0 |
| Bent Inc | `111` | Bent line increase, rate = bits 4-0 |

### FLG Register (`$6c`)

| Bit | Description |
|-----|-------------|
| 7 | Soft reset |
| 6 | Mute |
| 5 | Echo disable |
| 4-0 | Noise frequency (0-31) |

### Pitch Register Format

14-bit pitch value across P_LO and P_HI:
- `P_LO`: Bits 0-7
- `P_HI`: Bits 0-5 (upper bits are ignored)

The pitch value determines playback speed:
- `$1000` (4096) = 32kHz (normal speed)
- Higher = faster/higher pitch
- Lower = slower/lower pitch

Formula: `frequency = 32000 × pitch / 4096`

## Echo Buffer

The echo buffer location is at `ESA × $100` in ARAM. Size is `EDL × 2KB` (up to 30KB).

The echo uses an 8-tap FIR filter with coefficients at:
- `$0f`, `$1f`, `$2f`, `$3f`, `$4f`, `$5f`, `$6f`, `$7f`

Each coefficient is signed (-128 to 127).

## Extended Information

Some SPC files include extended information after the main data. This is indicated by the string "xid6" at offset `$10200`.

### Extended ID666 Format

| Offset | Size | Description |
|--------|------|-------------|
| `$10200` | 4 | "xid6" signature |
| `$10204` | 4 | Extended data size |
| `$10208` | var | Extended data chunks |

Each chunk has:
| Offset | Size | Description |
|--------|------|-------------|
| 0 | 1 | ID |
| 1 | 1 | Type |
| 2 | 2 | Length (if type ≥ 4) |
| 4 | var | Data |

## Reading an SPC File (Code Example)

```csharp
public class SpcFile {
    public string Header { get; }
    public byte VersionMinor { get; }
    public ushort PC { get; }
    public byte A { get; }
    public byte X { get; }
    public byte Y { get; }
    public byte PSW { get; }
    public byte SP { get; }
    public Id666Tag? Tag { get; }
    public byte[] Ram { get; }    // 64KB
    public byte[] DspRegs { get; } // 128 bytes
    public byte[] ExtraRam { get; } // 64 bytes
    public byte[] IplRom { get; } // 64 bytes

    public static SpcFile Load(string path) {
        var data = File.ReadAllBytes(path);
        
        // Verify header
        var header = Encoding.ASCII.GetString(data, 0, 33);
        if (!header.StartsWith("SNES-SPC700"))
            throw new InvalidDataException("Not an SPC file");
        
        // Read CPU registers
        ushort pc = (ushort)(data[0x25] | (data[0x26] << 8));
        byte a = data[0x27];
        byte x = data[0x28];
        byte y = data[0x29];
        byte psw = data[0x2a];
        byte sp = data[0x2b];
        
        // Read RAM
        var ram = new byte[65536];
        Array.Copy(data, 0x100, ram, 0, 65536);
        
        // Read DSP registers
        var dsp = new byte[128];
        Array.Copy(data, 0x10100, dsp, 0, 128);
        
        // Read extra RAM
        var extra = new byte[64];
        Array.Copy(data, 0x10180, extra, 0, 64);
        
        // Read IPL ROM
        var ipl = new byte[64];
        Array.Copy(data, 0x101c0, ipl, 0, 64);
        
        return new SpcFile(...);
    }
}
```

## Writing an SPC File (Code Example)

```csharp
public void Save(string path) {
    using var stream = File.Create(path);
    using var writer = new BinaryWriter(stream);
    
    // Write header
    writer.Write(Encoding.ASCII.GetBytes("SNES-SPC700 Sound File Data v0.30"));
    writer.Write((byte)0x1a);
    writer.Write((byte)0x1a);
    writer.Write((byte)0x1a); // Has ID666
    writer.Write((byte)30);   // Version
    
    // Write CPU state
    writer.Write(PC);
    writer.Write(A);
    writer.Write(X);
    writer.Write(Y);
    writer.Write(PSW);
    writer.Write(SP);
    writer.Write((short)0); // Reserved
    
    // Write ID666 tag (178 bytes)
    WriteId666Tag(writer);
    
    // Write RAM (64KB)
    writer.Write(Ram);
    
    // Write DSP registers (128 bytes)
    writer.Write(DspRegs);
    
    // Write extra RAM (64 bytes)
    writer.Write(ExtraRam);
    
    // Write IPL ROM (64 bytes)
    writer.Write(IplRom);
}
```

## References

- [SPC File Format (Wikipedia)](https://en.wikipedia.org/wiki/SPC_file)
- [SPC700 Reference](https://wiki.superfamicom.org/spc700-reference)
- [Anomie's S-DSP Doc](https://problemkaputt.de/fullsnes.htm#snesapudspbrrsamples)
- [SNES Dev Manual](https://wiki.superfamicom.org/snes-dev-manual)

## Tools

- **SPC2ROM** - Converts SPC back to ROM format
- **SPCTool** - Extracts samples from SPC files
- **spc_decoder** - Reference decoder implementation
- **This Plugin** - Full editing and playback support
