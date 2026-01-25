# GitHub Issues for Release Planning

**Last Updated**: January 25, 2026  
**Current Status**: v0.4.0 Released - GUI and file formats complete

## Milestones

### Milestone: v0.2.0 Beta - GUI Development ✅ COMPLETE
**Completed**: January 24, 2026  
**Description**: Basic VSTGUI integration with parameter binding

### Milestone: v0.3.0 - Custom Views ✅ COMPLETE
**Completed**: January 25, 2026  
**Description**: Advanced GUI components including WaveformView, SpectrumView, PresetBrowser

### Milestone: v0.4.0 - File Formats ✅ COMPLETE
**Completed**: January 25, 2026  
**Description**: Full SPC import/export with ID666 tags, SPCX project format, driver detection

### Milestone: v1.0.0 - Production Release 🔄 IN PROGRESS
**Target**: Q2 2026  
**Description**: Undo/redo, advanced editing, cross-platform testing

---

## Completed Epic Issues

### Epic #1: VSTGUI Integration ✅ COMPLETE
**Status**: Implemented in v0.2.0-v0.3.0

Completed features:
- [x] VSTGUI framework integrated into CMake build ✅
- [x] Base editor window created ✅
- [x] Resource management (images, fonts) ✅
- [x] Custom VSTGUI views (WaveformView, SpectrumView, PresetBrowser) ✅
- [x] HiDPI support ✅

### Epic #2: File Format Support ✅ COMPLETE
**Status**: Implemented in v0.4.0

Completed features:
- [x] SpcFile class with full ID666 support ✅
- [x] ID666 text and binary format handling ✅
- [x] SPCX project format (ZIP/JSON) ✅
- [x] Editor settings persistence ✅
- [x] Driver detection (10+ drivers) ✅

---

## Completed Feature Issues

### Feature #2: Transport Controls UI ✅
**Completed**: v0.2.0

### Feature #3: Voice Mixer View ✅
**Completed**: v0.2.0

### Feature #4: Waveform Visualizer ✅
**Completed**: v0.3.0 (WaveformView)

### Feature #5: Preset Browser ✅
**Completed**: v0.3.0 (PresetBrowser with search/filter/sort)

### Feature #6: MIDI Learn ✅
**Completed**: v0.3.0 (MidiLearnHandler)

### Feature #7: Keyboard Shortcuts ✅
**Completed**: v0.3.0 (KeyboardHandler)

### Feature #26: SPCX Project Format ✅
**Completed**: v0.4.0

### Feature #27: SPC Import ✅
**Completed**: v0.4.0

### Feature #28: SPC Export ✅
**Completed**: v0.4.0

---

## Remaining Issues (v1.0.0)

### Feature: Undo/Redo System
**Labels**: feature, v1.0.0  
**Status**: Not started

**Description**:
Implement command pattern for undo/redo support.

### Feature: Advanced Sample Editor
**Labels**: feature, v1.0.0  
**Status**: Not started

**Description**:
Waveform editing, BRR re-encoding, loop point editing.

### Feature: Piano Roll Sequencer
**Labels**: feature, v1.0.0  
**Status**: Not started

**Description**:
Visual sequence editor with MIDI-like piano roll interface.

### Feature: Cross-Platform Support
**Labels**: enhancement, v1.0.0  
**Status**: Not started

