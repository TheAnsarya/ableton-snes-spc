# Building the SNES SPC VST3 Plugin

**Version**: 0.4.0  
**Last Updated**: January 25, 2026

Complete guide to building the plugin from source.

## Build Output

When successfully built, you will have:
- `SnesSpcVst3.vst3` - The VST3 plugin bundle
- `SpcPlugin.Core.dll` - The .NET core library

## Prerequisites

### Required Software

1. **.NET 10 SDK** (or later)
   - Download from: https://dotnet.microsoft.com/download
   - Verify installation: `dotnet --version`

2. **CMake** (version 3.21 or later)
   - Download from: https://cmake.org/download/
   - Add to PATH during installation
   - Verify installation: `cmake --version`

3. **Visual Studio 2022** (Windows) or **Xcode** (macOS)
   - Windows: VS 2022 with "Desktop development with C++" workload
   - macOS: Install Xcode command line tools: `xcode-select --install`

4. **VST3 SDK**
   - Clone from: https://github.com/steinbergmedia/vst3sdk
   - Recommended version: 3.7.7+

### Optional Tools

- **Python 3.8+** (for icon generation)
- **Git** (for version control)

---

## Quick Build (Windows)

### 1. Clone the Repository

```powershell
git clone https://github.com/TheAnsarya/ableton-snes-spc.git
cd ableton-snes-spc
```

### 2. Setup VST3 SDK

Run the automated setup script:

```powershell
.\setup-vst3-sdk.ps1
```

This will:
- Clone the VST3 SDK to `vst3sdk/`
- Set the `VST3_SDK_ROOT` environment variable
- Initialize submodules

**OR** manually:

```powershell
# Clone VST3 SDK
git clone --recursive https://github.com/steinbergmedia/vst3sdk.git

# Set environment variable
$env:VST3_SDK_ROOT = "$(pwd)\vst3sdk"
```

### 3. Build the .NET Core Library

```powershell
cd src\SpcPlugin.Core
dotnet build --configuration Release
cd ..\..
```

### 4. Build the VST3 Plugin

Use the automated build script:

```powershell
.\build-vst3.ps1 -Configuration Release
```

**OR** manually:

```powershell
# Configure CMake
cd vst3
cmake -B build -DCMAKE_BUILD_TYPE=Release

# Build
cmake --build build --config Release

# Output: build\VST3\Release\SnesSpcVst3.vst3\
```

### 5. Install the Plugin

Copy the built plugin to your VST3 folder:

```powershell
# System (requires admin)
Copy-Item "vst3\build\VST3\Release\SnesSpcVst3.vst3" `
  "C:\Program Files\Common Files\VST3\" -Recurse -Force

# Or user folder
Copy-Item "vst3\build\VST3\Release\SnesSpcVst3.vst3" `
  "$env:APPDATA\VST3\" -Recurse -Force
```

Copy the .NET library:

```powershell
Copy-Item "src\SpcPlugin.Core\bin\Release\net10.0\SpcPlugin.Core.dll" `
  "$env:APPDATA\VST3\SnesSpcVst3.vst3\Contents\x86_64-win\" -Force
```

---

## Build Options

### Debug Build

For development with debugging symbols:

```powershell
# .NET Core
dotnet build --configuration Debug

# VST3
.\build-vst3.ps1 -Configuration Debug
```

### Native AOT Build

For smaller, self-contained deployment:

```powershell
# .NET Core with Native AOT
cd src\SpcPlugin.Core
dotnet publish --configuration Release /p:PublishAot=true
cd ..\..

# VST3 with Native AOT flag
cd vst3
cmake -B build -DCMAKE_BUILD_TYPE=Release -DUSE_NATIVE_AOT=ON
cmake --build build --config Release
```

### Enable Custom GUI Views

For advanced custom VSTGUI components:

```powershell
cd vst3
cmake -B build -DENABLE_CUSTOM_VIEWS=ON
cmake --build build --config Release
```

---

## macOS Build

### Prerequisites

```bash
# Install Homebrew
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"

# Install CMake
brew install cmake

# Install .NET SDK
brew install --cask dotnet-sdk
```

### Build Steps

```bash
# Clone repository
git clone https://github.com/TheAnsarya/ableton-snes-spc.git
cd ableton-snes-spc

# Setup VST3 SDK
export VST3_SDK_ROOT="$(pwd)/vst3sdk"
git clone --recursive https://github.com/steinbergmedia/vst3sdk.git

# Build .NET Core
cd src/SpcPlugin.Core
dotnet build --configuration Release
cd ../..

# Build VST3
cd vst3
cmake -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build --config Release

# Install
cp -R build/VST3/Release/SnesSpcVst3.vst3 \
  ~/Library/Audio/Plug-Ins/VST3/
