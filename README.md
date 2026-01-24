# Ableton SNES SPC Plugin

**Current Version**: 0.1.0 Alpha  
**Status**: In Development 🚧  
**License**: MIT

A VST3 plugin for Ableton Live (and other DAWs) that enables editing and playback of SNES SPC music files with full hardware-accurate emulation.

---

## 🎯 Project Vision

Create a professional-grade audio plugin that brings SNES music composition directly into modern DAWs, allowing artists to:

- **Import** existing SPC files and edit them
- **Compose** new SNES music with authentic sound
- **Export** to valid SPC format playable on real hardware
- **Collaborate** using a rich project format (.spcx) that preserves all editing state

---

## 📥 Installation

### Alpha Release (v0.1.0)

> ⚠️ **Alpha Software**: This is an early alpha release. Expect bugs and missing features. GUI is not yet implemented - all controls are via DAW automation parameters.

**Download**: [Latest Release](https://github.com/TheAnsarya/ableton-snes-spc/releases)

**Quick Install (Windows)**:
1. Download `SnesSpcVst3-0.1.0-win64.zip`
2. Extract to `%APPDATA%\VST3\`
3. Rescan plugins in your DAW

For detailed build instructions, see [BUILDING.md](docs/BUILDING.md).

---

## ⚡ Quick Start

1. Load the plugin on an audio track in your DAW
2. Use automation to set "Load SPC" parameter (or prepare to add file loading in v0.2.0)
3. Use "Play/Pause" parameter to start playback
4. Control individual voices with mute/solo/volume parameters

For complete usage guide, see [USER_GUIDE.md](docs/USER_GUIDE.md).

---

## ✨ Features (v0.1.0 Alpha)

### Core Emulation ✅
- Complete SPC700 CPU emulation (all 256 instructions)
- S-DSP audio processor with 8-voice stereo output
- Hardware-accurate BRR sample decompression and encoding
- Echo/reverb effects, ADSR envelopes, FIR filters
- Cycle-accurate timing

### Audio Engine ✅
- Real-time playback at 32 kHz (resampled to any DAW rate)
- Per-voice mute/solo/volume controls (8 voices)
- Master volume control
- Loop enable/disable
- Seek to position
- Waveform capture for visualization

### MIDI Support ✅
- MIDI note-on triggers voice playback (C3-G3 = Voice 0-7)
- MIDI note-off for voice release
- Velocity controls voice volume
- CC learn for parameter mapping

### VST3 Integration ✅
- 35+ automatable parameters
- DAW transport sync
- Tempo and time signature awareness
- State save/restore
- Preset management

### What's Missing (Coming in v0.2.0)
- ❌ GUI (all controls via automation)
- ❌ Visual waveform display
- ❌ Sample browser
- ❌ File drag-and-drop

---

## 🎮 What is SPC?

SPC files capture the complete state of the SNES's Sony SPC700 audio chip, including:

- 64KB of audio RAM
- 8 simultaneous sound channels
- BRR-compressed samples
- Echo/reverb effects
- Sequence data (music notation)

## 🔌 Plugin Architecture

```text
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

### Project: SPCX (.spcx) - Coming in v0.3.0

Custom extended format for rich editing:
- Full SPC data
- Extended metadata
- Source samples (pre-BRR WAV)
- Undo/redo history
- Annotations and markers

### Output: SPC (.spc)

Valid SPC file playable on:
- Real SNES hardware (via flash cart)
- SPC players and emulators

---

## 🔌 Plugin Architecture

```text
┌─────────────────────────────────────────┐
│     VST3 Plugin Host (DAW)              │
├─────────────────────────────────────────┤
│  ┌───────────────────────────────────┐  │
│  │   SNES SPC Plugin (VST3/C++)      │  │
│  │   ├─ Processor (Audio Thread)     │  │
│  │   ├─ Controller (UI Thread)       │  │
│  │   └─ .NET Host (Interop)          │  │
│  │       ↓                            │  │
│  │   ┌───────────────────────────┐   │  │
│  │   │ SpcPlugin.Core (.NET 10)  │   │  │
│  │   ├─ SpcEngine (Orchestration)│   │  │
│  │   ├─ Spc700 (CPU Emulator)    │   │  │
│  │   ├─ SDsp (Audio Processor)   │   │  │
│  │   ├─ BrrCodec (Compression)   │   │  │
│  │   ├─ MidiProcessor            │   │  │
│  │   └─ ProjectManager           │   │  │
│  └───────────────────────────────────┘  │
└─────────────────────────────────────────┘
```

---
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

| Component         | Technology                 |
| ----------------- | -------------------------- |
| Plugin Framework  | VST3 SDK + C++/CLI wrapper |
| Core Logic        | C# / .NET 10               |
| UI Framework      | MAUI or Avalonia           |
| Audio Processing  | Native interop             |
| Build System      | CMake + MSBuild            |
| Testing           | xUnit                      |

## 🚧 SNES Hardware Constraints

The plugin enforces these limitations to ensure valid SPC output:

| Constraint    | Value    | Plugin Behavior            |
| ------------- | -------- | -------------------------- |
| Channels      | 8 max    | Hard limit, no workaround  |
| Sample RAM    | 64KB     | Memory usage meter         |
| Sample Rate   | ≤32kHz   | Auto-resample if needed    |
| Sample Format | BRR      | Auto-encode from WAV       |
| Echo Buffer   | 0-30KB   | Reduce if exceeds          |

## 📂 Repository Structure

```text
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
- Visual Studio 2022 Build Tools with C++ workload
- CMake 3.21+
- VST3 SDK (cloned to `C:\vst3sdk`)
- Ableton Live 11+ (for testing) or another VST3 host

### Building

```powershell
# Clone the repository
git clone https://github.com/TheAnsarya/ableton-snes-spc.git
cd ableton-snes-spc

# Clone VST3 SDK (if not already installed)
git clone --recursive https://github.com/steinbergmedia/vst3sdk.git C:\vst3sdk

# Build the VST3 plugin
$env:VST3_SDK_ROOT = "C:/vst3sdk"
.\build-vst3.ps1

# Install to user VST3 folder
.\build-vst3.ps1 -Install

# Or install manually
Copy-Item -Recurse build\VST3\Debug\SnesSpcVst3.vst3 "$env:LOCALAPPDATA\Programs\Common\VST3\"
```

### Build Options

```powershell
.\build-vst3.ps1              # Debug build
.\build-vst3.ps1 -Release     # Release build
.\build-vst3.ps1 -Clean       # Clean build
.\build-vst3.ps1 -Install     # Build and install
.\build-vst3.ps1 -NativeAot   # Build with Native AOT
```

## 🎨 Plugin Icon

The plugin features a custom icon with a musical note surrounded by colorful Japanese Super Famicom ABXY buttons.

To regenerate the icon:
```powershell
pip install svglib reportlab Pillow
python tools/generate_icon.py
```

## 📄 License

MIT License - See [LICENSE](LICENSE) for details.

## 🤝 Contributing

Contributions welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) first.

## 📚 Related Projects

- [GameInfo](https://github.com/TheAnsarya/GameInfo) - SNES audio tools library
- [VST.NET](https://github.com/obiwanjacobi/vst.net) - VST for .NET framework
