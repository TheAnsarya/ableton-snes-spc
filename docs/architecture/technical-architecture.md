# Technical Architecture

## Overview

The Ableton SNES SPC Plugin is a VST3 audio plugin that provides hardware-accurate SNES audio playback and editing. This document describes the technical architecture and key design decisions.

**Current Version**: 0.4.0  
**Status**: Feature Complete

## System Architecture

```text
┌─────────────────────────────────────────────────────────────────────────┐
│                           DAW (Ableton Live)                            │
│                                                                         │
│  ┌───────────────────────────────────────────────────────────────────┐ │
│  │                      VST3 Host Interface                          │ │
│  │  • Audio processing callbacks                                     │ │
│  │  • Parameter changes                                              │ │
│  │  • MIDI events                                                    │ │
│  │  • Editor window management                                       │ │
│  └───────────────────────────────────────────────────────────────────┘ │
│                                   │                                     │
└───────────────────────────────────┼─────────────────────────────────────┘
                                    │
┌───────────────────────────────────┼─────────────────────────────────────┐
│                                   ▼                                     │
│  ┌───────────────────────────────────────────────────────────────────┐ │
│  │                    VST3 Wrapper Layer (C++/CLI)                   │ │
│  │                                                                   │ │
│  │  • IComponent, IAudioProcessor implementation                     │ │
│  │  • Parameter marshalling                                          │ │
│  │  • Thread-safe managed/native boundary                            │ │
│  └───────────────────────────────────────────────────────────────────┘ │
│                                   │                                     │
│                    ┌──────────────┼──────────────┐                     │
│                    ▼              ▼              ▼                     │
│  ┌─────────────────────┐  ┌─────────────┐  ┌─────────────────────┐    │
│  │   Audio Thread      │  │  UI Thread  │  │   Worker Thread     │    │
│  │                     │  │             │  │                     │    │
│  │  • Process()        │  │  • WPF/MAUI │  │  • File I/O         │    │
│  │  • Real-time DSP    │  │  • User     │  │  • BRR encoding     │    │
│  │  • Lock-free comms  │  │    input    │  │  • SPC export       │    │
│  └──────────┬──────────┘  └──────┬──────┘  └──────────┬──────────┘    │
│             │                    │                    │                │
│             └────────────────────┼────────────────────┘                │
│                                  ▼                                     │
│  ┌───────────────────────────────────────────────────────────────────┐ │
│  │                      Core Engine (.NET 10)                        │ │
│  │                                                                   │ │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐   │ │
│  │  │  SPC700     │  │   S-DSP     │  │    Project Manager      │   │ │
│  │  │  Emulator   │  │  Emulator   │  │                         │   │ │
│  │  │             │  │             │  │  • SPCX read/write      │   │ │
│  │  │  • CPU ops  │  │  • Voice    │  │  • SPC import/export    │   │ │
│  │  │  • RAM      │  │    mixing   │  │  • Sample management    │   │ │
│  │  │  • Timers   │  │  • Echo     │  │  • Sequence editing     │   │ │
│  │  │  • I/O      │  │  • BRR      │  │                         │   │ │
│  │  └─────────────┘  └─────────────┘  └─────────────────────────┘   │ │
│  │                                                                   │ │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐   │ │
│  │  │    BRR      │  │  Sequence   │  │    Memory Analyzer      │   │ │
│  │  │   Codec     │  │  Compiler   │  │                         │   │ │
│  │  │             │  │             │  │  • Driver detection     │   │ │
│  │  │  • Decode   │  │  • N-SPC    │  │  • Sample extraction    │   │ │
│  │  │  • Encode   │  │  • MIDI     │  │  • Sequence parsing     │   │ │
│  │  │  • Quality  │  │  • Events   │  │                         │   │ │
│  │  └─────────────┘  └─────────────┘  └─────────────────────────┘   │ │
│  │                                                                   │ │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐   │ │
│  │  │  SpcFile    │  │  SpcxFile   │  │    SpcAnalyzer          │   │ │
│  │  │  (Import/   │  │  (Project   │  │                         │   │ │
│  │  │   Export)   │  │   Format)   │  │  • 10+ sound drivers    │   │ │
│  │  │  • ID666    │  │  • ZIP/JSON │  │  • NSPC, Akao, HAL Lab  │   │ │
│  │  │  • Text/Bin │  │  • Settings │  │  • Capcom, Konami, etc  │   │ │
│  │  │  • CPU state│  │  • Analysis │  │                         │   │ │
│  │  └─────────────┘  └─────────────┘  └─────────────────────────┘   │ │
│  └───────────────────────────────────────────────────────────────────┘ │
│                                                                         │
│                         SNES SPC Plugin                                 │
└─────────────────────────────────────────────────────────────────────────┘
```

