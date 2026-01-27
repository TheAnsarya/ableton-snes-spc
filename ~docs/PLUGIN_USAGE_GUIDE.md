# SNES SPC Plugin - Complete Usage Guide

> **Version**: 0.4.0-beta  
> **For**: Ableton Live 10.1+, 11, 12 (and other VST3-compatible DAWs)

This guide covers everything you need to know to use the SNES SPC Plugin: loading files, editing, saving projects, and exporting.

---

## Table of Contents

1. [Getting Started](#getting-started)
2. [Loading an SPC File](#loading-an-spc-file)
3. [Playback Controls](#playback-controls)
4. [Voice Mixer](#voice-mixer)
5. [Sample Management](#sample-management)
6. [Creating an SPCX Project](#creating-an-spcx-project)
7. [Working with SPCX Projects](#working-with-spcx-projects)
8. [Exporting to SPC](#exporting-to-spc)
9. [Advanced Features](#advanced-features)
10. [Keyboard Shortcuts](#keyboard-shortcuts)
11. [Troubleshooting](#troubleshooting)

---

## Getting Started

### First Launch

1. **Add Plugin to Track**:
   - In Ableton: Create a MIDI track → Browser → Plug-ins → VST3 → **SNES SPC Player**
   - Drag the plugin onto your track

2. **Plugin Window**:
   - The plugin window shows the main editor interface
   - Default size: 800x500 (resizable)

### Interface Overview

```
┌─────────────────────────────────────────────────────────┐
│  [File ▼]  [Edit ▼]  [View ▼]       SNES SPC Player    │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  ┌─────────────────────────────────────────────────┐   │
│  │            Waveform Display                      │   │
│  │     ═══════════════════════════════════════     │   │
│  └─────────────────────────────────────────────────┘   │
│                                                         │
│  [◀] [▶ Play] [■ Stop] [🔁 Loop]    00:00 / 02:30     │
│                                                         │
│  ┌─────────────────────────────────────────────────┐   │
│  │  Voice 1  [M][S] ════════════════ 100%          │   │
│  │  Voice 2  [M][S] ════════════════ 100%          │   │
│  │  Voice 3  [M][S] ════════════════ 100%          │   │
│  │  ...                                             │   │
│  └─────────────────────────────────────────────────┘   │
│                                                         │
│  Master: ════════════════════ 100%                     │
└─────────────────────────────────────────────────────────┘
```

---

## Loading an SPC File

### Method 1: File Menu

1. Click **File** → **Open SPC...**
2. Navigate to your `.spc` file
3. Click **Open**

### Method 2: Drag & Drop

1. Open Windows Explorer
2. Locate your `.spc` file
3. **Drag** the file onto the plugin window
4. **Drop** it on the waveform area

### Method 3: Preset Browser (if available)

1. Click the **Browser** button or tab
2. Navigate folders to find SPC files
3. Double-click to load

### After Loading

When an SPC loads successfully:
- ✅ Song title appears in the window title
- ✅ Waveform display shows audio preview
- ✅ Duration and metadata are displayed
- ✅ File info panel shows game name, artist, etc.

**File Info Displayed:**
| Field | Description |
|-------|-------------|
| Title | Song name from ID666 tag |
| Game | Game title |
| Artist | Composer name |
| Duration | Play length (if specified) |
| Driver | Detected sound driver (NSPC, Akao, etc.) |

---

## Playback Controls

### Transport Buttons

| Button | Action | Keyboard |
|--------|--------|----------|
| ▶ **Play** | Start playback | `Space` |
| ⏸ **Pause** | Pause at current position | `Space` |
| ■ **Stop** | Stop and reset to beginning | `Escape` |
| 🔁 **Loop** | Toggle looping on/off | `L` |

### Playback Features

- **Seek**: Click on the waveform to jump to that position
- **Scrub**: Drag on the position slider to scrub through audio
- **DAW Sync**: Playback follows Ableton's transport (play/stop)

### Position Display

- **Current**: `00:15` - Current playback position (MM:SS)
- **Total**: `02:30` - Total song duration
- **Samples**: Optional display of sample position

---

## Voice Mixer

The SNES S-DSP has **8 independent voices**. Each voice plays a different BRR sample with its own settings.

### Per-Voice Controls

```
Voice 1  [M] [S]  ═══════════════════ 100%   Sample: #12
         │   │              │           │
         │   │              │           └── Current sample number
         │   │              └── Volume slider (0-100%)
         │   └── Solo button
         └── Mute button
```

### Mute/Solo Behavior

| State | Result |
|-------|--------|
| **Mute ON** | Voice is silenced |
| **Solo ON** | Only hear this voice (and other soloed voices) |
| **Multiple Solos** | Hear all soloed voices, others muted |
| **All Solos OFF** | Hear all unmuted voices |

### Common Mixing Tasks

**Isolate a melody line:**
1. Click **Solo** on the voice playing the melody
2. All other voices are muted
3. Click Solo again to hear everything

**Remove a percussion track:**
1. Identify which voice is the drums (often Voice 6 or 7)
2. Click **Mute** on that voice
3. Drums are silenced, melody continues

**Balance the mix:**
1. Adjust individual **Volume** sliders
2. Lower voices that are too loud
3. Raise voices that are too quiet

---

## Sample Management

### Viewing Samples

The SPC file contains up to **256 BRR samples** in the sample directory. Each voice references a sample by number.

**Sample Info Panel:**
```
Sample #12
├── Address: $2000-$2400
├── Size: 1024 bytes (113 BRR blocks)
├── Loop: Yes (at $2100)
└── Duration: ~0.5 seconds
```

### Switching a Voice's Sample

To change which sample a voice plays:

1. **Select the Voice**: Click on the voice in the mixer
2. **Open Sample Browser**: Click **Samples** tab or button
3. **Preview Samples**: Click samples to hear them
4. **Assign Sample**: 
   - Drag sample onto voice, OR
   - Enter sample number (0-255) in the Source field

> ⚠️ **Note**: Changing samples modifies the DSP registers. This change is saved in SPCX projects.

### Sample Operations

| Operation | How To | Notes |
|-----------|--------|-------|
| **Preview** | Click sample in list | Plays at middle C |
| **Export WAV** | Right-click → Export | Saves decoded audio |
| **View Waveform** | Double-click sample | Opens sample editor |
| **Copy** | Drag to another slot | Duplicates BRR data |

---

## Creating an SPCX Project

### What is SPCX?

**SPCX** is the plugin's native project format. It saves:
- ✅ Complete SPC state (RAM + DSP)
- ✅ Your mixer settings (mutes, solos, volumes)
- ✅ Song metadata (title, artist, game)
- ✅ Analysis cache (detected driver, samples)
- ✅ Editor preferences (view state, zoom)

### Why Use SPCX?

| SPC File | SPCX Project |
|----------|--------------|
| Just audio data | Audio + your edits |
| No mixer state | Saves mute/solo/volume |
| Basic metadata | Rich metadata |
| No analysis | Cached driver detection |
| Single file | Organized ZIP structure |

### Creating a New Project

**From an SPC file:**

1. **Load** an SPC file (see above)
2. **Make edits**:
   - Adjust mixer
   - Change samples
   - Edit metadata
3. **Save as SPCX**:
   - **File** → **Save Project As...**
   - Choose location
   - Enter filename (`.spcx` extension)
   - Click **Save**

**Keyboard shortcut**: `Ctrl+Shift+S` (Save As)

### SPCX File Structure

SPCX is a ZIP archive containing:

```
MyProject.spcx (ZIP file)
├── manifest.json      # Project info, version
├── metadata.json      # Song title, artist, game
├── settings.json      # Mixer state, editor prefs
├── analysis.json      # Detected driver, samples
└── spc/
    ├── ram.bin        # 64KB SPC RAM
    ├── dsp.bin        # 128 DSP registers
    └── cpu.json       # CPU state (PC, registers)
```

---

## Working with SPCX Projects

### Opening a Project

1. **File** → **Open Project...**
2. Select your `.spcx` file
3. Click **Open**

**Or drag & drop** the `.spcx` file onto the plugin.

### What Gets Restored

When you open an SPCX project:

| Restored | Details |
|----------|---------|
| **Audio** | Complete SPC state, ready to play |
| **Mixer** | All mute/solo/volume settings |
| **Metadata** | Title, artist, game, comments |
| **View** | Active tab, waveform zoom |
| **Analysis** | Driver detection (no re-analysis needed) |

### Saving Changes

**Quick Save** (existing project):
- **File** → **Save Project** (`Ctrl+S`)
- Overwrites the current `.spcx` file

**Save As** (new file):
- **File** → **Save Project As...** (`Ctrl+Shift+S`)
- Creates a new `.spcx` file

### Project Metadata

Edit project info via **File** → **Project Properties...**:

```
┌─────────────────────────────────────┐
│       Project Properties            │
├─────────────────────────────────────┤
│ Title:    [Super Mario World BGM 1] │
│ Artist:   [Koji Kondo             ] │
│ Game:     [Super Mario World      ] │
│ Comments: [World 1-1 theme        ] │
│ Duration: [2:30                   ] │
│ Fade:     [10000 ms               ] │
├─────────────────────────────────────┤
│         [Cancel]  [OK]              │
└─────────────────────────────────────┘
```

---

## Exporting to SPC

Export your project back to a standard `.spc` file that can be played in any SPC player.

### How to Export

1. **File** → **Export SPC...**
2. Choose save location
3. Enter filename
4. Click **Export**

**Keyboard shortcut**: `Ctrl+E`

### What Gets Exported

| Included | Notes |
|----------|-------|
| ✅ Audio RAM (64KB) | Complete sound data |
| ✅ DSP Registers | Current voice/echo settings |
| ✅ CPU State | Playback position |
| ✅ ID666 Metadata | Title, artist, game, etc. |
| ❌ Mixer state | Not in SPC format |
| ❌ Editor prefs | Not in SPC format |

### Export Options

```
┌─────────────────────────────────────┐
│        Export SPC Options           │
├─────────────────────────────────────┤
│ ☑ Include ID666 metadata           │
│ ☐ Use binary ID666 format          │
│ ☑ Reset playback position to start │
│ ☐ Include extended ID666 (xid6)    │
├─────────────────────────────────────┤
│         [Cancel]  [Export]          │
└─────────────────────────────────────┘
```

### Compatibility

Exported SPC files work with:
- SNESAmp / Winamp
- foobar2000 (with spc plugin)
- VGMPlay
- BizHawk / other emulators
- Real SNES hardware (via flash cart)

---

## Advanced Features

### Editing Notes/Sequences

> ⚠️ **Advanced**: Editing sequences requires understanding of the SPC's sound driver format.

Most SPC files use a **sound driver** that interprets note data from RAM. Common drivers:

| Driver | Games | Notes |
|--------|-------|-------|
| **N-SPC / Kankichi** | Nintendo games | Most common |
| **Akao** | Square games (FF) | Complex |
| **HAL Lab** | Kirby, Mother 2 | |
| **Capcom** | Mega Man X | |

**To edit notes (if supported):**
1. Open **Sequence Editor** tab
2. Select a voice/channel
3. Click notes in the piano roll
4. Modify pitch, duration, velocity

### DSP Register Editor

Direct access to the S-DSP registers:

1. **View** → **DSP Registers**
2. See all 128 registers in hex view
3. Click a value to edit
4. Changes apply immediately

**Common registers:**
| Address | Name | Function |
|---------|------|----------|
| `$0C` | MVOL_L | Main volume left |
| `$1C` | MVOL_R | Main volume right |
| `$2C` | EVOL_L | Echo volume left |
| `$3C` | EVOL_R | Echo volume right |
| `$4D` | EON | Echo enable (per voice) |
| `$5D` | DIR | Sample directory page |
| `$6D` | ESA | Echo buffer start |
| `$7D` | EDL | Echo delay (0-15) |

### Echo/Reverb Settings

The S-DSP has a built-in **echo effect** with 8-tap FIR filter:

**Echo Controls:**
```
Echo Delay:    [====◉=====] 8  (128ms)
Echo Feedback: [=====◉====] 64 
Echo Volume:   [======◉===] 80%

FIR Filter: [Preset ▼] → Custom, Reverb, Chorus...

Per-Voice Echo:
  Voice 1: [✓]  Voice 5: [✓]
  Voice 2: [✓]  Voice 6: [ ]
  Voice 3: [ ]  Voice 7: [ ]
  Voice 4: [✓]  Voice 8: [✓]
```

---

## Keyboard Shortcuts

### Playback

| Key | Action |
|-----|--------|
| `Space` | Play / Pause |
| `Escape` | Stop |
| `L` | Toggle Loop |
| `Home` | Go to start |
| `End` | Go to end |

### File Operations

| Key | Action |
|-----|--------|
| `Ctrl+O` | Open SPC |
| `Ctrl+Shift+O` | Open SPCX Project |
| `Ctrl+S` | Save Project |
| `Ctrl+Shift+S` | Save Project As |
| `Ctrl+E` | Export SPC |

### Mixer

| Key | Action |
|-----|--------|
| `1-8` | Toggle mute on voice 1-8 |
| `Shift+1-8` | Toggle solo on voice 1-8 |
| `0` | Reset all mutes |
| `Shift+0` | Clear all solos |
| `↑/↓` | Adjust selected volume |

### View

| Key | Action |
|-----|--------|
| `Tab` | Next panel |
| `Shift+Tab` | Previous panel |
| `+/-` | Zoom waveform in/out |
| `F1` | Help |

---

## Troubleshooting

### Plugin Not Found

**Problem**: Plugin doesn't appear in Ableton's browser.

**Solutions**:
1. Verify installation path:
   - User: `%LOCALAPPDATA%\Programs\Common\VST3\SnesSpcVst3.vst3`
   - System: `C:\Program Files\Common Files\VST3\SnesSpcVst3.vst3`
2. Rescan plugins in Ableton preferences
3. Check that VST3 plugins are enabled

### No Audio Output

**Problem**: SPC loads but no sound plays.

**Solutions**:
1. Check that voices aren't all muted
2. Verify master volume is up
3. Ensure the track output is routed correctly
4. Check that the SPC file isn't corrupted (try another file)

### Crackling/Glitches

**Problem**: Audio has pops, clicks, or dropouts.

**Solutions**:
1. Increase buffer size in Ableton preferences
2. Reduce CPU load (freeze other tracks)
3. Check if the SPC uses unusual timing

### SPCX Won't Open

**Problem**: "Invalid project file" error.

**Solutions**:
1. File may be corrupted - try re-downloading
2. Check SPCX version compatibility
3. Try extracting the ZIP manually to inspect contents

### Export Doesn't Sound Right

**Problem**: Exported SPC sounds different than in the plugin.

**Solutions**:
1. Mixer settings (mute/solo/volume) don't export to SPC
2. DSP register changes DO export
3. Make changes via DSP editor for permanent modifications

---

## Quick Reference Card

```
┌─────────────────────────────────────────────────────┐
│           SNES SPC Plugin Quick Reference           │
├─────────────────────────────────────────────────────┤
│ LOAD:   File → Open SPC, or drag & drop            │
│ PLAY:   Space or ▶ button                          │
│ STOP:   Escape or ■ button                         │
│ MUTE:   Press 1-8 or click [M]                     │
│ SOLO:   Shift+1-8 or click [S]                     │
│                                                     │
│ SAVE PROJECT:  Ctrl+S → .spcx file                 │
│ EXPORT SPC:    Ctrl+E → .spc file                  │
│                                                     │
│ SPCX saves: Audio + mixer + metadata + analysis    │
│ SPC saves:  Audio + metadata (no mixer state)      │
└─────────────────────────────────────────────────────┘
```

---

## Further Resources

- [SPC File Format](../docs/SPC_FORMAT.md) - Technical format specification
- [VST3 Testing Guide](VST3_TESTING_GUIDE.md) - Detailed test cases
- [GitHub Issues](https://github.com/TheAnsarya/ableton-snes-spc/issues) - Report bugs
