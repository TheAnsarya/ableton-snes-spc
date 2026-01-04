# Ableton SNES SPC Plugin - Project Roadmap

## Project Phases

### Phase 1: Foundation (v0.1)
**Goal**: Basic plugin that loads and plays SPC files

- [ ] Project structure and build system
- [ ] Core SPC700 CPU emulation
- [ ] S-DSP audio rendering
- [ ] BRR sample decoding
- [ ] Basic VST3 wrapper
- [ ] Load SPC file and play audio

### Phase 2: Core Features (v0.2)
**Goal**: Functional editing capabilities

- [ ] SPCX project format implementation
- [ ] SPC import with analysis
- [ ] Sample extraction and management
- [ ] Channel mixer UI
- [ ] Solo/mute per channel
- [ ] Parameter automation

### Phase 3: Editor (v0.3)
**Goal**: Full editing environment

- [ ] Sequence parser and editor
- [ ] Piano roll view
- [ ] Sample editor with waveform
- [ ] BRR encoding with quality preview
- [ ] ADSR envelope editor
- [ ] Echo configuration UI

### Phase 4: Export (v0.4)
**Goal**: Create valid SPC output

- [ ] Sequence compiler (MIDI → N-SPC)
- [ ] BRR encoder with optimization
- [ ] Memory layout builder
- [ ] SPC export with validation
- [ ] Preset system

### Phase 5: Polish (v1.0)
**Goal**: Production-ready release

- [ ] Complete documentation
- [ ] Undo/redo system
- [ ] Performance optimization
- [ ] Extensive testing
- [ ] User guide and tutorials

## Technical Milestones

### M1: Build System ✓ (This Session)
- CMake + MSBuild hybrid build
- .NET 10 project structure
- VST3 SDK integration

### M2: Audio Pipeline
- SPC700 CPU executing
- DSP outputting audio
- 32kHz → host sample rate conversion

### M3: UI Framework
- Plugin editor window
- WPF/MAUI integration with VST3
- Basic controls

### M4: File I/O
- SPC parsing complete
- SPCX format implemented
- Import/export working

### M5: Editing
- Sequence modification
- Sample replacement
- Real-time preview of changes

### M6: Integration
- MIDI input support
- Parameter automation
- DAW project save/load

## Timeline Estimate

| Phase | Duration | Target |
|-------|----------|--------|
| Phase 1 | 4-6 weeks | Q1 2026 |
| Phase 2 | 4-6 weeks | Q1 2026 |
| Phase 3 | 6-8 weeks | Q2 2026 |
| Phase 4 | 4-6 weeks | Q2 2026 |
| Phase 5 | 4-6 weeks | Q3 2026 |

## Dependencies

### External
- Steinberg VST3 SDK (GPLv3 or proprietary)
- .NET 10 SDK
- CMake

### Internal (from GameInfo)
- SpcFile parser
- BrrDecoder/BrrEncoder
- NSpcParser
- SequenceDetector

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| VST3 C++/CLI complexity | High | Extensive prototyping first |
| Real-time audio requirements | High | Profile early, optimize |
| Cross-platform UI | Medium | Start with Windows, expand later |
| SPC driver variations | Medium | Focus on N-SPC, document limitations |
| Ableton-specific issues | Low | Test with multiple hosts |