## GUI Architecture (VSTGUI 4.x)

The plugin uses VSTGUI 4.x with custom view components:

```text
┌─────────────────────────────────────────────────────────────────────────┐
│                         SpcEditor (Main Window)                         │
├─────────────────────────────────────────────────────────────────────────┤
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────────┐  │
│  │  ViewSwitcher    │  │  WaveformView    │  │  SpectrumView        │  │
│  │  (Tab Control)   │  │  (Time Domain)   │  │  (Frequency Domain)  │  │
│  │                  │  │  • Zoom/scroll   │  │  • 32-band FFT       │  │
│  │  • Main View     │  │  • BRR blocks    │  │  • Peak hold         │  │
│  │  • Browser View  │  │  • Loop points   │  │  • Decay smoothing   │  │
│  │  • Settings      │  │  • Selection     │  │                      │  │
│  └──────────────────┘  └──────────────────┘  └──────────────────────┘  │
│                                                                         │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────────┐  │
│  │  PresetBrowser   │  │  KeyboardHandler │  │  MidiLearnHandler    │  │
│  │  (File Browser)  │  │  (Shortcuts)     │  │  (CC Mapping)        │  │
│  │                  │  │                  │  │                      │  │
│  │  • Directory     │  │  • Space=Play    │  │  • Learn mode        │  │
│  │    scanning      │  │  • Esc=Stop      │  │  • Multi-channel     │  │
│  │  • Search/filter │  │  • Arrows=Volume │  │  • Preset save/load  │  │
│  │  • Sort by name  │  │  • Customizable  │  │                      │  │
│  │  • Double-click  │  │                  │  │                      │  │
│  └──────────────────┘  └──────────────────┘  └──────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────┘
```

## Component Details

### 1. VST3 Wrapper Layer (C++/CLI)

The wrapper bridges the native VST3 API and managed .NET code.

```cpp
// SpcProcessor.h
class SpcProcessor : public Steinberg::Vst::AudioEffect {
public:
    tresult PLUGIN_API process(ProcessData& data) override;
    tresult PLUGIN_API setState(IBStream* state) override;
    tresult PLUGIN_API getState(IBStream* state) override;

private:
    gcroot<SpcPlugin::Core::Engine^> engine_;
    LockFreeQueue<ParameterChange> parameterQueue_;
};
```

Key responsibilities:

- Implement Steinberg VST3 interfaces
- Marshal data between native and managed code
- Provide lock-free communication for real-time thread

### 2. Core Engine (.NET 10)

The engine provides all audio processing and project management.

```csharp
namespace SpcPlugin.Core;

public sealed class Engine : IDisposable {
    private readonly Spc700 _cpu;
    private readonly SDsp _dsp;
    private readonly ProjectManager _project;

    public void Process(Span<float> leftBuffer, Span<float> rightBuffer) {
        // Real-time audio generation
        _cpu.Execute(samplesNeeded);
        _dsp.Render(leftBuffer, rightBuffer);
    }

    public void LoadProject(string path) {
        _project.Load(path);
        InitializeFromProject();
    }
}
```

### 3. SPC700 Emulator

Cycle-accurate SPC700 CPU emulation.

```csharp
public sealed class Spc700 {
    private byte[] _ram = new byte[65536];
    private byte _a, _x, _y, _sp, _psw;
    private ushort _pc;

    public void Execute(int cycles) {
        while (cycles > 0) {
            byte opcode = _ram[_pc++];
            cycles -= ExecuteOpcode(opcode);
        }
    }

    public byte ReadPort(int port) => _ports[port];
    public void WritePort(int port, byte value) => _ports[port] = value;
}
```

### 4. S-DSP Emulator

Digital signal processor with all SNES audio features.

