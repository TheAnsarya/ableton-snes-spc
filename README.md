# Ableton SNES SPC Plugin

A VST3 plugin for Ableton Live (and other DAWs) that enables editing and playback of SNES SPC music files with full hardware-accurate emulation.

## 🎯 Project Vision

Create a professional-grade audio plugin that brings SNES music composition directly into modern DAWs, allowing artists to:

- **Import** existing SPC files and edit them
- **Compose** new SNES music with authentic sound
- **Export** to valid SPC format playable on real hardware
- **Collaborate** using a rich project format (.spcx) that preserves all editing state

## 🎮 What is SPC?

SPC files capture the complete state of the SNES's Sony SPC700 audio chip, including:
- 64KB of audio RAM
- 8 simultaneous sound channels  
- BRR-compressed samples
- Echo/reverb effects
- Sequence data (music notation)

## 🔌 Plugin Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    VST3 Plugin Host (Ableton)                │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │              SNES SPC Plugin (VST3)                  │   │
│  ├─────────────────────────────────────────────────────┤   │
│  │                                                     │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌────────────┐  │   │
│  │  │   UI Layer  │  │  Editor     │  │  Transport │  │   │
│  │  │  (WPF/MAUI) │  │  (Channels) │  │  Control   │  │   │
│  │  └──────┬──────┘  └──────┬──────┘  └─────┬──────┘  │   │
│  │         │                │               │         │   │
│  │  ┌──────┴────────────────┴───────────────┴──────┐  │   │
│  │  │              Core Engine (.NET 10)            │  │   │
│  │  ├───────────────────────────────────────────────┤  │   │
│  │  │  ┌─────────┐  ┌─────────┐  ┌──────────────┐  │  │   │
│  │  │  │  SPC700 │  │   DSP   │  │     BRR      │  │  │   │
│  │  │  │   CPU   │  │ (S-DSP) │  │ Codec/Render │  │  │   │
│  │  │  └─────────┘  └─────────┘  └──────────────┘  │  │   │
│  │  │  ┌─────────┐  ┌─────────┐  ┌──────────────┐  │  │   │
│  │  │  │ Project │  │ Import/ │  │   Sequence   │  │  │   │
│  │  │  │  (SPCX) │  │  Export │  │   Compiler   │  │  │   │
│  │  │  └─────────┘  └─────────┘  └──────────────┘  │  │   │
│  │  └───────────────────────────────────────────────┘  │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

## 📁 File Formats

### Input: SPC (.spc)
Standard SNES music file format containing:
- SPC700 RAM snapshot (64KB)
- DSP registers (128 bytes)
- ID666 metadata (song info)

### Project: SPCX (.spcx)
Custom extended format for rich editing:
- Full SPC data
- Extended metadata (unlimited)
- Source samples (pre-BRR WAV)
- Undo/redo history
- Channel solo/mute states
- Custom filter settings
- Annotations and markers

### Output: SPC (.spc)
Valid SPC file playable on:
- Real SNES hardware (via flash cart)
- SPC players (SPC700 Player, etc.)
- Emulators

## ⚙️ Features

### Core Features
- [ ] Hardware-accurate SPC700 CPU emulation
- [ ] S-DSP audio processing with all effects
- [ ] BRR sample encoding/decoding
- [ ] Real-time audio rendering

### Editing Features
- [ ] 8-channel mixer view
- [ ] Sample editor with waveform display
- [ ] Piano roll sequence editor
- [ ] Echo/reverb configuration
- [ ] ADSR envelope visualization

### Project Features
- [ ] SPCX project format
- [ ] Import from SPC
- [ ] Export to SPC
- [ ] Preset management
- [ ] Undo/redo system

### Integration Features
- [ ] VST3 parameter automation
- [ ] MIDI input for live playing
- [ ] Sample rate conversion
- [ ] Latency compensation

## 🛠️ Technology Stack

| Component | Technology |
|-----------|------------|
| Plugin Framework | VST3 SDK + C++/CLI wrapper |
| Core Logic | C# / .NET 10 |
| UI Framework | MAUI or Avalonia |
| Audio Processing | Native interop |
| Build System | CMake + MSBuild |
| Testing | xUnit |

## 🚧 SNES Hardware Constraints

The plugin enforces these limitations to ensure valid SPC output:

| Constraint | Value | Plugin Behavior |
|------------|-------|-----------------|
| Channels | 8 max | Hard limit, no workaround |
| Sample RAM | 64KB | Memory usage meter |
| Sample Rate | ≤32kHz | Auto-resample if needed |
| Sample Format | BRR | Auto-encode from WAV |
| Echo Buffer | 0-30KB | Reduce if exceeds |

## 📂 Repository Structure

```
ableton-snes-spc/
├── docs/                    # Documentation
│   ├── architecture/        # Technical architecture docs
│   ├── formats/             # File format specifications
│   ├── guides/              # User and developer guides
│   └── research/            # Research notes and references
├── src/                     # Source code
│   ├── SpcPlugin.Core/      # Core engine (.NET)
│   ├── SpcPlugin.Vst/       # VST3 wrapper (C++/CLI)
│   ├── SpcPlugin.Ui/        # UI components
│   └── SpcPlugin.Tests/     # Unit tests
├── tools/                   # Build and development tools
├── samples/                 # Sample SPC files for testing
├── ~docs/                   # Development documentation
│   ├── session-logs/        # AI session logs
│   ├── chat-logs/           # Chat history
│   └── plans/               # Planning documents
└── build/                   # Build output
```

## 🚀 Getting Started

### Prerequisites
- .NET 10 SDK
- Visual Studio 2022 with C++ workload
- CMake 3.25+
- Ableton Live 11+ (for testing)

### Building
```powershell
# Clone the repository
git clone https://github.com/TheAnsarya/ableton-snes-spc.git
cd ableton-snes-spc

# Build the solution
dotnet build

# Run tests
dotnet test
```

## 📄 License

MIT License - See [LICENSE](LICENSE) for details.

## 🤝 Contributing

Contributions welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) first.

## 📚 Related Projects

- [GameInfo](https://github.com/TheAnsarya/GameInfo) - SNES audio tools library
- [VST.NET](https://github.com/obiwanjacobi/vst.net) - VST for .NET framework