**Description**:
Test and verify macOS and Linux builds.
│ │  │ │  │ │  │ │  │ │  │ │  │ │  │ │  │
│ ○  │ ○  │ ○  │ ○  │ ○  │ ○  │ ○  │ ○  │ Vol
│ │  │ │  │ │  │ │  │ │  │ │  │ │  │ │  │
└────┴────┴────┴────┴────┴────┴────┴────┘
```

**Technical Notes**:
- Update meters in real-time from audio thread
- Thread-safe data transfer
- Custom VSTGUI controls for meters
- Parameter binding for automation

**Estimated Effort**: 1 week

---

### Feature #4: Waveform Visualizer
**Title**: Real-time Stereo Waveform Display  
**Labels**: feature, gui, v0.2.0  
**Milestone**: v0.2.0 Beta  
**Epic**: #1

**Description**:
Display real-time audio waveform for visual feedback during playback.

**Acceptance Criteria**:
- [ ] Stereo waveform display (left + right channels)
- [ ] Real-time updates (smooth scrolling or circular buffer)
- [ ] Zoom controls (time scale)
- [ ] Peak hold indicators
- [ ] Per-voice isolation mode
- [ ] Efficient rendering (no audio thread impact)

**UI Mockup**:
```
┌─────────────────────────────────────────┐
│        Waveform Display                 │
│  L: ╱╲╱╲╱╲╱╲╱╲╱╲╱╲╱╲╱╲╱╲╱╲╱╲╱╲           │
│  R: ╲╱╲╱╲╱╲╱╲╱╲╱╲╱╲╱╲╱╲╱╲╱╲╱╲╱           │
│                                         │
│  [Zoom: 100ms] [Voice: All ▾]          │
└─────────────────────────────────────────┘
```

**Technical Notes**:
- Use existing waveform capture from SpcEngine
- Lock-free ringbuffer for audio→UI transfer
- OpenGL or native drawing for performance
- 60fps target refresh rate

**Estimated Effort**: 1 week

---

### Feature #5: Sample Browser
**Title**: Sample Browser and Exporter  
**Labels**: feature, gui, v0.2.0  
**Milestone**: v0.2.0 Beta  
**Epic**: #1

**Description**:
Browse and manage BRR samples in the loaded SPC file.

**Acceptance Criteria**:
- [ ] List view of all samples (0-255)
- [ ] Sample info (start address, loop, length)
- [ ] Preview playback button
- [ ] Export to WAV button (per-sample)
- [ ] Export all samples button
- [ ] Waveform thumbnail preview
- [ ] Active indicator (which voices using this sample)

**UI Mockup**:
```
┌─────────────────────────────────────┐
│  Sample Browser                     │
├──────┬────────┬────────┬────────────┤
│ # 00 │ 1.2KB  │ [Loop] │ [▶] [💾]  │
│ ───╱─╲─╱─╲─── │        │           │
│ # 01 │ 2.4KB  │ [Loop] │ [▶] [💾]  │
│ ───╲╱──╲╱──── │        │           │
│ # 02 │ 512B   │        │ [▶] [💾]  │
│ ───────────── │        │           │
│  ...                                │
│                                     │
│ [Export All Samples]                │
└─────────────────────────────────────┘
```

**Technical Notes**:
- Decode BRR on-demand for preview
- Async file writing for exports
- Progress indicator for bulk export
- Filter to show only used samples

**Estimated Effort**: 1 week

---

### Feature #6: File Drag-and-Drop
**Title**: Drag-and-Drop SPC File Loading  
**Labels**: feature, gui, enhancement, v0.2.0  
**Milestone**: v0.2.0 Beta  
**Epic**: #1

**Description**:
Allow users to drag .spc files directly onto the plugin window to load them.

**Acceptance Criteria**:
- [ ] Drop target on main window
- [ ] Visual feedback on hover
- [ ] File validation (.spc extension)
- [ ] Error handling (invalid files)
- [ ] Multi-file drop (load first, ignore rest)

**Technical Notes**:
- Implement `IDataPackage` for VST3
- Cross-platform file path handling
- UI feedback during load

**Estimated Effort**: 2 days

---

## Testing Issues

### Task #7: Multi-DAW Compatibility Testing
**Title**: Test plugin in multiple DAWs  
**Labels**: testing, v0.2.0  
**Milestone**: v0.2.0 Beta

**Description**:
Comprehensive testing across different DAW environments.

**Test Matrix**:

| DAW | Windows | macOS | Linux | Status |
|-----|---------|-------|-------|--------|
| Ableton Live 11+ | [ ] | [ ] | [ ] | |
| FL Studio 21+ | [ ] | - | - | |
| Reaper 6+ | [ ] | [ ] | [ ] | |
| Cubase 12+ | [ ] | [ ] | - | |
| Logic Pro X | - | [ ] | - | |
| Studio One 6+ | [ ] | [ ] | - | |
| Bitwig Studio | [ ] | [ ] | [ ] | |

**Test Cases**:
- [ ] Plugin loads without errors
- [ ] SPC file loading works
- [ ] Playback starts/stops correctly
- [ ] Parameter automation works
- [ ] MIDI input works
- [ ] Audio output is clean (no dropouts)
- [ ] UI responds correctly
- [ ] CPU usage is acceptable
- [ ] Save/recall state works
- [ ] Preset save/load works

**Estimated Effort**: 2 weeks

---

### Task #8: Create Video Tutorials
**Title**: Video Tutorial Series for v0.2.0  
**Labels**: documentation, video, v0.2.0  
**Milestone**: v0.2.0 Beta

**Description**:
Create video tutorials for new users.

**Videos**:
1. **Quick Start** (3-5 min)
   - Installing the plugin
   - Loading an SPC file
   - Basic playback

2. **Voice Mixer** (5-7 min)
   - Understanding the 8 voices
   - Mute/solo/volume controls
   - Voice information display

3. **Sample Browser** (5-7 min)
   - Browsing samples
   - Previewing samples
   - Exporting to WAV

4. **Advanced Usage** (10-15 min)
   - MIDI triggering
   - Automation in Ableton
   - Echo/reverb settings
   - Workflow tips

**Deliverables**:
- [ ] Screen recordings
- [ ] Voice-over narration
- [ ] Edited videos (1080p)
- [ ] Upload to YouTube
- [ ] Embed in documentation

**Estimated Effort**: 1 week

---

## Bug Tracking Template

### Bug Template
**Title**: [BUG] Brief description  
**Labels**: bug  
**Priority**: High / Medium / Low

**Describe the bug**:
A clear description of the bug.

**To Reproduce**:
1. Step 1
2. Step 2
3. See error

**Expected behavior**:
What should happen.

**Actual behavior**:
What actually happens.

**Environment**:
- OS: Windows 10 / macOS 13 / Ubuntu 22.04
- DAW: Ableton Live 11.3
- Plugin Version: 0.2.0-beta
- .NET Version: 10.0

**Screenshots**:
If applicable.

**SPC File**:
Attach if issue is file-specific.

---

## Release Checklist Issues

### Task #9: v0.1.0 Alpha Release
**Title**: Create v0.1.0 Alpha Release  
**Labels**: release  
**Milestone**: v0.1.0

**Checklist**:
- [x] All core features complete
- [x] Build succeeds on all platforms
- [x] Documentation complete
- [ ] Create GitHub release tag `v0.1.0`
- [ ] Build release binaries
- [ ] Package installer (optional for alpha)
- [ ] Write release notes
- [ ] Upload binaries to GitHub
- [ ] Announce on social media
- [ ] Update README with download link

---

### Task #10: v0.2.0 Beta Release
**Title**: Create v0.2.0 Beta Release  
**Labels**: release  
**Milestone**: v0.2.0

**Checklist**:
- [ ] All v0.2.0 features complete
- [ ] GUI fully functional
- [ ] Multi-DAW testing passed
- [ ] Video tutorials created
- [ ] Beta tester feedback incorporated
- [ ] Create GitHub release tag `v0.2.0-beta`
- [ ] Build release binaries
- [ ] Package installer
- [ ] Update CHANGELOG.md
- [ ] Write release notes
- [ ] Upload binaries
- [ ] Announce beta program

**Target Date**: March 31, 2026

---

## Next Steps

1. Create GitHub milestones for v0.1.0, v0.2.0, v0.9.0, v1.0.0
2. Create issues from this template
3. Assign labels and priorities
4. Begin development on v0.2.0 GUI work
5. Set up project board for tracking

---

*This document serves as a template for creating actual GitHub issues. Each issue should be created individually with proper labels, milestones, and assignments.*