```csharp
public sealed class SDsp {
    private readonly Voice[] _voices = new Voice[8];
    private readonly int[] _echoBuffer;
    private readonly sbyte[] _firCoefficients = new sbyte[8];

    public void Render(Span<float> left, Span<float> right) {
        for (int i = 0; i < left.Length; i++) {
            int mixL = 0, mixR = 0;

            // Mix all 8 voices
            foreach (var voice in _voices) {
                var (l, r) = voice.GetSample();
                mixL += l;
                mixR += r;
            }

            // Apply echo
            ApplyEcho(ref mixL, ref mixR);

            // Output
            left[i] = mixL / 32768f;
            right[i] = mixR / 32768f;
        }
    }
}
```

### 5. BRR Codec

Encode and decode BRR samples.

```csharp
public static class BrrCodec {
    public static short[] Decode(ReadOnlySpan<byte> brr) {
        // Decode 9-byte blocks to 16 samples each
    }

    public static byte[] Encode(ReadOnlySpan<short> pcm, int loopPoint = -1) {
        // Encode PCM to BRR with optimal filter selection
    }
}
```

### 6. Project Manager

Handles SPCX project files and SPC import/export.

```csharp
public sealed class ProjectManager {
    public SpcxProject CurrentProject { get; private set; }

    public SpcxProject ImportSpc(string path) {
        var spc = SpcFile.Load(path);
        var project = new SpcxProject();

        // Extract samples
        project.Samples = SampleExtractor.Extract(spc);

        // Parse sequences (if driver known)
        project.Sequences = SequenceParser.Parse(spc);

        return project;
    }

    public void ExportSpc(string path) {
        var builder = new SpcBuilder();

        // Compile sequences
        var sequenceData = SequenceCompiler.Compile(CurrentProject.Sequences);

        // Encode samples
        var sampleData = EncodeSamples(CurrentProject.Samples);

        // Build SPC
        builder.Build(sequenceData, sampleData, path);
    }
}
```

### 7. SpcFile (NEW in v0.4.0)

Complete SPC file format handling with ID666 tag support.

```csharp
public sealed class SpcFile {
    // CPU State
    public ushort PC { get; set; }
    public byte A { get; set; }
    public byte X { get; set; }
    public byte Y { get; set; }
    public byte PSW { get; set; }
    public byte SP { get; set; }

    // Memory
    public byte[] Ram { get; } = new byte[65536];
    public byte[] DspRegisters { get; } = new byte[128];
    public byte[] ExtraRam { get; } = new byte[64];
    public byte[] IplRom { get; } = new byte[64];

    // ID666 Metadata (Text or Binary format)
    public string? SongTitle { get; set; }
    public string? GameTitle { get; set; }
    public string? Artist { get; set; }
    public int PlayLength { get; set; }
    public int FadeLength { get; set; }

    // Load with driver detection
    public static SpcFile Load(string path) { ... }
    
    // Export with full ID666 tags
    public void Save(string path) { ... }
    
    // Run analysis
    public SpcAnalysisResult Analyze() { ... }
}
```

### 8. SpcxFile (Enhanced in v0.4.0)

ZIP-based project format with JSON manifests.

```csharp
public sealed class SpcxFile {
    // SPC Data
    public byte[] Ram { get; set; }
    public byte[] DspRegisters { get; set; }
    public ushort PC { get; set; }
    public byte A { get; set; }
    public byte X { get; set; }
    public byte Y { get; set; }
    public byte PSW { get; set; }
    public byte SP { get; set; }

    // Metadata
    public SpcxMetadata Metadata { get; set; }
    
    // Editor Settings (voice mutes, solos, volumes)
    public SpcxEditorSettings Settings { get; set; }
    
    // Cached Analysis
    public SpcxAnalysisResult Analysis { get; set; }

    // Import from SPC with driver detection
    public static SpcxFile ImportFromSpc(string path) { ... }
    
    // Export to valid SPC file
    public byte[] ExportToSpcBytes() { ... }
}
```

### 9. SpcAnalyzer

Sound driver detection and memory analysis.

