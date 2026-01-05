# SPCX File Format Specification

Version 1.0 - Draft

## Overview

SPCX (SPC Extended) is a project file format for the Ableton SNES SPC Plugin. It extends the standard SPC format with additional metadata, editing state, and source assets that cannot be stored in a standard SPC file.

## Design Goals

1. **Lossless round-trip**: Import SPC → Edit → Export SPC should produce identical output
2. **Rich editing**: Store all editing state, undo history, annotations
3. **Source preservation**: Keep original WAV samples before BRR conversion
4. **Human-readable metadata**: JSON for metadata, binary for audio data
5. **Versioned format**: Support format evolution while maintaining compatibility

## File Structure

SPCX files are ZIP archives with a specific internal structure:

```text
myproject.spcx (ZIP archive)
├── manifest.json           # Project metadata and version
├── spc/
│   ├── ram.bin             # 64KB SPC700 RAM
│   ├── dsp.bin             # 128 bytes DSP registers
│   └── metadata.json       # ID666 and extended metadata
├── samples/
│   ├── index.json          # Sample directory
│   ├── 00_bass.wav         # Original WAV (optional)
│   ├── 00_bass.brr         # Compiled BRR
│   ├── 01_snare.wav
│   ├── 01_snare.brr
│   └── ...
├── sequences/
│   ├── index.json          # Sequence directory
│   └── channels/
│       ├── 0.json          # Channel 0 sequence
│       ├── 1.json          # Channel 1 sequence
│       └── ...
├── editor/
│   ├── state.json          # Editor state (solo/mute, zoom, etc.)
│   ├── markers.json        # User markers and annotations
│   └── history.json        # Undo/redo history (optional)
└── assets/
    └── ...                 # Additional project assets
```

## Manifest Schema

`manifest.json`:

```json
{
  "spcxVersion": "1.0",
  "createdAt": "2026-01-04T12:00:00Z",
  "modifiedAt": "2026-01-04T14:30:00Z",
  "createdBy": "Ableton SNES SPC Plugin 1.0.0",
  "project": {
    "name": "My SNES Song",
    "author": "Artist Name",
    "description": "A chiptune masterpiece",
    "tags": ["chiptune", "game-music", "original"]
  },
  "source": {
    "importedFrom": "original.spc",
    "importedAt": "2026-01-04T12:00:00Z",
    "originalChecksum": "sha256:abc123..."
  },
  "target": {
    "driver": "n-spc",
    "compatibility": ["snes", "spc-player"]
  }
}
```

## SPC Data

### ram.bin

Raw 65,536 bytes of SPC700 RAM exactly as it would appear in an SPC file.

### dsp.bin

Raw 128 bytes of DSP registers.

### spc/metadata.json

```json
{
  "id666": {
    "songTitle": "Forest Maze",
    "gameTitle": "Super Mario RPG",
    "artist": "Yoko Shimomura",
    "dumper": "Unknown",
    "dumpDate": "1996-03-09",
    "songLength": 180,
    "fadeLength": 10000,
    "comments": ""
  },
  "extended": {
    "tempo": 120,
    "timeSignature": "4/4",
    "key": "C major",
    "loopPoint": 12345,
    "customFields": {}
  }
}
```

## Sample Directory

### samples/index.json

```json
{
  "samples": [
    {
      "id": 0,
      "name": "bass",
      "sourceFile": "00_bass.wav",
      "brrFile": "00_bass.brr",
      "sampleRate": 16000,
      "loopStart": 1024,
      "loopEnd": 2048,
      "rootNote": 60,
      "encoding": {
        "filter": "auto",
        "preEmphasis": true,
        "quality": 0.95
      },
      "ramAddress": 8192,
      "ramSize": 4500
    }
  ],
  "totalRamUsage": 45000,
  "maxRamAvailable": 58000
}
```

### Sample Files

- **WAV files**: Original source audio (16-bit, mono)
- **BRR files**: Compiled BRR data ready for SPC

The WAV file is optional but recommended for quality re-encoding if parameters change.

## Sequence Data

### sequences/index.json

```json
{
  "format": "n-spc",
  "ticksPerBeat": 48,
  "tempo": 120,
  "channels": [
    {
      "id": 0,
      "name": "Lead",
      "instrument": 0,
      "volume": 100,
      "pan": 64,
      "muted": false,
      "solo": false,
      "sequenceFile": "channels/0.json"
    }
  ]
}
```

### sequences/channels/0.json

```json
{
  "channel": 0,
  "events": [
    {"tick": 0, "type": "instrument", "value": 0},
    {"tick": 0, "type": "volume", "value": 100},
    {"tick": 0, "type": "note", "note": 60, "duration": 48, "velocity": 100},
    {"tick": 48, "type": "note", "note": 62, "duration": 48, "velocity": 100},
    {"tick": 96, "type": "note", "note": 64, "duration": 96, "velocity": 100}
  ]
}
```

## Editor State

### editor/state.json

```json
{
  "view": {
    "zoom": 1.0,
    "scrollPosition": 0,
    "activeChannel": 0,
    "selectedSample": null
  },
  "mixer": {
    "channels": [
      {"id": 0, "muted": false, "solo": false, "visible": true},
      {"id": 1, "muted": true, "solo": false, "visible": true}
    ],
    "masterVolume": 1.0
  },
  "echo": {
    "enabled": true,
    "delay": 4,
    "feedback": 64,
    "filterCoefficients": [127, 0, 0, 0, 0, 0, 0, 0]
  }
}
```

### editor/markers.json

```json
{
  "markers": [
    {"tick": 0, "name": "Intro", "color": "#ff0000"},
    {"tick": 480, "name": "Verse 1", "color": "#00ff00"},
    {"tick": 1920, "name": "Chorus", "color": "#0000ff"}
  ],
  "annotations": [
    {"tick": 0, "channel": 0, "text": "Main melody starts here"}
  ]
}
```

## Export Process

When exporting to SPC:

1. **Compile sequences** to N-SPC (or target driver) format
2. **Encode samples** from WAV to BRR (or use cached BRR)
3. **Build memory layout** for all data
4. **Generate SPC file** with:
   - RAM containing driver, sequences, samples
   - DSP registers for initial voice state
   - ID666 metadata

## Import Process

When importing from SPC:

1. **Parse SPC file** (RAM, DSP, ID666)
2. **Detect sound driver** (N-SPC, Akao, etc.)
3. **Extract samples** from RAM
4. **Parse sequences** if driver is known
5. **Create SPCX structure** preserving all data

## Compatibility Notes

### Version Handling

```json
{
  "spcxVersion": "1.0",
  "minReaderVersion": "1.0"
}
```

Readers should refuse to open files with `minReaderVersion` higher than their version.

### Unknown Fields

Parsers should preserve unknown JSON fields to support forward compatibility.

### Migration

When format version increases, older files are automatically migrated on open.

## Compression

- ZIP uses DEFLATE compression
- WAV files may use additional lossless compression
- BRR files are stored uncompressed (already small)

## Security

- File paths must be relative and within archive
- Maximum uncompressed size limit (100MB default)
- Validate all JSON before parsing