cp src/SpcPlugin.Core/bin/Release/net10.0/libSpcPlugin.Core.dylib \
  ~/Library/Audio/Plug-Ins/VST3/SnesSpcVst3.vst3/Contents/MacOS/
```

---

## Linux Build

### Prerequisites

```bash
# Debian/Ubuntu
sudo apt update
sudo apt install cmake build-essential git

# Install .NET SDK
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0
```

### Build Steps

```bash
# Clone repository
git clone https://github.com/TheAnsarya/ableton-snes-spc.git
cd ableton-snes-spc

# Setup VST3 SDK
export VST3_SDK_ROOT="$(pwd)/vst3sdk"
git clone --recursive https://github.com/steinbergmedia/vst3sdk.git

# Build .NET Core
cd src/SpcPlugin.Core
dotnet build --configuration Release
cd ../..

# Build VST3
cd vst3
cmake -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build --config Release

# Install
mkdir -p ~/.vst3
cp -R build/VST3/Release/SnesSpcVst3.vst3 ~/.vst3/
cp src/SpcPlugin.Core/bin/Release/net10.0/libSpcPlugin.Core.so \
  ~/.vst3/SnesSpcVst3.vst3/Contents/x86_64-linux/
```

---

## Troubleshooting

### VST3_SDK_ROOT not found

**Error:** `VST3_SDK_ROOT is not defined`

**Solution:**
```powershell
# Set environment variable
$env:VST3_SDK_ROOT = "C:\path\to\vst3sdk"

# Or set permanently (Windows)
[System.Environment]::SetEnvironmentVariable(
  "VST3_SDK_ROOT", "C:\path\to\vst3sdk", "User")
```

### .NET Library Not Found

**Error:** Plugin loads but outputs silence

**Solution:** Ensure `SpcPlugin.Core.dll` is in the same directory as the VST3:
```powershell
# Check VST3 structure
dir "$env:APPDATA\VST3\SnesSpcVst3.vst3\Contents\x86_64-win\"

# Should contain SpcPlugin.Core.dll
```

### CMake Configuration Fails

**Error:** `Could not find a package configuration file`

**Solution:** Ensure VST3 SDK is properly cloned with submodules:
```powershell
cd vst3sdk
git submodule update --init --recursive
```

### Visual Studio Version Mismatch

**Error:** Platform Toolset not found

**Solution:** Update CMake generator:
```powershell
cmake -B build -G "Visual Studio 17 2022" -A x64
```

### Plugin Doesn't Appear in DAW

**Solution:**
1. Verify plugin location: `%APPDATA%\VST3\` or `C:\Program Files\Common Files\VST3\`
2. Rescan plugins in your DAW
3. Check DAW plugin blacklist/preferences
4. Run VST3 validator: `validator.exe SnesSpcVst3.vst3`

---

## Development Workflow

### Iterative Development

For faster iteration during development:

```powershell
# Terminal 1: Watch and rebuild .NET
cd src\SpcPlugin.Core
dotnet watch build

# Terminal 2: Rebuild VST3 as needed
cd vst3\build
cmake --build . --config Debug

# Terminal 3: Copy to test location
Copy-Item "VST3\Debug\SnesSpcVst3.vst3" "$env:APPDATA\VST3\" -Recurse -Force
```

### Running Tests

```powershell
# Run .NET unit tests
cd tests\SpcPlugin.Tests
dotnet test --configuration Debug --verbosity normal

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### VST3 Validator

Test plugin compliance:

```powershell
# Build VST3 SDK validator
cd vst3sdk\build
cmake --build . --target validator --config Release

# Run validator on plugin
.\bin\Release\validator.exe ..\..\vst3\build\VST3\Release\SnesSpcVst3.vst3
```

---

## Project Structure

```
ableton-snes-spc/
├── src/
│   └── SpcPlugin.Core/          # .NET core library
│       ├── Audio/               # SPC engine, BRR codec
│       ├── Emulation/           # SPC700 CPU, S-DSP
│       ├── Interop/             # Native exports for C++
│       └── ...
├── vst3/
│   ├── src/                     # VST3 C++ code
│   │   ├── spc_processor.cpp    # Audio processor
│   │   ├── spc_controller.cpp   # UI controller
│   │   └── dotnet_host.cpp      # .NET interop
│   ├── resource/                # Plugin resources
│   └── CMakeLists.txt           # Build configuration
├── tests/
│   └── SpcPlugin.Tests/         # Unit tests
├── docs/                        # Documentation
└── tools/                       # Build tools
```

---

## Next Steps

After building:
1. Read [USER_GUIDE.md](USER_GUIDE.md) for usage instructions
2. See [API_REFERENCE.md](API_REFERENCE.md) for developer documentation
3. Check [CONTRIBUTING.md](CONTRIBUTING.md) to contribute
4. Report issues at https://github.com/TheAnsarya/ableton-snes-spc/issues
