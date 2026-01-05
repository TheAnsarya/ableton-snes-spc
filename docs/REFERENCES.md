# Technical References

This document lists the technical references and documentation used to implement the SNES SPC emulator and Ableton VST3 plugin.

## SPC700 CPU

The SPC700 is an 8-bit CPU manufactured by Sony, used in the SNES Audio Processing Unit (APU).

### Official Documentation
- **SPC700 Datasheet** - Sony's original hardware documentation (limited availability)

### Community Documentation
- **[Anomie's SPC700 Doc](https://wiki.superfamicom.org/spc700-reference)** - Comprehensive opcode reference
- **[Super Famicom Development Wiki - SPC700](https://wiki.superfamicom.org/spc700)** - CPU architecture overview
- **[Fullsnes by nocash](https://problemkaputt.de/fullsnes.htm#saborregisters)** - Detailed register documentation

### Key Technical Details
- 8-bit data bus, 16-bit address space (64KB)
- Runs at ~1.024 MHz (24.576 MHz / 24)
- 256 opcodes with multiple addressing modes
- 8-bit A, X, Y registers; 8-bit stack pointer; 16-bit PC
- Direct Page addressing (similar to 6502 zero page)
- Bit manipulation instructions (SET1, CLR1, BBS, BBC)
- 16-bit operations (ADDW, SUBW, MOVW, etc.)

## S-DSP (Digital Signal Processor)

The S-DSP generates audio output from BRR-encoded samples.

### Documentation
- **[Anomie's S-DSP Doc](https://wiki.superfamicom.org/spc700-reference)** - DSP register reference
- **[Fullsnes S-DSP](https://problemkaputt.de/fullsnes.htm#sabordigitalsignalprocessordsp)** - Complete DSP documentation
- **[Super Famicom Development Wiki - DSP](https://wiki.superfamicom.org/dsp)** - Overview and registers

### Key Technical Details
- 8 independent voices
- BRR (Bit Rate Reduction) sample compression (4:1 ratio)
- ADSR and GAIN envelope modes
- Gaussian interpolation for sample playback
- FIR-filtered echo/reverb effect
- Pitch modulation between voices
- Noise generator (LFSR)
- Sample rates up to 32 kHz per voice

## BRR Audio Compression

BRR is the proprietary audio compression format used by the SNES.

### Documentation
- **[BRR Format Specification](https://wiki.superfamicom.org/bit-rate-reduction-(brr))** - Sample format details
- **[Fullsnes BRR](https://problemkaputt.de/fullsnes.htm#snaborbrrsampleformat)** - Encoding details

### Format Details
- 9 bytes per block (1 header + 8 data bytes)
- 16 samples per block (4 bits per sample)
- 4 filter modes for prediction
- Loop and end flags in header
- ~32 kHz maximum sample rate

## Gaussian Interpolation

The SNES DSP uses a specific Gaussian interpolation table for sample playback.

### References
- **[Gaussian Table Values](https://wiki.superfamicom.org/spc700-reference)** - 512-entry interpolation table
- **bsnes/higan source code** - Reference implementation by Near

### Implementation
The table provides 4 coefficients for each of 256 fractional positions, used to interpolate between 4 sample points for smooth pitch shifting.

## SPC File Format

The .spc file format stores SNES music for playback outside the console.

### Documentation
- **[SPC File Format v0.30](http://www.snesmusic.org/files/spc_file_format.txt)** - Official specification

### Structure
| Offset | Size | Description |
|--------|------|-------------|
| 0x00 | 33 | File header "SNES-SPC700 Sound File Data v0.30" |
| 0x21 | 2 | 0x1A, 0x1A markers |
| 0x23 | 1 | ID666 tag present (0x1A = present) |
| 0x24 | 1 | Minor version |
| 0x25 | 2 | PC (little-endian) |
| 0x27 | 1 | A register |
| 0x28 | 1 | X register |
| 0x29 | 1 | Y register |
| 0x2A | 1 | PSW register |
| 0x2B | 1 | SP register |
| 0x2E | 32 | Song title |
| ... | ... | Additional ID666 tags |
| 0x100 | 65536 | SPC RAM (64KB) |
| 0x10100 | 128 | DSP registers |
| 0x10180 | 64 | Extra RAM (IPL ROM region) |

## VST3 SDK

The VST3 SDK provides the plugin interface for DAW integration.

### Official Documentation
- **[Steinberg VST3 SDK](https://steinbergmedia.github.io/vst3_doc/)** - Official documentation
- **[VST3 Developer Portal](https://www.steinberg.net/developers/)** - Downloads and resources
- **[VST3 GitHub Repository](https://github.com/steinbergmedia/vst3sdk)** - Source code

### Key Concepts
- **Processor**: Audio processing (runs in real-time thread)
- **Controller**: UI and parameter management
- **Parameters**: Automatable values exposed to DAW
- **Plugin Factory**: Creates processor/controller instances

### VST3 for Instruments
- `kVstAudioEffectClass` - Audio effect plugin
- `kInstrumentSynth` - Synthesizer/instrument plugin
- Stereo output (SpeakerArr::kStereo)
- 32-bit float audio processing

## .NET Native Hosting

For integrating .NET code with native VST3 wrapper.

### Documentation
- **[.NET Native Hosting](https://docs.microsoft.com/en-us/dotnet/core/tutorials/netcore-hosting)** - Microsoft docs
- **[hostfxr API](https://github.com/dotnet/runtime/blob/main/docs/design/features/native-hosting.md)** - Low-level hosting

### Key APIs
- `hostfxr_initialize_for_runtime_config` - Initialize runtime
- `hostfxr_get_runtime_delegate` - Get function pointers
- `get_function_pointer` - Call managed code from native

### UnmanagedCallersOnly
C# methods with `[UnmanagedCallersOnly]` attribute can be called directly from native code without marshaling overhead.

## Reference Emulators

These open-source emulators provided implementation guidance:

### bsnes/higan (by Near)
- Most accurate SNES emulator
- Reference for DSP and SPC700 timing
- [GitHub](https://github.com/bsnes-emu/bsnes)

### snes9x
- Popular SNES emulator
- Good balance of accuracy and performance
- [GitHub](https://github.com/snes9xgit/snes9x)

### SPC_Player
- Standalone SPC player
- Reference for audio output
- [GitHub](https://github.com/trgwii/SPC_Player)

## Ableton Live Integration

### Ableton-Specific Considerations
- **Buffer sizes**: 64-2048 samples typical
- **Sample rates**: 44.1kHz, 48kHz, 96kHz common
- **Latency**: Real-time audio requires < 10ms
- **Parameter automation**: All exposed parameters automatable
- **Preset management**: VST3 state save/restore

### Testing Resources
- **Ableton Live** - Primary target DAW
- **REAPER** - Good for VST3 debugging
- **Steinberg Cubase** - VST3 reference host

## Additional Resources

### SNES Music Archives
- **[SNESmusic.org](http://www.snesmusic.org/)** - SPC file archive
- **[Zophar's Domain](https://www.zophar.net/music/spc)** - Additional SPC files
- **[VGMRips](https://vgmrips.net/)** - Video game music archive

### Tools
- **[SPC700 Player](http://www.snesmusic.org/hoot/)** - Reference player
- **[AddMusicK](https://www.smwcentral.net/)** - SNES music composition tool
- **[C700](https://github.com/osoumen/C700)** - VST sampler for SNES sounds

### Community
- **[Romhacking.net](https://www.romhacking.net/)** - ROM hacking community
- **[SMW Central](https://www.smwcentral.net/)** - Super Mario World hacking (SPC expertise)
- **[SNES Dev Discord](https://discord.gg/snesdev)** - Developer community
