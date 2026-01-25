# Ableton SNES SPC Plugin - Project Roadmap

**Last Updated**: January 25, 2026  
**Current Version**: 0.4.0 Beta

## Project Phases

### Phase 1: Foundation (v0.1) ✅ COMPLETE

**Goal**: Basic plugin that loads and plays SPC files

- [x] Project structure and build system ✅ (2026-01-05)
- [x] VST3 SDK integration ✅ (2026-01-05)
- [x] Basic VST3 wrapper with VSTGUI ✅ (2026-01-05)
- [x] Custom plugin icon ✅ (2026-01-05)
- [x] Core SPC700 CPU emulation ✅
- [x] S-DSP audio rendering ✅
- [x] BRR sample decoding ✅
- [x] .NET Core integration ✅
- [x] Load SPC file and play audio ✅

### Phase 2: Core Features (v0.2) ✅ COMPLETE

**Goal**: Functional editing capabilities

- [x] Basic VSTGUI editor ✅
- [x] Parameter binding ✅
- [x] Channel mixer controls ✅
- [x] Solo/mute per channel ✅
- [x] Parameter automation ✅

### Phase 3: GUI Components (v0.3) ✅ COMPLETE

**Goal**: Full visual editing environment

- [x] WaveformView - Real-time waveform display ✅
- [x] SpectrumView - FFT frequency analyzer ✅
- [x] PresetBrowser - SPC file browser ✅
- [x] ViewSwitcher - Tab-based panel switching ✅
- [x] KeyboardHandler - Keyboard shortcuts ✅
- [x] MidiLearnHandler - MIDI CC mapping ✅
- [x] VSTGUI 4.x API compatibility fixes ✅

### Phase 4: File Formats (v0.4) ✅ COMPLETE

**Goal**: Full SPC import/export and project format

- [x] SpcFile class with full ID666 support ✅
- [x] ID666 text and binary format handling ✅
- [x] SPCX project format (ZIP/JSON) ✅
- [x] Editor settings persistence ✅
- [x] Driver detection (10+ drivers) ✅
- [x] Analysis caching ✅
- [x] SPC export with metadata ✅

### Phase 5: Polish (v1.0) 🔄 IN PROGRESS

**Goal**: Production-ready release

- [ ] Undo/redo system
- [ ] Advanced sample editor
- [ ] Piano roll sequencer
- [ ] Performance optimization
- [ ] Cross-platform testing (macOS, Linux)
- [ ] Extensive documentation and tutorials

## Technical Milestones

### M1: Build System ✅ (2026-01-05)

- [x] CMake + MSBuild hybrid build
- [x] .NET 10 project structure
- [x] VST3 SDK integration
- [x] Custom SNES-style plugin icon
- [x] VSTGUI support enabled

### M2: Audio Pipeline ✅

- [x] SPC700 CPU executing
- [x] DSP outputting audio
- [x] 32kHz → host sample rate conversion

### M3: UI Framework ✅

- [x] Plugin editor window
- [x] VSTGUI custom views
- [x] Standard and custom controls

### M4: File I/O ✅

- [x] SPC parsing complete with ID666
- [x] SPCX format implemented
- [x] Import/export working

### M5: Editing 🔄

- [x] Voice mute/solo/volume
- [x] MIDI learn
- [ ] Sequence modification
- [ ] Sample replacement
- [ ] Real-time preview of changes

### M6: Integration ✅

- [x] MIDI input support
- [x] Parameter automation
- [x] DAW project save/load

## Timeline

| Phase   | Status      | Completed   |
| ------- | ----------- | ----------- |
| Phase 1 | ✅ Complete | 2026-01-22  |
| Phase 2 | ✅ Complete | 2026-01-24  |
| Phase 3 | ✅ Complete | 2026-01-25  |
| Phase 4 | ✅ Complete | 2026-01-25  |
| Phase 5 | 🔄 Active   | Target: Q2  |

## Dependencies

### External

- Steinberg VST3 SDK (GPLv3 or proprietary)
- .NET 10 SDK
- CMake

### Internal (Implemented)

- `SpcFile` - Parse/export SPC files with ID666 ✅
- `SpcxFile` - Project format (ZIP/JSON) ✅
- `BrrCodec` - BRR encode/decode ✅
- `SpcAnalyzer` - Detect 10+ sound drivers ✅

## Risk Assessment

| Risk                         | Impact | Status     | Notes                        |
| ---------------------------- | ------ | ---------- | ---------------------------- |
| VST3 C++/CLI complexity      | High   | ✅ Resolved | Extensive testing done       |
| Real-time audio requirements | High   | ✅ Resolved | Performance verified         |
| VSTGUI 4.x API changes       | Medium | ✅ Resolved | All custom views working     |
| SPC driver variations        | Medium | ✅ Resolved | 10+ drivers detected         |
| Cross-platform support       | Low    | 🔄 Pending | Windows working, others TBD  |
