# SPC Editing Workflow in Ableton Live

This guide covers the complete workflow for editing SNES SPC music files using the Ableton SNES SPC Plugin, from importing existing tracks to exporting finished audio for playback on real hardware.

## Overview

The SNES SPC Plugin enables you to:

- Import SPC files from classic SNES games
- Edit individual channels, samples, and sequences
- Apply effects and adjustments within SNES hardware limits
- Preview changes in real-time
- Export valid SPC files playable on actual SNES hardware

## Prerequisites

Before starting, ensure you have:

- Ableton Live 11 or later installed
- SNES SPC Plugin (VST3) installed to your plugins folder
- Source SPC files to edit (download from spcsets.com, Zophar's Domain, etc.)
- Basic familiarity with Ableton Live's interface

## Step 1: Setting Up the Plugin

### Loading the Plugin

1. Open Ableton Live
2. Create a new MIDI track (Ctrl+Shift+T)
3. In the browser, navigate to **Plug-ins → VST3 → SpcPlugin**
4. Drag the plugin onto your MIDI track

### Initial Configuration

The plugin opens with default settings:

```text
┌─────────────────────────────────────────────────────┐
│  SNES SPC Plugin v1.0                               │
├─────────────────────────────────────────────────────┤
│  [Load SPC] [Save Project] [Export SPC]             │
│                                                     │
│  Channels: ○○○○○○○○  (8 voice indicators)          │
│  Memory:   [████░░░░░░░░░░░░] 32KB / 64KB          │
│                                                     │
│  Transport: [▶ Play] [■ Stop] [⟳ Loop]             │
└─────────────────────────────────────────────────────┘
```

## Step 2: Importing an SPC File

### Load Your SPC

1. Click **Load SPC** in the plugin interface
2. Navigate to your SPC file (e.g., `super_mario_world_athletic.spc`)
3. Click **Open**

### What Gets Imported

The plugin extracts:

- **Audio RAM (64KB)**: Complete memory snapshot including samples and sequences
- **DSP Registers**: All 128 DSP register values (voice settings, echo parameters)
- **ID666 Metadata**: Song title, game name, artist, duration, fade length

### Understanding the Import Summary

After loading, the plugin displays:

```text
┌─────────────────────────────────────────────────────┐
│  Imported: super_mario_world_athletic.spc           │
├─────────────────────────────────────────────────────┤
│  Game: Super Mario World                            │
│  Song: Athletic Theme                               │
│  Duration: 2:45 (fade: 10s)                         │
│                                                     │
│  Driver: N-SPC (Nintendo)                           │
│  Samples: 12 detected (28KB used)                   │
│  Echo: Enabled (4KB buffer)                         │
│  Free RAM: 32KB                                     │
└─────────────────────────────────────────────────────┘
```

## Step 3: The Editing Interface

### Channel Mixer

The 8-channel mixer shows all active voices:

```text
  Ch1    Ch2    Ch3    Ch4    Ch5    Ch6    Ch7    Ch8
┌────┐ ┌────┐ ┌────┐ ┌────┐ ┌────┐ ┌────┐ ┌────┐ ┌────┐
│████│ │████│ │████│ │████│ │░░░░│ │░░░░│ │████│ │████│
│████│ │██░░│ │████│ │██░░│ │░░░░│ │░░░░│ │██░░│ │██░░│
│████│ │░░░░│ │████│ │░░░░│ │░░░░│ │░░░░│ │░░░░│ │░░░░│
├────┤ ├────┤ ├────┤ ├────┤ ├────┤ ├────┤ ├────┤ ├────┤
│Lead│ │Bass│ │Harm│ │Pad │ │ -- │ │ -- │ │Drum│ │Drum│
│ M S│ │ M S│ │ M S│ │ M S│ │ M S│ │ M S│ │ M S│ │ M S│
└────┘ └────┘ └────┘ └────┘ └────┘ └────┘ └────┘ └────┘
  127    96    127    80     0      0      110    110
```

- **Volume bars**: Real-time level display
- **M**: Mute toggle
- **S**: Solo toggle
- **Volume**: 0-127 (click and drag)

### Sample Browser

View and manage all samples in the SPC:

```text
┌─────────────────────────────────────────────────────┐
│  Samples (12)                              [+ Add]  │
├─────────────────────────────────────────────────────┤
│  ○ 00: Piano         ████████░░░░   2.1KB  C4     │
│  ○ 01: Bass          ██████░░░░░░   1.8KB  C2     │
│  ○ 02: Strings       ████████████   3.2KB  C4     │
│  ○ 03: Kick          ██░░░░░░░░░░   0.5KB  C3     │
│  ○ 04: Snare         ███░░░░░░░░░   0.7KB  E3     │
│  ○ 05: Hi-Hat        █░░░░░░░░░░░   0.3KB  G#4    │
│  ...                                               │
└─────────────────────────────────────────────────────┘
```

### Sequence Editor

Edit note sequences for each channel:

```text
┌─────────────────────────────────────────────────────┐
│  Channel 1: Lead                    [Piano Roll ▼]  │
├─────────────────────────────────────────────────────┤
│     1   2   3   4   5   6   7   8   9  10  11  12  │
│  C5 ─── ─── ─── ■■■ ─── ─── ■■■ ─── ─── ─── ─── ───│
│  B4 ─── ─── ─── ─── ■■■ ─── ─── ─── ─── ■■■ ─── ───│
│  A4 ─── ─── ■■■ ─── ─── ─── ─── ■■■ ─── ─── ─── ───│
│  G4 ─── ■■■ ─── ─── ─── ─── ─── ─── ─── ─── ─── ■■■│
│  F4 ─── ─── ─── ─── ─── ■■■ ─── ─── ■■■ ─── ─── ───│
│  E4 ■■■ ─── ─── ─── ─── ─── ─── ─── ─── ─── ■■■ ───│
└─────────────────────────────────────────────────────┘
```

## Step 4: Making Edits

### Adjusting Channel Parameters

#### Volume and Pan

1. Select a channel in the mixer
2. Drag the volume slider (0-127)
3. Adjust pan knob (-64 to +63, center = 0)

#### Muting/Soloing

- **Mute**: Click 'M' to silence a channel
- **Solo**: Click 'S' to hear only that channel
- Multiple solos stack (hear all soloed channels)

### Editing Samples

#### Replace a Sample

1. Select a sample in the browser
2. Click **Replace**
3. Choose a WAV file (mono, 8-32kHz recommended)
4. The plugin auto-converts to BRR format

#### Sample Requirements

| Parameter    | Requirement           | Notes                            |
| ------------ | --------------------- | -------------------------------- |
| Format       | WAV (PCM)             | Auto-converted to BRR            |
| Channels     | Mono                  | Stereo files auto-mixed to mono  |
| Sample Rate  | ≤32kHz                | Higher rates downsampled         |
| Bit Depth    | 8 or 16-bit           | Converted to 4-bit BRR           |
| Length       | No hard limit         | Longer = more memory used        |

#### BRR Encoding Options

When replacing samples, configure encoding:

```text
┌─────────────────────────────────────────────────────┐
│  BRR Encoding Options                               │
├─────────────────────────────────────────────────────┤
│  Filter Mode:     [Auto ▼]                          │
│  Pre-emphasis:    [✓] On                            │
│  Loop:           [✓] Enable   Start: 1024 samples  │
│  Quality:         [High ▼]                          │
│                                                     │
│  Preview Size: 2.1KB → 0.9KB (57% reduction)       │
│  [Cancel] [Encode & Replace]                        │
└─────────────────────────────────────────────────────┘
```

### Editing Sequences

#### Adding Notes

1. Open the sequence editor for a channel
2. Click in the piano roll to add notes
3. Drag to set duration
4. Right-click to delete

#### Editing Note Properties

Select a note to modify:

- **Velocity**: 0-127 (affects volume)
- **Duration**: In ticks (48 ticks = 1 beat at 120 BPM)
- **Pitch**: Note + octave (C0-B7)

#### Copy/Paste Patterns

1. Select notes (click + drag box)
2. Ctrl+C to copy
3. Move playhead to destination
4. Ctrl+V to paste

### Configuring Echo/Reverb

The SNES has a built-in echo effect:

```text
┌─────────────────────────────────────────────────────┐
│  Echo Configuration                                 │
├─────────────────────────────────────────────────────┤
│  Enabled:     [✓]                                   │
│  Delay:       [4 ▼] × 16ms = 64ms                  │
│  Feedback:    [████████░░░░░░░░] 64                 │
│  Mix:         [██████░░░░░░░░░░] 40                 │
│                                                     │
│  FIR Filter Coefficients:                           │
│  [127] [0] [0] [0] [0] [0] [0] [0]                  │
│  Preset: [Low-pass ▼]                              │
│                                                     │
│  Echo RAM Usage: 4.0KB / 15.0KB max                │
│  ⚠ Higher delay uses more RAM                      │
└─────────────────────────────────────────────────────┘
```

**Echo Parameters:**

| Parameter   | Range       | Description                      |
| ----------- | ----------- | -------------------------------- |
| Delay       | 0-15        | Echo delay (× 16ms)              |
| Feedback    | -128 to 127 | Echo decay (negative = inverted) |
| Mix         | 0-127       | Wet/dry balance                  |
| FIR         | 8 × sbyte   | 8-tap FIR filter coefficients    |

## Step 5: Working with Projects

### Saving Your Work

#### SPCX Project Format

Save your work in progress as an SPCX project:

1. Click **Save Project** (or Ctrl+S)
2. Choose location and filename (`my_remix.spcx`)
3. Project saves:
   - Full SPC data
   - Original WAV samples (before BRR encoding)
   - All editor state (solo/mute, zoom, etc.)
   - Undo history

#### Auto-Save

The plugin auto-saves every 5 minutes to:

```text
%APPDATA%/SpcPlugin/Autosave/[timestamp].spcx
```

### Loading Projects

1. Click **Load Project**
2. Select an `.spcx` file
3. All state is restored including:
   - Mute/solo settings
   - Editor window positions
   - Undo/redo history

## Step 6: Real-Time Preview

### Playback Controls

| Control      | Action                          | Shortcut |
| ------------ | ------------------------------- | -------- |
| Play         | Start playback                  | Space    |
| Stop         | Stop playback                   | Space    |
| Loop         | Toggle loop mode                | L        |
| Seek         | Click timeline to jump          | Click    |

### Syncing with Ableton

The plugin responds to Ableton's transport:

- **Play/Stop**: Syncs with Ableton's transport bar
- **Tempo**: SPC has fixed tempo (edit sequences to match)
- **Loop**: Plugin's internal loop, not Ableton's arrangement

### Monitoring Output

1. Ensure the track is armed for recording
2. Monitor through your audio interface
3. Plugin outputs stereo audio at host sample rate

## Step 7: Exporting SPC

### Export Options

Click **Export SPC** to open the export dialog:

```text
┌─────────────────────────────────────────────────────┐
│  Export SPC                                         │
├─────────────────────────────────────────────────────┤
│  Output File: [my_remix.spc          ] [Browse]     │
│                                                     │
│  Metadata:                                          │
│    Song Title:  [Athletic Theme (Remix)       ]     │
│    Game Title:  [Super Mario World            ]     │
│    Artist:      [Original: Koji Kondo         ]     │
│    Dumper:      [YourName                     ]     │
│                                                     │
│  Duration:     [2:45] + Fade: [10s]                │
│                                                     │
│  [ ] Include extended xid6 metadata                 │
│  [✓] Optimize memory layout                         │
│                                                     │
│  [Cancel] [Export]                                  │
└─────────────────────────────────────────────────────┘
```

### Export Process

1. Fill in metadata fields
2. Set duration and fade length
3. Click **Export**
4. Wait for compilation:
   - Sequences compiled to driver format
   - Samples re-encoded if modified
   - Memory layout optimized
   - SPC file written

### Verifying Output

After export, test your SPC:

1. **In Plugin**: Reload the exported SPC to verify
2. **SPC Player**: Test in standalone player (SPC700 Player)
3. **Emulator**: Test in bsnes/higan for accuracy
4. **Real Hardware**: Use SD2SNES/FXPak to play on actual SNES

## Step 8: Advanced Workflows

### Batch Processing

For editing multiple SPCs:

1. Create a template project with your preferred settings
2. Import SPC → Edit → Export → Repeat
3. Use keyboard shortcuts for efficiency

### Creating New Tracks

To create original music:

1. Load any SPC as a template (provides driver)
2. Replace all samples with your own
3. Clear sequences and compose new ones
4. Export as new SPC

### Collaboration

Share SPCX projects with collaborators:

1. Save as `.spcx` (includes all source data)
2. Share the file (can be large if many WAV samples)
3. Collaborator loads and continues editing

## Troubleshooting

### Common Issues

#### "Sample too large"

- Solution: Trim the sample or reduce quality
- The SNES has only 64KB RAM for everything

#### "Out of memory"

- Check total sample usage in the browser
- Reduce sample lengths or count
- Lower echo delay (uses RAM)

#### "Playback glitches"

- Increase Ableton's buffer size
- Close other audio applications
- Check CPU usage

#### "Export fails"

- Ensure destination folder exists and is writable
- Check disk space
- Verify no filename conflicts

### Getting Help

- **Documentation**: Full API reference in [API_REFERENCE.md](API_REFERENCE.md)
- **Format Specs**: SPC format details in [SPC_FORMAT.md](SPC_FORMAT.md)
- **GitHub Issues**: Report bugs at github.com/TheAnsarya/ableton-snes-spc

## Keyboard Shortcuts Reference

| Action            | Shortcut     |
| ----------------- | ------------ |
| Load SPC          | Ctrl+O       |
| Save Project      | Ctrl+S       |
| Export SPC        | Ctrl+E       |
| Play/Stop         | Space        |
| Toggle Loop       | L            |
| Undo              | Ctrl+Z       |
| Redo              | Ctrl+Y       |
| Mute Channel      | M            |
| Solo Channel      | S            |
| Copy              | Ctrl+C       |
| Paste             | Ctrl+V       |
| Delete            | Delete       |
| Select All        | Ctrl+A       |
| Zoom In           | Ctrl++       |
| Zoom Out          | Ctrl+-       |
| Reset View        | Ctrl+0       |

## Next Steps

Now that you understand the basic workflow:

1. **Practice**: Load some SPCs and experiment
2. **Study**: Learn about BRR encoding in the format docs
3. **Create**: Try making original content
4. **Share**: Export and share your creations

Happy editing! 🎮🎵
