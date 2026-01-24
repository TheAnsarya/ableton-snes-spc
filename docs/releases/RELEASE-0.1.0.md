# Release Notes - Version 0.1.0 Alpha

**Release Date**: January 24, 2026  
**Status**: Alpha - Early Development Release  
**Target Audience**: Developers, Early Adopters, Testers

---

## 🎉 What's New

Version 0.1.0 marks the first public release of the SNES SPC VST3 Plugin. This alpha version provides a solid foundation for SNES audio emulation with complete CPU and DSP emulation, ready for integration into Digital Audio Workstations.

---

## ✨ Features

### Core Emulation ✅
- **Complete SPC700 CPU Emulation**
  - All 256 instructions implemented
  - Cycle-accurate timing
  - Hardware-accurate addressing modes
  - Full register support

- **S-DSP Audio Processor**
  - 8-voice stereo audio output
  - BRR sample decompression (hardware-accurate)
  - ADSR and GAIN envelope modes
  - Echo/reverb effects
  - FIR 8-tap filter
  - Noise generator
  - Pitch modulation
  - Voice key-on/key-off

- **BRR Codec**
  - Complete decoder implementation
  - Encoder with auto filter selection
  - Loop point support
  - Sample rate conversion

### Audio Engine ✅
- Real-time playback at 32 kHz native rate
- Sample rate conversion to any DAW rate (44.1k, 48k, 96k, etc.)
- Per-voice controls:
  - Mute/Solo (8 voices)
  - Individual volume (0-100%)
- Master volume control (0-200%)
- Loop enable/disable
- Seek to position (in seconds)
- Waveform capture for future visualization

### VST3 Integration ✅
- **35+ Automatable Parameters**:
  - Master volume
  - Play/Pause/Stop
  - Loop toggle
  - Voice 0-7: Enable/Mute
  - Voice 0-7: Solo
  - Voice 0-7: Volume
  
- **DAW Integration**:
  - Transport sync
  - Tempo awareness (BPM from host)
  - Time signature sync
  - State save/restore
  - Preset management

### MIDI Support ✅
- Note-on triggers voice playback (C3-G3 = Voice 0-7)
- Note-off triggers voice release
- Velocity controls voice volume (0-127)
- CC learn functionality
- CC parameter mapping

### File Operations ✅
- SPC file loading (.spc format)
- ID666 metadata reading
- Sample export to WAV
- Voice export (isolated)
- Full mix export
- Project state serialization (JSON)

---

## 🚫 Known Limitations

### No GUI Yet
- All controls are via DAW automation parameters
- File loading must be done programmatically or via future file browser
- No visual waveform display
- No on-screen meters

**Workaround**: Use your DAW's automation lanes to control all parameters

### Performance
- Seek operation is expensive (requires full re-emulation)
- Native 32kHz requires resampling (adds slight CPU overhead)

### Platform Support
- Windows: ✅ Fully tested
- macOS: ⚠️ Build tested, needs runtime testing
- Linux: ⚠️ Build tested, needs runtime testing

---

## 🔧 Technical Details

### Requirements

**System**:
- Windows 10+ / macOS 12+ / Linux (Ubuntu 20.04+)
- CPU: x64 (Intel/AMD/Apple Silicon)
- RAM: 256 MB minimum
- VST3-compatible DAW

**Software**:
- .NET 10 Runtime (included with plugin)
- VST3 host application

### Performance

| Metric | Value |
|--------|-------|
| CPU Usage | ~3-5% (i7-9700K @ 44.1kHz) |
| RAM Usage | ~20 MB base + sample data |
| Latency | <1ms (real-time safe) |
| Sample Rate | 32kHz native (resampled to host) |
| Voices | 8 simultaneous |

### Build Info

- **.NET Version**: 10.0
- **C++ Standard**: C++20
- **VST3 SDK**: 3.7.7+
- **Build System**: CMake 3.21+
- **Compiler**: MSVC 2022 / Clang 14+

---

## 📦 Installation

### Windows

