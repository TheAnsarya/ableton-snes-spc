# Changelog

All notable changes to the SNES SPC VST3 Plugin will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Planned
- SPCX project format support
- Advanced sample editor with waveform display
- Piano roll sequence editor
- Real-time parameter automation recording
- Echo/reverb visual editor
- Preset system with bank management

## [0.3.0] - 2026-01-25

### Added

#### Custom View Components
- **WaveformView**: Real-time audio waveform visualization
  - Time-domain waveform display
  - BRR sample block visualization mode
  - Zoom and scroll support
  - Selection highlighting for loop points
  - Configurable colors (background, waveform, grid)

- **SpectrumView**: FFT-based frequency spectrum analyzer
  - 32-band frequency visualization
  - Peak hold indicators
  - Logarithmic scale option
  - Configurable decay rate and smoothing
  - Cooley-Tukey radix-2 FFT implementation

- **PresetBrowser**: SPC file browser panel
  - Directory scanning for .spc files
  - Search/filter functionality
  - Sorting by name, game, artist
  - Double-click to load presets
  - Scrollbar for large lists

- **ViewSwitcher**: Panel-switching container
  - Tab-like view management
  - Parameter-driven view selection

- **KeyboardHandler**: Keyboard shortcuts
  - Space for Play/Pause
  - Escape for Stop
  - Arrow keys for volume control
  - Customizable key bindings

- **MidiLearnHandler**: MIDI CC mapping system
  - MIDI learn mode for parameter assignment
  - Save/load mapping presets
  - Multi-channel support

### Changed
- Full VSTGUI 4.x API compatibility
  - Updated IKeyboardHook interface signature
  - Replaced deprecated moveTo/lineTo with drawLine
  - Updated onWheel to onMouseWheelEvent
  - Fixed UIViewFactory registration includes
  - Resolved template instantiation for non-copyable classes

### Fixed
- Circular dependency between spc_controller.h and midi_learn.h
- Missing include for IControlListener interface
- CColor type definition in view_switcher.cpp
- UIAttributes incomplete type in view factories
- MouseWheelEvent incomplete type in preset_browser

### Technical Notes
- Build with `-DENABLE_CUSTOM_VIEWS=ON` (default) for full GUI
- Build with `-DENABLE_CUSTOM_VIEWS=OFF` for minimal GUI without custom views

## [0.2.0] - 2026-01-24

### Added

#### GUI Framework
- Basic VSTGUI integration for plugin editor window
- UI description file (spc_editor.uidesc) with layout definitions
- Parameter binding infrastructure for real-time updates
- Support for knobs, sliders, buttons, and text displays
- Color scheme and font definitions for consistent styling

#### Technical Improvements
- Fixed VSTGUI API compatibility with SDK 4.x
- Improved build configuration with optional custom views
- Enhanced CMake build system with ENABLE_CUSTOM_VIEWS option

### Changed
- Updated build documentation with GUI build options
- Improved VST3 controller with better parameter synchronization

### Fixed
- VSTGUI header include paths for ViewCreatorAdapter
- UIViewCreator attribute namespace resolution

### Known Issues
- Custom view components (WaveformView, SpectrumView, PresetBrowser) require VSTGUI API updates
- Advanced visualizers deferred to v0.3.0

## [0.1.0] - 2026-01-24

### Added

#### Core Emulation
- Complete SPC700 CPU emulator with all 256 instructions
- S-DSP (Digital Signal Processor) emulation
  - 8-voice stereo audio output
  - Hardware-accurate BRR sample decompression
  - ADSR and GAIN envelope support
  - Echo/reverb effect processing
  - Noise generator
  - Pitch modulation
  - FIR filter (8-tap)
- BRR codec (decoder and encoder)
  - Automatic filter selection
  - Loop point support

#### Audio Engine
- Real-time SPC playback at native 32 kHz
- Sample rate conversion (resampling to any DAW sample rate)
- Per-voice mute/solo/volume controls
- Master volume control
- Loop enable/disable
- Seek functionality
- Waveform capture for visualization

