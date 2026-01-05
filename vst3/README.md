# VST3 Plugin Build Instructions

This directory contains the VST3 plugin wrapper for the SNES SPC emulator.

## Prerequisites

1. **VST3 SDK**: Download from [Steinberg](https://www.steinberg.net/developers/)
2. **CMake**: Version 3.21 or higher
3. **C++ Compiler**: Visual Studio 2022 (Windows), Clang (macOS), GCC (Linux)
4. **.NET 10 SDK**: For building the managed assembly

## Building

### Windows

```powershell
# Set environment variable for VST3 SDK location
$env:VST3_SDK_ROOT = "C:\path\to\vst3sdk"

# Build the .NET core library first
cd ../
dotnet build -c Release

# Create build directory
cd vst3
mkdir build
cd build

# Configure and build
cmake .. -G "Visual Studio 17 2022" -A x64
cmake --build . --config Release
```

### macOS

```bash
export VST3_SDK_ROOT=/path/to/vst3sdk

# Build .NET core library
cd ../
dotnet build -c Release

# Build VST3
cd vst3
mkdir build && cd build
cmake .. -GNinja
ninja
```

### Linux

```bash
export VST3_SDK_ROOT=/path/to/vst3sdk

# Build .NET core library
cd ../
dotnet build -c Release

# Build VST3
cd vst3
mkdir build && cd build
cmake .. -GNinja
ninja
```

## Architecture

The VST3 plugin consists of:

1. **C++ VST3 wrapper** (`src/`): Implements the VST3 interfaces
2. **.NET Core emulator** (`../src/SpcPlugin.Core/`): The actual SPC700/DSP emulation
3. **.NET hosting** (`src/dotnet_host.cpp`): Bridges C++ and .NET

### Data Flow

```text
DAW -> VST3 Plugin (C++) -> .NET Host -> SpcEngine -> SPC700 + DSP -> Audio Output
```

## Current Status

- [x] Basic VST3 project structure
- [x] Parameter definitions (volume, play/pause, voice enable/solo)
- [x] Processor and Controller scaffolding
- [ ] .NET runtime hosting implementation
- [ ] Full audio processing pipeline
- [ ] GUI (VSTGUI or custom)

## Next Steps

1. Complete the .NET hosting implementation in `dotnet_host.cpp`
2. Add UnmanagedCallersOnly exports to SpcPlugin.Core for native interop
3. Implement the audio processing callback to generate samples
4. Add a basic GUI for file loading and voice control
5. Test with Ableton Live and other DAWs
