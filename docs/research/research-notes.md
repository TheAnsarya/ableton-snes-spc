# Ableton SNES SPC Plugin - Research Notes

**Last Updated**: January 25, 2026  
**Status**: Research complete, implementation done

## Ableton Live Plugin Integration

### Approach Options

#### Option 1: Native VST3 Plugin (Chosen ✅)

- **Pros**: Universal DAW compatibility, full control, real-time audio
- **Cons**: Complex C++/CLI bridge, VST3 SDK learning curve
- **Implementation**: C++ VST3 wrapper + .NET Core library
- **Status**: ✅ IMPLEMENTED - Full VSTGUI 4.x integration with custom views

#### Option 2: Max for Live Device

- **Pros**: Deep Ableton integration, visual programming
- **Cons**: Ableton-only, limited for complex audio processing
- **Not chosen**: Need cross-DAW compatibility

#### Option 3: JUCE Framework

- **Pros**: Cross-platform, well-documented
- **Cons**: C++ only, no .NET integration
- **Not chosen**: Want to reuse GameInfo C# libraries

### VST3 SDK Key Concepts

```text
VST3 Plugin Structure:
├── Component (IComponent)
│   ├── AudioProcessor (IAudioProcessor)
│   │   ├── process() - Real-time audio
│   │   ├── setActive() - Start/stop
│   │   └── setState()/getState() - Serialization
│   └── EditController (IEditController)
│       ├── createView() - UI window
│       ├── setParamNormalized() - Parameter changes
│       └── getParamValueByString() - Parameter display
└── Factory (IPluginFactory)
    └── createInstance() - Create plugin
```

### C++/CLI Bridge Pattern

```cpp
// Native VST3 interface
class SpcProcessor : public AudioEffect {
    gcroot<ManagedEngine^> engine; // .NET reference
    
    tresult process(ProcessData& data) override {
        // Pin managed array, pass to native
        pin_ptr<float> left = &managedLeft[0];
        engine->Process(left, data.numSamples);
    }
};
```

### Real-time Audio Constraints

**Forbidden in audio thread:**

- Memory allocation (GC pressure)
- Locks/mutexes (priority inversion)
- System calls (unpredictable latency)
- Exceptions

**Allowed:**

- Lock-free queues
- Pre-allocated buffers
- Atomic operations

## SNES Audio Hardware

### SPC700 CPU

| Register | Size   | Description         |
| -------- | ------ | ------------------- |
| A        | 8-bit  | Accumulator         |
| X        | 8-bit  | Index register      |
| Y        | 8-bit  | Index register      |
| SP       | 8-bit  | Stack pointer       |
| PSW      | 8-bit  | Program status word |
| PC       | 16-bit | Program counter     |

**Clock**: 1.024 MHz
**RAM**: 64KB

### S-DSP Registers

| Address | Name   | Description   |
| ------- | ------ | ------------- |
| $x0     | VOL(L) | Left volume   |
| $x1     | VOL(R) | Right volume  |
| $x2-3   | PITCH  | Sample pitch  |
| $x4     | SRCN   | Source number |
| $x5-6   | ADSR   | Envelope      |
| $x7     | GAIN   | Gain mode     |

### BRR Format

```text
Block (9 bytes = 16 samples):
┌─────────────────────────────────────┐
│ Header (1 byte)                     │
│   Bits 7-4: Range (shift)           │
│   Bits 3-2: Filter (0-3)            │
│   Bit 1: Loop flag                  │
│   Bit 0: End flag                   │
├─────────────────────────────────────┤
│ Sample data (8 bytes = 16 nibbles)  │
│   Each nibble is 4-bit signed       │
└─────────────────────────────────────┘

Filter coefficients:
  0: sample = delta
  1: sample = delta + old × 15/16
  2: sample = delta + old × 61/32 - older × 15/16
  3: sample = delta + old × 115/64 - older × 13/16
```

### Echo Effect

```text
Echo parameters:
  EDL: Delay (0-15 × 16ms = 0-240ms)
  EFB: Feedback (-128 to 127)
  FIR: 8-tap filter coefficients
  ESA: Echo buffer start address

Buffer size = EDL × 2048 bytes
```

