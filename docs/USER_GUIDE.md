# SNES SPC VST3 Plugin - User Guide

## Overview

The SNES SPC VST3 Plugin allows you to play and edit Super Nintendo (SNES) SPC audio files directly within your DAW (Digital Audio Workstation) like Ableton Live. It provides cycle-accurate emulation of the Sony SPC700 CPU and S-DSP audio processor.

## Features

- **SPC Playback**: Load and play any `.spc` file with accurate SNES audio emulation
- **8-Voice Control**: Mute, solo, and adjust volume for each of the 8 S-DSP voices
- **Sample Export**: Extract BRR samples to WAV files
- **DAW Sync**: Tempo and time signature synchronization with host
- **Loop Control**: Enable/disable song looping
- **Real-time Editing**: Modify DSP parameters in real-time

---

## Installation

### Windows

1. Copy `SnesSpcPlugin.vst3` to your VST3 folder:
   - System: `C:\Program Files\Common Files\VST3\`
   - User: `%APPDATA%\VST3\`

2. Copy `SpcPlugin.Core.dll` to the same folder as the VST3 plugin

3. Rescan plugins in your DAW

### macOS

1. Copy `SnesSpcPlugin.vst3` to:
   - System: `/Library/Audio/Plug-Ins/VST3/`
   - User: `~/Library/Audio/Plug-Ins/VST3/`

2. Copy `libSpcPlugin.Core.dylib` alongside the plugin

---

## Quick Start

### Loading an SPC File

1. Insert the SNES SPC plugin on an audio track
2. Click the **Load SPC** button in the plugin UI
3. Browse to and select your `.spc` file
4. Press **Play** or start DAW playback

### Basic Controls

| Control | Description |
|---------|-------------|
| **Play/Pause** | Start/stop SPC playback |
| **Stop** | Stop and reset to beginning |
| **Loop** | Toggle song looping |
| **Master Volume** | Overall output level (0-200%) |
| **Position** | Current playback position |

---

## Voice Mixer

The S-DSP has 8 independent voices. Each voice can play a different sample with its own settings.

### Per-Voice Controls

| Control | Range | Description |
|---------|-------|-------------|
| **Mute** | On/Off | Silence this voice |
| **Solo** | On/Off | Only hear this voice (and other soloed voices) |
| **Volume** | 0-100% | Individual voice level |

### Voice Information

When an SPC is loaded, the plugin displays information about each voice:

- **Source #**: Which sample (0-255) the voice is playing
- **Frequency**: Current playback pitch
- **Envelope**: ADSR or GAIN mode indicator
- **Status**: Key on/off, envelope phase

---

## Ableton Live Integration

### Transport Sync

The plugin automatically syncs with Ableton's transport:

- **Play/Stop**: Plugin follows DAW transport
- **Tempo**: Displayed in beats/bars using DAW tempo
- **Loop**: Can be automated via clip envelopes

### Automation

All parameters can be automated in Ableton:

1. Click the **Configure** button (or press Cmd/Ctrl+M)
2. Move a plugin parameter
3. The parameter appears in the automation lane
4. Draw automation curves

#### Automatable Parameters

| Parameter | ID | Description |
|-----------|-----|-------------|
| Master Volume | 0 | Output level |
| Play/Pause | 1 | Playback state |
| Loop | 2 | Loop enable |
| Voice 1-8 Enable | 10-17 | Voice mute states |
| Voice 1-8 Solo | 20-27 | Voice solo states |
| Voice 1-8 Volume | 30-37 | Voice levels |

### MIDI Triggering

You can trigger voices via MIDI notes:

| Note | Voice |
|------|-------|
| C3 (60) | Voice 0 |
| C#3 (61) | Voice 1 |
| D3 (62) | Voice 2 |
| D#3 (63) | Voice 3 |
| E3 (64) | Voice 4 |
| F3 (65) | Voice 5 |
| F#3 (66) | Voice 6 |
| G3 (67) | Voice 7 |

- **Note On**: Key-on the voice (restart from sample start)
- **Note Off**: Key-off the voice (enter release phase)
- **Velocity**: Controls voice volume for that note

### Audio Routing

The plugin outputs stereo audio. You can:

1. Route to any audio track
2. Apply Ableton effects after the plugin
3. Use sidechain from other tracks
4. Record the output to audio clips

---

## Sample Export

### Export All Samples

1. Right-click the plugin window
2. Select **Export Samples...**
3. Choose an output folder
4. All BRR samples are exported as WAV files

### Export Single Voice

1. Solo the desired voice
2. Right-click and select **Export Voice Audio...**
3. Specify duration and output file
4. The isolated voice is rendered to WAV

### Export Full Mix

1. Set loop off (or specify duration)
2. Right-click and select **Export to WAV...**
3. Choose fade-out duration
4. Full SPC audio is rendered

---

## SPC Editing

### DSP Parameters

You can modify S-DSP registers in real-time:

#### Global Settings

| Parameter | Address | Description |
|-----------|---------|-------------|
| Main Volume L/R | $0C/$1C | Master volume |
| Echo Volume L/R | $2C/$3C | Echo effect level |
| Echo Feedback | $0D | Echo feedback amount |
| Echo Delay | $7D | Echo buffer size (16ms units) |
| FIR Coefficients | $xF | 8-tap echo filter |
| Noise Clock | $6C | Noise generator frequency |

#### Per-Voice Settings

| Parameter | Description |
|-----------|-------------|
| Volume L/R | Stereo panning |
| Pitch | Playback frequency |
| Source # | Sample to play |
| ADSR | Envelope settings |
| GAIN | Direct envelope control |

### Sample Directory

The Sample Directory Table (at DSP address $5D × $100) maps source numbers to BRR sample addresses. You can:

1. View current sample mappings
2. Remap sources to different samples
3. Import new BRR samples

---

## Technical Information

### Audio Specifications

| Specification | Value |
|---------------|-------|
| Sample Rate | 32000 Hz (native) / Resampled to DAW rate |
| Bit Depth | 16-bit |
| Channels | 2 (Stereo) |
| Latency | ~1ms + DAW buffer |

### Emulation Accuracy

The plugin emulates:

- **SPC700 CPU**: Full 256-instruction set, cycle-accurate
- **S-DSP**: All 8 voices, BRR decoding, Gaussian interpolation
- **Echo**: FIR filter, feedback, delay
- **Noise**: LFSR-based noise generator
- **Pitch Modulation**: Voice-to-voice modulation

### SPC File Format

SPC files contain:

| Offset | Size | Content |
|--------|------|---------|
| $00 | 33 | Header "SNES-SPC700 Sound File Data" |
| $21 | 2 | Version |
| $25 | 2 | PC (Program Counter) |
| $27 | 1 | A register |
| $28 | 1 | X register |
| $29 | 1 | Y register |
| $2A | 1 | PSW (flags) |
| $2B | 1 | SP (Stack Pointer) |
| $2E | 32 | ID666 tag (song info) |
| $100 | 65536 | RAM (64KB) |
| $10100 | 128 | DSP registers |
| $10180 | 64 | Extra RAM |

---

## Troubleshooting

### No Sound

1. Check DAW track is not muted
2. Verify Master Volume is above 0%
3. Ensure an SPC file is loaded
4. Check no voices are accidentally muted

### Crackling/Popping

1. Increase DAW buffer size
2. Reduce CPU load from other plugins
3. Check sample rate matches project

### Plugin Not Loading

1. Verify `SpcPlugin.Core.dll` is present
2. Check .NET 10 runtime is installed
3. Ensure VST3 folder permissions

### Wrong Pitch

1. SPC native rate is 32kHz
2. Plugin resamples to DAW rate
3. Check project sample rate settings

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Space | Play/Pause |
| Enter | Stop |
| L | Toggle Loop |
| M | Mute selected voice |
| S | Solo selected voice |
| 1-8 | Select voice |
| Ctrl+E | Export dialog |
| Ctrl+O | Open SPC file |

---

## Resources

- [SPC File Archive](https://www.zophar.net/music/spc.html) - Thousands of SNES music files
- [SNESMusic.org](http://snesmusic.org/) - SPC archive and player
- [GitHub Repository](https://github.com/TheAnsarya/ableton-snes-spc) - Source code

---

## Version History

### v1.0.0
- Initial release
- Full SPC700/S-DSP emulation
- 8-voice mixer with mute/solo
- DAW transport sync
- Sample export to WAV
- Ableton automation support