```csharp
public sealed class SpcAnalyzer {
    // Supported drivers
    public static readonly string[] SupportedDrivers = {
        "NSPC", "Akao", "HAL Lab", "Capcom", "Konami",
        "Rare", "Enix", "Hudson", "Namco", "Taito"
    };

    public string DetectDriver(byte[] ram) { ... }
    public SpcxSampleInfo[] ExtractSamples(byte[] ram, byte[] dsp) { ... }
    public SpcxMemoryUsage AnalyzeMemory(byte[] ram, byte[] dsp) { ... }
}
```

## Threading Model

### Audio Thread (Real-time)

- Called by host at audio rate (typically 44.1kHz)
- Must complete within buffer time (~5ms for 256 samples)
- No allocations, locks, or I/O
- Uses lock-free queues for parameter changes

### UI Thread

- Handles user input and rendering
- Can allocate, use locks
- Communicates with audio thread via lock-free queues

### Worker Thread(s)

- Background tasks: file I/O, BRR encoding, export
- ThreadPool or dedicated background workers
- Progress reporting to UI thread

## Memory Management

### Real-time Considerations

```csharp
// Pre-allocate buffers at initialization
private readonly float[] _mixBuffer = new float[MaxBufferSize];
private readonly int[] _voiceBuffer = new int[MaxBufferSize];

// Use Span<T> to avoid allocations
public void Process(Span<float> output) {
    // Direct buffer access, no allocations
}
```

### Sample Memory Budget

```csharp
public class MemoryBudget {
    public const int TotalRam = 65536;
    public const int DriverReserve = 3000;      // Sound driver code
    public const int SequenceReserve = 5000;    // Sequence data
    public const int EchoBufferMax = 15360;     // Echo (EDL × 2048)
    
    public int AvailableForSamples => 
        TotalRam - DriverReserve - SequenceReserve - CurrentEchoSize;
}
```

## Plugin Parameters

VST3 parameters map to SPC features:

| Parameter ID | Name           | Range      | Description             |
| ------------ | -------------- | ---------- | ----------------------- |
| 0-7          | Ch N Volume    | 0-127      | Per-channel volume      |
| 8-15         | Ch N Pan       | 0-127      | Per-channel pan         |
| 16-23        | Ch N Mute      | 0-1        | Per-channel mute        |
| 100          | Master Volume  | 0-127      | Main output volume      |
| 101          | Echo Delay     | 0-15       | Echo delay (×16ms)      |
| 102          | Echo Feedback  | -128-127   | Echo feedback amount    |
| 103          | Echo Enabled   | 0-1        | Echo on/off             |

## State Serialization

Plugin state is saved/restored in the DAW project:

```csharp
public void GetState(Stream output) {
    using var writer = new BinaryWriter(output);

    // Version
    writer.Write(StateVersion);

    // Project data (SPCX format, embedded)
    WriteEmbeddedSpcx(writer);

    // UI state
    WriteEditorState(writer);
}
```

## Error Handling

```csharp
public class PluginException : Exception {
    public ErrorCode Code { get; }
    public string UserMessage { get; }

    // Show user-friendly message, log technical details
}

public enum ErrorCode {
    InvalidSpcFile,
    SampleTooLarge,
    OutOfMemory,
    UnsupportedDriver,
    ExportFailed
}
```

## Platform Support

Primary target: Windows x64 with VST3

| Platform       | Support   | Notes                     |
| -------------- | --------- | ------------------------- |
| Windows x64    | ✅ Full   | Primary platform          |
| Windows ARM64  | ⏳ Future | .NET 10 supports it       |
| macOS (AU/VST3)| ⏳ Future | Requires MAUI/Avalonia    |
| Linux (VST3)   | ⏳ Future | Requires Avalonia UI      |

## Dependencies

| Dependency              | Version | Purpose            |
| ----------------------- | ------- | ------------------ |
| .NET                    | 10.0    | Runtime            |
| VST3 SDK                | 3.7.x   | Plugin interface   |
| System.IO.Compression   | -       | SPCX archives      |
| Avalonia (or MAUI)      | 11.x    | Cross-platform UI  |

## Performance Targets

| Metric            | Target  | Notes                 |
| ----------------- | ------- | --------------------- |
| Audio latency     | <10ms   | Buffer size dependent |
| CPU usage         | <5%     | Per instance          |
| Memory usage      | <50MB   | Per instance          |
| UI responsiveness | <16ms   | 60fps minimum         |
| Export time       | <5s     | Typical project       |
