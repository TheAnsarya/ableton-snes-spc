# Release Notes - v0.2.0 Beta

**Release Date:** January 24, 2026  
**Release Type:** Beta (Pre-release)  
**GitHub Release:** [v0.2.0](https://github.com/TheAnsarya/ableton-snes-spc/releases/tag/v0.2.0)

---

## Overview

Version 0.2.0 Beta adds basic VSTGUI integration for the plugin editor window. This release establishes the GUI framework foundation while deferring advanced visualizers to v0.3.0 due to VSTGUI API compatibility updates needed.

---

## New Features

### GUI Framework

- **Plugin Editor Window**: Basic VSTGUI editor implementation with parameter binding
- **UI Description**: Layout definitions for the plugin interface (spc_editor.uidesc)
- **Control Bindings**: Real-time parameter synchronization between UI and audio engine
- **Standard Controls**: Support for knobs, sliders, buttons, and text displays
- **Styling**: Consistent color scheme and font definitions

### Build System Improvements

- **Flexible Configuration**: CMake option `ENABLE_CUSTOM_VIEWS` for build-time customization
- **Header Resolution**: Fixed VSTGUI 4.x include paths for view creator components
- **API Compatibility**: Updated namespace resolution for UIViewCreator attributes

---

## Technical Details

### Build Configuration

Two build modes are now supported:

```bash
# Basic GUI (recommended for v0.2.0)
cmake -DSMTG_ENABLE_VSTGUI_SUPPORT=ON -DENABLE_CUSTOM_VIEWS=OFF

# Full GUI with custom views (requires API fixes, deferred to v0.3.0)
cmake -DSMTG_ENABLE_VSTGUI_SUPPORT=ON -DENABLE_CUSTOM_VIEWS=ON
```

### Files Modified

| File | Change |
|------|--------|
| `vst3/src/gui/view_switcher.h` | Added VSTGUI 4.x includes |
| `vst3/src/gui/waveform_view.h` | Added VSTGUI 4.x includes |
| `vst3/src/gui/spectrum_view.h` | Added VSTGUI 4.x includes |
| `vst3/src/gui/preset_browser.h` | Added VSTGUI 4.x includes |

### API Compatibility Notes

VSTGUI 4.x requires:
- `#include "vstgui/uidescription/iviewcreator.h"` for ViewCreatorAdapter
- `#include "vstgui/uidescription/detail/uiviewcreatorattributes.h"` for kCViewContainer constants
- Updated drawing API using CGraphicsPath instead of moveTo/lineTo

---

## GitHub Issues Closed

This release closes the following issues:

| Issue | Title | Description |
|-------|-------|-------------|
| #7 | SPC700 CPU Emulator | Cycle-accurate emulation of all 256 opcodes |
| #8 | S-DSP Audio Processor | Hardware-accurate digital signal processor |
| #9 | BRR Sample Codec | Bit-perfect BRR decoding |
| #10 | Audio Pipeline Integration | Real-time audio processing |
| #38 | VST3 SDK Integration | Full VST3 plugin framework |
| #39 | C++/CLI Bridge Layer | Native AOT .NET interop |
| #40 | Audio Processing Callbacks | VST3 process callbacks |
| #41 | Parameter Automation | 35+ automatable parameters |
| #2 | Epic: Core Audio Engine | All core components complete |
| #15 | Epic: VST3 Plugin Wrapper | All wrapper components complete |

---

## Known Issues

1. **Custom Visualizers**: WaveformView, SpectrumView, and PresetBrowser require VSTGUI 4.x drawing API updates
2. **CDrawContext API**: Methods like `moveTo()` and `lineTo()` need migration to `CGraphicsPath`
3. **View Registration**: `UIViewFactory::registerViewCreator()` mechanism needs updating

These issues are tracked and targeted for v0.3.0.

---

## Test Status

- **Total Tests**: 146
- **Passed**: 146 ✅
- **Failed**: 0
- **Coverage**: Comprehensive across all core components

---

## Build Requirements

- .NET 10 SDK (with Native AOT support)
- VST3 SDK 3.7+
- CMake 3.21+
- Visual Studio 2022 or later
- Windows 10/11 x64

---

## Installation

### Building from Source

```bash
# Clone repository
git clone https://github.com/TheAnsarya/ableton-snes-spc.git
cd ableton-snes-spc

# Build .NET core library
dotnet build -c Release

# Build VST3 plugin
cd vst3
cmake -B build -DSMTG_ENABLE_VSTGUI_SUPPORT=ON -DENABLE_CUSTOM_VIEWS=OFF
cmake --build build --config Release
```

### Plugin Location

After building, the VST3 plugin is located at:
```
vst3/build/VST3/Release/SnesSpcVst3.vst3/
```

Copy this folder to your VST3 plugins directory.

---

## What's Next (v0.3.0)

- Advanced waveform visualizer with CGraphicsPath
- Real-time spectrum analyzer
- Preset browser with file management
- Drag-and-drop SPC file loading
- Multi-DAW testing and validation

---

## Contributors

See [CONTRIBUTING.md](CONTRIBUTING.md) for how to contribute to this project.

---

## License

MIT License - See [LICENSE](LICENSE) for details.
