# SNES SPC VST3 Plugin - Manual Testing Guide

## Prerequisites

Before testing, ensure the following are complete:

1. **VST3 SDK installed** - Download from [Steinberg Developer Portal](https://www.steinberg.net/developers/)
2. **Plugin built** - Run `.\build-vst3.ps1 -Release -Install`
3. **Ableton Live installed** - Version 10.1.43+ recommended
4. **Sample SPC files** - For testing playback functionality

## Test Environment Setup

### Windows Installation Paths

| Component | Path |
|-----------|------|
| VST3 Plugin | `C:\Program Files\Common Files\VST3\SnesSpcVst3.vst3` |
| .NET Runtime | Bundled with plugin (Native AOT) or system-installed |
| Ableton Live | `C:\ProgramData\Ableton\Live 10 Suite\Program\` |

### Quick Install Script

```powershell
# Build and install in one command
.\build-vst3.ps1 -Release -Install -OpenAbleton
```

## Test Cases

### TC-001: Plugin Loading

**Objective:** Verify plugin loads correctly in Ableton Live

**Steps:**
1. Open Ableton Live
2. Go to Options > Preferences > Plug-ins
3. Ensure "Use VST3 plug-in System folders" is enabled
4. Click "Rescan plug-ins"
5. Navigate to Browser > Plug-ins > VST3
6. Locate "SNES SPC Player"

**Expected Result:**
- Plugin appears in the VST3 list
- No error dialogs during scan
- Plugin icon visible (if provided)

**Pass/Fail:** [ ]

---

### TC-002: Plugin Instantiation

**Objective:** Verify plugin can be added to a track

**Steps:**
1. Create new MIDI track
2. Double-click "SNES SPC Player" in browser
3. Or drag plugin to track device area

**Expected Result:**
- Plugin window opens
- GUI displays correctly (800x500 default size)
- No audio glitches or crashes
- CPU usage remains reasonable (<5% idle)

**Pass/Fail:** [ ]

---

### TC-003: SPC File Loading (Drag & Drop)

**Objective:** Verify SPC files can be loaded via drag and drop

**Steps:**
1. Open plugin GUI
2. Locate test SPC file in Windows Explorer
3. Drag file onto plugin's drop zone (right side of waveform area)
4. Release mouse button

**Expected Result:**
- Drop zone highlights during drag
- File path accepted
- Waveform updates to show loaded audio
- File name displayed in UI (if implemented)

**Pass/Fail:** [ ]

---

### TC-004: Playback Controls

**Objective:** Verify transport controls function correctly

**Steps:**
1. Load an SPC file
2. Click Play button
3. Verify audio output
4. Click Pause button
5. Click Play again
6. Enable Loop toggle
7. Wait for track to loop

**Expected Result:**
- Audio plays when Play is clicked
- Audio stops when Pause is clicked
- Audio resumes from paused position
- Track loops seamlessly when Loop enabled

**Pass/Fail:** [ ]

---

### TC-005: Channel Mixer

**Objective:** Verify per-channel volume and mute/solo

**Steps:**
1. Load an SPC file with multiple channels
2. Play the track
3. Move CH 1 volume slider down
4. Move CH 1 volume slider back up
5. Click CH 1 Mute button
6. Unmute CH 1
7. Click CH 1 Solo button
8. Unsolo CH 1

**Expected Result:**
- Volume changes affect channel output
- Mute silences the channel
- Solo silences all other channels
- Visual feedback on buttons reflects state

**Pass/Fail:** [ ]

---

### TC-006: Master Volume

**Objective:** Verify master volume control

**Steps:**
1. Load and play an SPC file
2. Move master volume slider to minimum
3. Move to maximum
4. Move to 50%

**Expected Result:**
- Volume scales proportionally
- No clipping at maximum (unless source clips)
- Complete silence at minimum

**Pass/Fail:** [ ]

---

### TC-007: Waveform Display

**Objective:** Verify waveform visualization

**Steps:**
1. Load an SPC file
2. Play the track
3. Observe waveform display

**Expected Result:**
- Waveform shows left/right channels
- Updates in real-time during playback
- Clear visualization of audio amplitude
- No visual artifacts or tearing

**Pass/Fail:** [ ]

---

### TC-008: Spectrum Analyzer

**Objective:** Verify frequency spectrum display

**Steps:**
1. Load an SPC file
2. Play the track
3. Observe spectrum analyzer panel
4. Note response to different frequency content

**Expected Result:**
- Frequency bands update in real-time
- Peak hold indicators visible
- Logarithmic frequency distribution
- dB scale accurate

**Pass/Fail:** [ ]

---

### TC-009: State Persistence (Session Save/Load)

**Objective:** Verify plugin state saves with Ableton project

**Steps:**
1. Load an SPC file
2. Adjust mixer settings (volumes, mutes, solos)
3. Save Ableton project (Ctrl+S)
4. Close Ableton
5. Reopen project

**Expected Result:**
- SPC file still loaded
- All mixer settings restored
- Transport state appropriate (not auto-playing)
- GUI state matches saved settings

**Pass/Fail:** [ ]

---

### TC-010: Parameter Automation

**Objective:** Verify parameters can be automated in Ableton

**Steps:**
1. Load an SPC file
2. Click Configure on plugin device
3. Move Master Volume slider
4. Create automation envelope for Master Volume
5. Draw automation curve
6. Play project and observe volume changes

**Expected Result:**
- Parameter appears in automation chooser
- Automation envelope controls parameter
- Smooth parameter changes (no stepping)
- Automation reads correctly on playback

**Pass/Fail:** [ ]

---

### TC-011: MIDI Note Triggering

**Objective:** Verify MIDI notes trigger samples (if implemented)

**Steps:**
1. Load an SPC file
2. Create MIDI clip on the track
3. Add MIDI notes (C3, D3, E3, etc.)
4. Play the clip

**Expected Result:**
- MIDI notes trigger sample playback
- Different pitches play different notes
- Velocity affects volume
- Note-off stops sample (or ADSR release)

**Pass/Fail:** [ ]

---

### TC-012: CPU Performance

**Objective:** Measure CPU usage under load

**Steps:**
1. Load complex SPC file (8 voices active)
2. Set Ableton buffer size to 128 samples
3. Play track for 60 seconds
4. Monitor CPU meter in Ableton
5. Monitor Windows Task Manager

**Expected Result:**
- CPU usage stays under 10% on modern hardware
- No audio dropouts or glitches
- Stable performance over time
- No memory leaks visible

**Pass/Fail:** [ ]

---

### TC-013: Sample Rate Switching

**Objective:** Verify plugin handles sample rate changes

**Steps:**
1. Load plugin with SPC file
2. Open Audio preferences
3. Change sample rate (44100 → 48000)
4. Confirm change and observe plugin
5. Play audio

**Expected Result:**
- Plugin handles sample rate change gracefully
- Audio plays at correct pitch
- No crashes or errors
- Performance remains stable

**Pass/Fail:** [ ]

---

### TC-014: Multiple Instances

**Objective:** Verify multiple plugin instances work simultaneously

**Steps:**
1. Create 3 MIDI tracks
2. Add SNES SPC Player to each track
3. Load different SPC files on each
4. Play all tracks simultaneously

**Expected Result:**
- All instances play independently
- No audio artifacts from conflicts
- CPU scales reasonably with instances
- Each instance maintains own state

**Pass/Fail:** [ ]

---

### TC-015: Preset Browser (if implemented)

**Objective:** Verify preset/file browser functionality

**Steps:**
1. Open plugin GUI
2. Access preset browser panel
3. Navigate to folder with SPC files
4. Click on SPC file to load
5. Add file to favorites
6. Close and reopen plugin
7. Check favorites list

**Expected Result:**
- Browser displays SPC files
- Files load when clicked
- Favorites persist across sessions
- Scrollbar works for large lists

**Pass/Fail:** [ ]

---

### TC-016: Keyboard Shortcuts

**Objective:** Verify keyboard shortcuts work in plugin GUI

**Steps:**
1. Load an SPC file
2. Click inside the plugin GUI to give it focus
3. Press Space to toggle Play/Pause
4. Press L to toggle Loop
5. Press Escape to Stop
6. Press Up Arrow to increase volume
7. Press Down Arrow to decrease volume
8. Press 1-8 to toggle individual voice mute
9. Press M to mute all voices
10. Press N to unmute all voices
11. Press Home to jump to start

**Expected Result:**
- Space toggles playback state
- L toggles loop on/off
- Escape stops playback and resets position
- Arrow keys adjust master volume by 5%
- Number keys 1-8 toggle corresponding voice
- M mutes all 8 voices
- N unmutes all voices and disables solos
- Home resets position to beginning

**Pass/Fail:** [ ]

---

### TC-017: View Mode Switching

**Objective:** Verify tab-based view switching works

**Steps:**
1. Open plugin GUI
2. Click "Mixer" tab
3. Verify Mixer panel is visible
4. Click "Samples" tab
5. Verify Sample Editor panel is visible
6. Click "Browser" tab
7. Verify Preset Browser panel is visible
8. Switch between tabs multiple times

**Expected Result:**
- Only one panel visible at a time
- Transitions are instant (no delay)
- Active tab is visually highlighted
- Panel content is preserved when switching back

**Pass/Fail:** [ ]

---

### TC-018: Sample Editor Controls

**Objective:** Verify sample editor parameter controls

**Steps:**
1. Load an SPC file
2. Switch to Samples view
3. Select a sample using the Sample selector (0-127)
4. Adjust Pitch slider (-24 to +24 semitones)
5. Adjust Volume slider (0-100%)
6. Adjust Attack rate (0-15)
7. Adjust Decay rate (0-7)
8. Adjust Sustain level (0-7)
9. Adjust Release rate (0-31)
10. Click Trigger button

**Expected Result:**
- Sample selection changes active sample
- Pitch affects playback frequency
- Volume affects sample loudness
- ADSR values pack into SPC format (ADSR1/ADSR2)
- Trigger button plays the sample once

**Pass/Fail:** [ ]

---

### TC-019: Real-Time Visualization Updates

**Objective:** Verify waveform and spectrum update continuously

**Steps:**
1. Load an SPC file
2. Play the track
3. Watch waveform display for 10 seconds
4. Switch to view with spectrum analyzer
5. Watch spectrum for 10 seconds
6. Pause playback

**Expected Result:**
- Waveform updates at ~60 FPS
- Left and right channels visible (different colors)
- Spectrum shows frequency content
- Updates stop or flatten when paused
- No visual stuttering or freezing

**Pass/Fail:** [ ]

---

### TC-020: MIDI Learn Mode

**Objective:** Verify MIDI CC mapping functionality

**Steps:**
1. Open plugin GUI
2. Right-click on Master Volume slider (or use MIDI Learn button)
3. Enter MIDI Learn mode
4. Move a physical MIDI controller knob/slider
5. Verify mapping is created
6. Move the MIDI controller again
7. Verify parameter responds
8. Clear the mapping
9. Verify parameter no longer responds to MIDI

**Expected Result:**
- MIDI Learn mode indicated visually
- First CC movement creates the mapping
- Parameter responds to subsequent CC movements
- Value range maps 0-127 to parameter range
- Mappings can be cleared

**Pass/Fail:** [ ]

---

### TC-021: MIDI Learn Persistence

**Objective:** Verify MIDI mappings save and restore

**Steps:**
1. Create multiple MIDI CC mappings (e.g., volume, mutes)
2. Save Ableton project
3. Close Ableton completely
4. Reopen the project
5. Verify MIDI controllers still control mapped parameters

**Expected Result:**
- All MIDI mappings restored
- No need to reconfigure mappings
- Mappings work identically to before save

**Pass/Fail:** [ ]

---

### TC-022: Pitch Bend Per Channel

**Objective:** Verify per-channel pitch bend parameters

**Steps:**
1. Load an SPC file with multiple voices
2. Adjust Pitch Bend slider for Channel 1
3. Verify pitch of Channel 1 changes
4. Set Pitch Bend Range to 12 semitones
5. Move pitch bend slider to maximum
6. Verify pitch change is 12 semitones up

**Expected Result:**
- Pitch bend affects individual channel pitch
- Center position = no pitch change
- Pitch bend range controls the semitone range
- Full bend produces expected semitone offset

**Pass/Fail:** [ ]

---

## Ableton Live 10.1.43 Specific Tests

### TC-L10-001: VST3 Loading in Live 10

**Steps:**
1. Open Live 10.1.43
2. Verify VST3 folder scanning
3. Locate plugin in browser

**Notes:** Live 10 uses older VST3 implementation - verify all features work

**Pass/Fail:** [ ]

---

### TC-L10-002: Plugin Delay Compensation

**Steps:**
1. Load plugin on track
2. Check Track Delay value
3. Compare to other plugins

**Notes:** Verify plugin reports latency correctly

**Pass/Fail:** [ ]

---

## Known Issues

| Issue | Severity | Workaround |
|-------|----------|------------|
| - | - | - |

## Test Results Summary

| Test Case | Date | Tester | Result | Notes |
|-----------|------|--------|--------|-------|
| TC-001 | | | | |
| TC-002 | | | | |
| TC-003 | | | | |
| TC-004 | | | | |
| TC-005 | | | | |
| TC-006 | | | | |
| TC-007 | | | | |
| TC-008 | | | | |
| TC-009 | | | | |
| TC-010 | | | | |
| TC-011 | | | | |
| TC-012 | | | | |
| TC-013 | | | | |
| TC-014 | | | | |
| TC-015 | | | | |
| TC-016 | | | | Keyboard shortcuts |
| TC-017 | | | | View mode tabs |
| TC-018 | | | | Sample editor |
| TC-019 | | | | Real-time visualizations |
| TC-020 | | | | MIDI learn mode |
| TC-021 | | | | MIDI learn persistence |
| TC-022 | | | | Pitch bend per channel |
| TC-L10-001 | | | | |
| TC-L10-002 | | | | |

## Automation Scripts

### Quick Test Launch

```powershell
# Install plugin and launch Ableton
.\build-vst3.ps1 -Release -Install -OpenAbleton

# Build only (no install)
.\build-vst3.ps1 -Release

# Clean rebuild
.\build-vst3.ps1 -Clean -Release -Install
```

### Verify Installation

```powershell
# Check if plugin is installed
$vst3Path = "C:\Program Files\Common Files\VST3\SnesSpcVst3.vst3"
if (Test-Path $vst3Path) {
    Write-Host "Plugin installed at: $vst3Path" -ForegroundColor Green
    Get-ChildItem $vst3Path -Recurse | Format-Table Name, Length
} else {
    Write-Host "Plugin NOT found" -ForegroundColor Red
}
```

### Open VST3 Folder

```powershell
explorer "C:\Program Files\Common Files\VST3"
```

## Troubleshooting

### Plugin Not Appearing in Ableton

1. Verify installation path: `C:\Program Files\Common Files\VST3\SnesSpcVst3.vst3`
2. Rescan plugins in Ableton: Options > Preferences > Plug-ins > Rescan
3. Check Ableton's VST3 blacklist
4. Verify .NET runtime DLL is present alongside plugin

### Plugin Crashes on Load

1. Check Windows Event Viewer for crash logs
2. Verify all dependencies are present in VST3 bundle
3. Try rebuilding with debug configuration: `.\build-vst3.ps1 -Clean`
4. Check CPU compatibility (AVX requirements)

### No Audio Output

1. Verify SPC file is loaded correctly
2. Check master volume is not at 0
3. Verify track output routing in Ableton
4. Check channel mute states

### High CPU Usage

1. Increase Ableton's buffer size
2. Reduce number of plugin instances
3. Check for debug builds (use Release)
4. Monitor for memory leaks

---

*Last Updated: 2026-01-05*
*Plugin Version: 0.1.0*
*Tested With: Ableton Live 10.1.43, 11.x, 12.x*