## Known Sound Drivers

### Implemented Detection ✅

The SpcAnalyzer class detects these drivers automatically on SPC import:

| Driver   | Publisher  | Example Games                      | Status |
|----------|------------|---------------------------------------|--------|
| NSPC     | Nintendo   | Most Nintendo first-party games       | ✅     |
| Akao     | Square     | Final Fantasy, Chrono Trigger         | ✅     |
| HAL Lab  | HAL        | Kirby, Smash Bros                     | ✅     |
| Capcom   | Capcom     | Mega Man X, Street Fighter            | ✅     |
| Konami   | Konami     | Castlevania, Contra III               | ✅     |
| Rare     | Rare       | Donkey Kong Country series            | ✅     |
| Enix     | Enix       | ActRaiser, Soul Blazer                | ✅     |
| Hudson   | Hudson     | Bomberman, Adventure Island           | ✅     |
| Namco    | Namco      | Tales series                          | ✅     |
| Taito    | Taito      | Space Invaders, Bust-a-Move           | ✅     |

### N-SPC (Nintendo Standard)

- Used by: Most Nintendo first-party games
- Features: 8 channels, echo, pitch modulation
- Well documented, primary target
- **Status**: ✅ Detection implemented

### Akao (Square)

- Used by: Final Fantasy, Chrono Trigger
- Features: Complex, subroutines, loops
- More complex to parse
- **Status**: ✅ Detection implemented

## Existing Libraries to Leverage

### Implemented in SpcPlugin.Core ✅

| Component      | Status | Description                          |
|----------------|--------|--------------------------------------|
| SpcFile        | ✅     | Full SPC import/export with ID666    |
| SpcxFile       | ✅     | ZIP/JSON project format              |
| BrrCodec       | ✅     | BRR encode/decode                    |
| SpcAnalyzer    | ✅     | Driver detection (10+ drivers)       |
| Spc700         | ✅     | CPU emulator (256 instructions)      |
| SDsp           | ✅     | Audio processor (8 voices, echo)     |
| SpcEngine      | ✅     | Main orchestration engine            |
| MidiProcessor  | ✅     | MIDI input handling                  |

### External

- `VST.NET` - Partial VST2 support (not VST3)
- `NAudio` - Audio utilities (not used)

## Technical Decisions

### Decision: Use .NET 10 ✅

**Rationale**: Latest features, good performance, spans for audio buffers
**Status**: Implemented and working

### Decision: Start Windows-only ✅

**Rationale**: Simplest VST3 path, expand later
**Status**: Windows x64 fully working

### Decision: Focus on N-SPC ✅

**Rationale**: Most common, well-documented, expand to others later
**Status**: 10+ drivers now detected

### Decision: SPCX as primary format ✅

**Rationale**: SPC too limited for editing state, need extended format
**Status**: Implemented with ZIP/JSON structure

### Decision: VSTGUI for UI ✅

**Rationale**: Native VST3 integration, cross-platform, well-supported
**Status**: Full custom views implemented (WaveformView, SpectrumView, PresetBrowser, etc.)

## Resolved Questions

1. **Real-time BRR decoding performance?**
   - ✅ RESOLVED: Pre-decode to cache works well

2. **VST3 state chunk size limit?**
   - ✅ RESOLVED: Embedded SPCX works, external file not needed

3. **SPC700 timing accuracy?**
   - ✅ RESOLVED: Cycle-accurate emulation implemented

4. **MIDI latency compensation?**
   - ✅ RESOLVED: DAW handles latency, plugin provides instant response

## References

- [VST3 SDK Documentation](https://steinbergmedia.github.io/vst3_dev_portal/)
- [SPC700 Reference](https://wiki.superfamicom.org/spc700-reference)
- [Anomie's S-DSP Doc](https://www.romhacking.net/documents/197/)
- [VST.NET GitHub](https://github.com/obiwanjacobi/vst.net)
- [BRR Format Guide](https://wiki.superfamicom.org/bit-rate-reduction-(brr))