#### Editing Capabilities
- Voice parameter editing (volume, pitch, ADSR, GAIN)
- DSP register access (echo, filters, main volume)
- Sample directory editing
- BRR sample decode/encode
- Voice key-on/key-off triggers
- RAM snapshot export

#### MIDI Support
- Note-on triggers voice playback
- Note-off triggers voice release
- Velocity controls voice volume
- 8 voices mapped to consecutive MIDI notes (C3-G3)
- CC learn functionality
- CC parameter mapping

#### VST3 Plugin
- Full VST3 SDK integration
- Stereo audio output
- 35+ automatable parameters
  - Master volume, play/pause, loop
  - 8 voice enable/mute switches
  - 8 voice solo switches
  - 8 voice volume controls
- DAW transport sync
- Tempo and time signature awareness
- File loading (drag-and-drop planned)
- Native AOT deployment option

#### Project Management
- SPC file import (.spc format)
- ID666 metadata support
- Project state serialization (JSON)
- Preset save/load (basic)
- Sample export to WAV

#### Developer Features
- Clean C# / C++ interop via UnmanagedCallersOnly
- Comprehensive unit tests
  - BRR codec tests
  - Emulation accuracy tests
  - MIDI processing tests
  - Preset system tests
  - Sample effects tests
- Full API documentation
- Cross-platform support (Windows, macOS, Linux)

#### Build System
- CMake configuration for VST3
- .NET 10 build with Native AOT option
- Automated build scripts (PowerShell)
- VST3 SDK setup automation
- Icon generation tooling

#### Documentation
- README with project vision
- API Reference (complete C# API)
- User Guide (installation and usage)
- Building Guide (all platforms)
- Contributing Guidelines
- Technical Architecture Documentation
- SPC File Format Documentation
- Session logs (development history)

### Technical Details
- **Languages**: C# (.NET 10), C++ (C++20)
- **Frameworks**: VST3 SDK 3.7+
- **Sample Rate**: Native 32 kHz, resampled to host rate
- **Channels**: Stereo output
- **Latency**: Low latency, real-time safe
- **Memory**: ~100KB RAM + sample data
- **CPU**: Optimized emulation loop

### Known Limitations
- No GUI (parameters via DAW automation only)
- No advanced waveform visualizer yet
- No built-in file browser
- Limited echo/reverb customization in UI
- Seek operation is expensive (full re-emulation)
- No undo/redo for editing operations

---

## Version History

### [0.1.0] - 2026-01-24
- Initial release
- Core emulation and VST3 plugin functional
- Ready for alpha testing

---

## Upcoming Releases

### [0.2.0] - Planned
**Focus**: GUI and User Experience

- Native VST3 GUI with VSTGUI
- Waveform visualizer
- Sample browser
- Voice matrix view
- File drag-and-drop
- Parameter tooltips
- Visual feedback for automation

### [0.3.0] - Planned
**Focus**: Advanced Editing

- SPCX project format
- Undo/redo system
- Advanced sample editor
- Loop point editor
- Envelope visualizer
- Copy/paste for voices

### [0.4.0] - Planned
**Focus**: Composition Tools

- Piano roll sequencer
- Pattern editor
- Step sequencer mode
- Note recording from MIDI
- Quantization

### [1.0.0] - Planned
**Focus**: Production Ready

- Complete feature set
- Comprehensive testing
- Performance optimization
- Full documentation
- Installer packages
- Production-quality GUI
- Preset library
- Example SPC files

---

## Development Milestones

- ✅ 2026-01-04: Project initialization, C++ VST3 skeleton
- ✅ 2026-01-05: .NET interop working, basic audio output
- ✅ 2026-01-05: SPC700 CPU emulation complete
- ✅ 2026-01-05: S-DSP audio rendering complete
- ✅ 2026-01-05: VST3 build system complete
- ✅ 2026-01-05: Plugin icon designed and integrated
- ✅ 2026-01-24: Documentation complete, ready for 0.1.0 release

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on contributing to this project.

## License

This project is licensed under the MIT License - see the LICENSE file for details.
