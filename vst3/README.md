# VST3 Plugin Build Instructions

This directory contains the VST3 plugin wrapper for the SNES SPC emulator.

## Prerequisites

### 1. VST3 SDK

Download from [Steinberg Developer Portal](https://www.steinberg.net/developers/):

1. Register for a free Steinberg Developer account
2. Download VST 3 Audio Plug-Ins SDK (e.g., `vst3sdk_3.7.x.zip`)
3. Extract to a location like:
   - Windows: `C:\SDKs\vst3sdk`
   - macOS: `~/SDKs/vst3sdk`
   - Linux: `/opt/vst3sdk`

**Important**: The VST3 SDK requires you to accept Steinberg's licensing terms.

### 2. Build Tools

| Platform | Requirements |
|----------|--------------|
| Windows  | Visual Studio 2022 (with C++ workload), CMake 3.21+ |
| macOS    | Xcode Command Line Tools, CMake 3.21+, Ninja (optional) |
| Linux    | GCC 11+, CMake 3.21+, Ninja |

### 3. .NET 10 SDK

Download from [Microsoft .NET](https://dot.net/download) for Native AOT compilation.

## Environment Setup

### Windows (PowerShell)

```powershell
# Set SDK path (add to your $PROFILE for persistence)
$env:VST3_SDK_ROOT = "C:\SDKs\vst3sdk"

# Verify
Test-Path $env:VST3_SDK_ROOT  # Should return True
```

### macOS / Linux (Bash)

```bash
# Add to ~/.bashrc or ~/.zshrc
export VST3_SDK_ROOT="$HOME/SDKs/vst3sdk"

# Verify
ls $VST3_SDK_ROOT  # Should list SDK contents
```

## Building

### Step 1: Build .NET Core Library

```bash
# From repository root
dotnet build -c Release

# For Native AOT (smaller, faster, no runtime dependency)
dotnet publish -c Release -r win-x64 --self-contained -p:PublishAot=true
# Replace win-x64 with osx-arm64, osx-x64, or linux-x64 as needed
```

### Step 2: Build VST3 Plugin

#### Windows

```powershell
cd vst3
mkdir build
cd build

# Configure
cmake .. -G "Visual Studio 17 2022" -A x64

# Build
cmake --build . --config Release

# Output: build/VST3/Release/SnesSpcVst3.vst3
```

#### macOS

```bash
cd vst3
mkdir build && cd build

# Configure (Xcode)
cmake .. -G Xcode

# Build
cmake --build . --config Release

# Or with Ninja (faster)
cmake .. -G Ninja -DCMAKE_BUILD_TYPE=Release
ninja

# Output: build/VST3/SnesSpcVst3.vst3
```

#### Linux

```bash
cd vst3
mkdir build && cd build

# Configure
cmake .. -G Ninja -DCMAKE_BUILD_TYPE=Release

# Build
ninja

# Output: build/VST3/SnesSpcVst3.vst3
```

### Build Options

| Option | Description | Default |
|--------|-------------|---------|
| `USE_NATIVE_AOT` | Use Native AOT compiled .NET library | OFF |
| `BUILD_TESTS` | Build VST3 validator tests | OFF |

Example with options:
```bash
cmake .. -DUSE_NATIVE_AOT=ON -DBUILD_TESTS=ON
```

## Installation

Copy the built `.vst3` bundle to:

| Platform | Location |
|----------|----------|
| Windows | `C:\Program Files\Common Files\VST3\` |
| macOS | `~/Library/Audio/Plug-Ins/VST3/` or `/Library/Audio/Plug-Ins/VST3/` |
| Linux | `~/.vst3/` or `/usr/lib/vst3/` |

**Note**: Also copy the Native AOT library (`SpcPlugin.Core.dll` or `libSpcPlugin.Core.so`) next to the `.vst3` bundle.

## Architecture

```
┌─────────────────┐    ┌──────────────┐    ┌────────────────┐
│  DAW (Ableton)  │───▶│ VST3 Plugin  │───▶│ DotNetHost     │
│                 │    │ (C++)        │    │ (Dynamic Load) │
└─────────────────┘    └──────────────┘    └───────┬────────┘
                                                   │
       ┌───────────────────────────────────────────┘
       ▼
┌──────────────────────────────────────────────────────────┐
│                SpcPlugin.Core (.NET Native AOT)          │
├──────────────┬──────────────┬──────────────┬────────────┤
│ NativeExports│  SpcEngine   │   Spc700     │    SDsp    │
│ (C ABI)      │ (Coordinator)│ (CPU)        │ (Audio DSP)│
└──────────────┴──────────────┴──────────────┴────────────┘
```

### Data Flow

1. **Parameter Changes** (volume, play/pause): VST3 → processor → DotNetHost → SpcEngine
2. **MIDI Events**: VST3 → processMidiEvents() → DotNetHost → MidiProcessor → SpcEngine
3. **Audio Output**: SpcEngine → process() → interleaved buffer → deinterleave → VST3 channels
4. **File Loading**: Controller → message → Processor → DotNetHost → SpcEngine

## Plugin Features

### Parameters

| Category | Parameters | IDs |
|----------|------------|-----|
| Master | Volume, Play/Pause, Loop, Position | 0-3 |
| Voice Enable | Voice 1-8 | 100-107 |
| Voice Solo | Solo 1-8 | 200-207 |
| Voice Volume | Volume 1-8 | 300-307 |
| Pitch Bend | Bend 1-8, Range | 400-407, 500 |

### MIDI Support

- Note On/Off → Trigger samples
- Control Change → Various parameters
- Pitch Bend → Per-channel pitch adjustment
- Aftertouch → Modulation

### File Loading

The controller provides methods for UI integration:
- `loadSpcFile(path)` - Load from file path
- `loadSpcData(data, length)` - Load from memory

## Current Status

- [x] VST3 project structure
- [x] Complete parameter set
- [x] Audio processor with .NET bridge
- [x] MIDI event handling
- [x] Pitch bend support
- [x] State save/restore with embedded SPC
- [x] File load messaging system
- [ ] GUI (planned: VSTGUI or custom)
- [ ] Native AOT validation tests

## Troubleshooting

### "VST3_SDK_ROOT is not defined"

Set the environment variable pointing to your VST3 SDK installation.

### "Cannot find library: SpcPlugin.Core.dll"

Ensure the Native AOT built library is in the same directory as the VST3 plugin.

### Plugin loads but no audio

1. Check that an SPC file has been loaded
2. Verify the Play parameter is enabled
3. Check voice mute/solo states

### CMake cannot find SDK modules

Ensure `VST3_SDK_ROOT` points to the root of the SDK (should contain `cmake/modules/`).