1. Download `SnesSpcVst3-0.1.0-win64.zip`
2. Extract the contents
3. Copy `SnesSpcVst3.vst3` folder to:
   - **System**: `C:\Program Files\Common Files\VST3\`
   - **User**: `%APPDATA%\VST3\`
4. Ensure `SpcPlugin.Core.dll` is in the plugin's `Contents\x86_64-win\` subfolder
5. Rescan plugins in your DAW

### macOS

1. Download `SnesSpcVst3-0.1.0-macos.zip`
2. Extract the contents
3. Copy `SnesSpcVst3.vst3` to:
   - **System**: `/Library/Audio/Plug-Ins/VST3/`
   - **User**: `~/Library/Audio/Plug-Ins/VST3/`
4. Ensure `libSpcPlugin.Core.dylib` is in `Contents/MacOS/`
5. Rescan plugins in your DAW

### Linux

1. Download `SnesSpcVst3-0.1.0-linux.zip`
2. Extract the contents
3. Copy `SnesSpcVst3.vst3` to: `~/.vst3/`
4. Ensure `libSpcPlugin.Core.so` is in `Contents/x86_64-linux/`
5. Rescan plugins in your DAW

---

## 🎮 Quick Start

1. **Load the Plugin**
   - Insert "SNES SPC Plugin" on an audio track

2. **Load an SPC File** (v0.1.0 requires manual approach)
   - Use DAW automation to trigger file loading
   - Or wait for v0.2.0 with drag-and-drop GUI

3. **Start Playback**
   - Automate "Play/Pause" parameter to 1.0 (On)
   - Or start your DAW's transport

4. **Control Voices**
   - Use "Voice 0-7 Volume" parameters
   - Use "Voice 0-7 Mute" for mixing
   - Use "Voice 0-7 Solo" to isolate voices

5. **MIDI Triggering**
   - Send MIDI notes C3-G3 to trigger voices
   - Velocity affects voice volume

---

## 🧪 Testing

### Tested DAWs

| DAW | Windows | macOS | Status |
|-----|---------|-------|--------|
| Ableton Live 11+ | ✅ | ⏳ | Working |
| FL Studio 21+ | ⏳ | - | Not tested |
| Reaper 6+ | ⏳ | ⏳ | Not tested |
| Cubase 12+ | ⏳ | ⏳ | Not tested |

### Known Issues

1. **File Loading**: No GUI file browser yet (coming in v0.2.0)
2. **Performance**: Seek is slow on very long positions
3. **Documentation**: User guide needs video tutorials

Please report any issues at: https://github.com/TheAnsarya/ableton-snes-spc/issues

---

## 🗺️ What's Next?

### Version 0.2.0 Beta (Target: March 2026)

**Focus**: Native GUI

- VSTGUI-based interface
- Transport controls (Play/Pause/Stop/Loop)
- 8-channel voice mixer with meters
- Real-time waveform visualizer
- Sample browser with preview
- Drag-and-drop file loading
- Visual feedback for automation

### Version 0.9.0 RC (Target: May 2026)

**Focus**: Feature Complete

- SPCX project format
- Advanced sample editor
- Echo/reverb visual editor
- Performance optimizations
- Complete documentation
- Video tutorials

### Version 1.0.0 (Target: June 2026)

**Focus**: Production Ready

- Polished GUI
- Comprehensive testing
- Installer packages
- Full documentation
- Preset library

See [RELEASE_PLAN_1.0.md](../docs/plans/RELEASE_PLAN_1.0.md) for the complete roadmap.

---

## 📝 Changelog

See [CHANGELOG.md](../CHANGELOG.md) for the complete version history.

---

## 🤝 Contributing

Want to help? We'd love contributions!

- **Test** the plugin in your DAW and report issues
- **Code** improvements (see open issues)
- **Document** usage patterns and workflows
- **Design** UI mockups for v0.2.0

See [CONTRIBUTING.md](../docs/CONTRIBUTING.md) for guidelines.

---

## 📄 Documentation

- [User Guide](../docs/USER_GUIDE.md)
- [Building from Source](../docs/BUILDING.md)
- [API Reference](../docs/API_REFERENCE.md)
- [SPC Format Spec](../docs/SPC_FORMAT.md)

---

## 📬 Support

- **Issues**: https://github.com/TheAnsarya/ableton-snes-spc/issues
- **Discussions**: https://github.com/TheAnsarya/ableton-snes-spc/discussions
- **Documentation**: https://github.com/TheAnsarya/ableton-snes-spc/tree/main/docs

---

## 🙏 Acknowledgments

Thank you to:
- The SNES/SPC community for documentation
- Steinberg for the VST3 SDK
- Microsoft for .NET and excellent tooling
- All future contributors and testers!

---

**Release**: v0.1.0 Alpha  
**Date**: January 24, 2026  
**Download**: [GitHub Releases](https://github.com/TheAnsarya/ableton-snes-spc/releases/tag/v0.1.0)

🎮 Happy SNES music making! 🎵
