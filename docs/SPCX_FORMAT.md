# SPCX Project Format Specification

> **Version**: 1.0  
> **File Extension**: `.spcx`  
> **MIME Type**: `application/x-spcx+zip`

## Overview

SPCX is the native project format for the SNES SPC Plugin. It's a ZIP-based container that stores:

- Complete SPC audio state (RAM, DSP, CPU)
- Song metadata (title, artist, game)
- Editor settings (mixer state, preferences)
- Cached analysis results (driver detection, samples)
- Edit history and undo data (optional)

## Why SPCX?

| Feature | SPC | SPCX |
|---------|-----|------|
| Audio data | ✅ | ✅ |
| Metadata | Basic ID666 | Rich JSON |
| Mixer state | ❌ | ✅ |
| Editor prefs | ❌ | ✅ |
| Driver detection | ❌ | ✅ Cached |
| Sample analysis | ❌ | ✅ Cached |
| Edit history | ❌ | ✅ Optional |
| Extensible | ❌ | ✅ |

## File Structure

SPCX files are standard ZIP archives with the following structure:

```
project.spcx (ZIP)
│
├── manifest.json          # Required: Project metadata
├── metadata.json          # Required: Song information
├── settings.json          # Required: Editor state
│
├── spc/                   # Required: SPC state
│   ├── ram.bin            # 65536 bytes - Audio RAM
│   ├── dsp.bin            # 128 bytes - DSP registers
│   └── cpu.json           # CPU register state
│
├── analysis.json          # Optional: Cached analysis
│
├── samples/               # Optional: Exported samples
│   ├── sample_000.brr
│   ├── sample_001.brr
│   └── ...
│
├── history/               # Optional: Edit history
│   ├── undo_stack.json
│   └── snapshots/
│       ├── snap_001.bin
│       └── ...
│
└── resources/             # Optional: Additional files
    ├── waveform_cache.bin
    └── thumbnail.png
```

---

## File Specifications

### manifest.json

Project-level metadata. **Required**.

```json
{
  "version": 1,
  "name": "Super Mario World - Overworld",
  "description": "Main overworld theme with custom mix",
  "createdDate": "2026-01-27T10:30:00Z",
  "modifiedDate": "2026-01-27T14:15:00Z",
  "generator": "SNES SPC Plugin v0.4.0",
  "originalFile": "smw_overworld.spc"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `version` | int | ✅ | Format version (currently 1) |
| `name` | string | ✅ | Project display name |
| `description` | string | ❌ | User notes/description |
| `createdDate` | ISO8601 | ✅ | Creation timestamp |
| `modifiedDate` | ISO8601 | ✅ | Last modification |
| `generator` | string | ❌ | Creating application |
| `originalFile` | string | ❌ | Source SPC filename |

### metadata.json

Song metadata, compatible with ID666 tags. **Required**.

```json
{
  "title": "Overworld Theme",
  "artist": "Koji Kondo",
  "game": "Super Mario World",
  "year": "1990",
  "dumperName": "SNESmusic",
  "comments": "Arranged by the composer",
  "durationMs": 150000,
  "fadeMs": 10000,
  "introLengthMs": 5000,
  "loopLengthMs": 145000,
  "tags": ["nintendo", "platformer", "classic"],
  "copyright": "Nintendo",
  "emulatorUsed": 2
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `title` | string | ❌ | Song title |
| `artist` | string | ❌ | Composer/artist name |
| `game` | string | ❌ | Game title |
| `year` | string | ❌ | Release year |
| `dumperName` | string | ❌ | Who ripped the SPC |
| `comments` | string | ❌ | Additional comments |
| `durationMs` | int | ❌ | Total play time in ms |
| `fadeMs` | int | ❌ | Fade out duration in ms |
| `introLengthMs` | int | ❌ | Non-looping intro |
| `loopLengthMs` | int | ❌ | Loop section length |
| `tags` | string[] | ❌ | Searchable tags |
| `copyright` | string | ❌ | Copyright holder |
| `emulatorUsed` | int | ❌ | 0=unknown, 1=ZSNES, 2=Snes9x |

### settings.json

Editor state and preferences. **Required**.

```json
{
  "voiceMutes": [false, false, false, false, false, false, false, false],
  "voiceSolos": [false, false, false, false, false, false, false, false],
  "voiceVolumes": [1.0, 1.0, 1.0, 0.8, 1.0, 1.0, 0.5, 1.0],
  "voicePans": [0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0],
  "masterVolume": 1.0,
  "loopEnabled": true,
  "playbackPosition": 0,
  "activeViewIndex": 0,
  "waveformZoom": 1.5,
  "selectedSampleIndex": -1,
  "selectedVoiceIndex": 0,
  "viewSettings": {
    "showSpectrum": true,
    "showVoiceInfo": true,
    "waveformColorScheme": "default"
  },
  "midiMappings": [
    {"param": "masterVolume", "channel": 1, "cc": 7},
    {"param": "voice1Volume", "channel": 1, "cc": 21}
  ]
}
```

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `voiceMutes` | bool[8] | all false | Mute state per voice |
| `voiceSolos` | bool[8] | all false | Solo state per voice |
| `voiceVolumes` | float[8] | all 1.0 | Volume per voice (0-1) |
| `voicePans` | float[8] | all 0.0 | Pan per voice (-1 to 1) |
| `masterVolume` | float | 1.0 | Master output volume |
| `loopEnabled` | bool | true | Loop playback |
| `playbackPosition` | long | 0 | Position in samples |
| `activeViewIndex` | int | 0 | Current tab/view |
| `waveformZoom` | float | 1.0 | Waveform zoom level |
| `selectedSampleIndex` | int | -1 | Selected sample |
| `selectedVoiceIndex` | int | 0 | Selected voice |
| `viewSettings` | object | {} | View preferences |
| `midiMappings` | array | [] | MIDI CC mappings |

### spc/ram.bin

Complete 64KB (65536 bytes) Audio RAM dump. **Required**.

- **Size**: Exactly 65536 bytes
- **Format**: Raw binary
- **Compression**: ZIP compression (recommended: optimal)

### spc/dsp.bin

S-DSP register state (128 bytes). **Required**.

- **Size**: Exactly 128 bytes
- **Format**: Raw binary, register order matches hardware

### spc/cpu.json

SPC700 CPU register state. **Required**.

```json
{
  "PC": 1024,
  "A": 0,
  "X": 0,
  "Y": 0,
  "PSW": 0,
  "SP": 239
}
```

| Field | Type | Range | Description |
|-------|------|-------|-------------|
| `PC` | int | 0-65535 | Program Counter |
| `A` | int | 0-255 | Accumulator |
| `X` | int | 0-255 | X Index Register |
| `Y` | int | 0-255 | Y Index Register |
| `PSW` | int | 0-255 | Program Status Word |
| `SP` | int | 0-255 | Stack Pointer |

### analysis.json

Cached analysis results. **Optional but recommended**.

```json
{
  "detectedDriver": "N-SPC",
  "driverVersion": "1.0",
  "confidence": 0.95,
  "sampleCount": 24,
  "samples": [
    {
      "index": 0,
      "startAddress": 8192,
      "endAddress": 8704,
      "loopAddress": 8448,
      "size": 512,
      "hasLoop": true,
      "blockCount": 56,
      "sampleCount": 896,
      "estimatedNote": "C4",
      "name": "Bass"
    }
  ],
  "memoryUsage": {
    "sampleBytes": 24576,
    "echoBytes": 4096,
    "driverBytes": 8192,
    "freeBytes": 28672
  },
  "echoSettings": {
    "delay": 8,
    "feedback": 64,
    "enabled": true,
    "bufferStart": 61440
  },
  "voiceUsage": [
    {"voice": 0, "sample": 5, "purpose": "Melody"},
    {"voice": 1, "sample": 5, "purpose": "Harmony"},
    {"voice": 7, "sample": 12, "purpose": "Drums"}
  ]
}
```

---

## Reading SPCX Files

### C# Example

```csharp
using System.IO.Compression;
using System.Text.Json;

public SpcxFile LoadProject(string path) {
    using var archive = ZipFile.OpenRead(path);
    
    // Read manifest
    var manifestEntry = archive.GetEntry("manifest.json");
    using var manifestStream = manifestEntry.Open();
    var manifest = JsonSerializer.Deserialize<SpcxManifest>(manifestStream);
    
    // Read RAM
    var ramEntry = archive.GetEntry("spc/ram.bin");
    using var ramStream = ramEntry.Open();
    var ram = new byte[65536];
    ramStream.ReadExactly(ram);
    
    // ... read other files
}
```

### Python Example

```python
import zipfile
import json

def load_spcx(path):
    with zipfile.ZipFile(path, 'r') as archive:
        # Read manifest
        with archive.open('manifest.json') as f:
            manifest = json.load(f)
        
        # Read RAM
        with archive.open('spc/ram.bin') as f:
            ram = f.read()  # 65536 bytes
        
        # Read DSP
        with archive.open('spc/dsp.bin') as f:
            dsp = f.read()  # 128 bytes
        
        return manifest, ram, dsp
```

---

## Writing SPCX Files

### C# Example

```csharp
public void SaveProject(SpcxFile project, string path) {
    using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
    var options = new JsonSerializerOptions { WriteIndented = true };
    
    // Write manifest
    var manifestEntry = archive.CreateEntry("manifest.json");
    using (var stream = manifestEntry.Open()) {
        JsonSerializer.Serialize(stream, project.Manifest, options);
    }
    
    // Write RAM (with compression)
    var ramEntry = archive.CreateEntry("spc/ram.bin", CompressionLevel.Optimal);
    using (var stream = ramEntry.Open()) {
        stream.Write(project.Ram);
    }
    
    // ... write other files
}
```

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1 | 2026-01 | Initial release |

### Future Versions

Planned additions for version 2:
- Custom sample names and annotations
- Voice color coding
- Marker/cue points
- Tempo/time signature info
- Multiple SPC states (for A/B comparison)

---

## Compatibility Notes

### Opening Newer Versions

If a newer SPCX version is encountered:
1. Check `manifest.version`
2. If version > supported, show warning
3. Attempt to load known fields (graceful degradation)
4. Unknown fields are ignored

### Minimal Valid SPCX

A minimal valid SPCX contains:
```
minimal.spcx
├── manifest.json   (version, name, dates)
├── metadata.json   (can be {})
├── settings.json   (can be {})
└── spc/
    ├── ram.bin     (65536 bytes)
    ├── dsp.bin     (128 bytes)
    └── cpu.json    (PC, A, X, Y, PSW, SP)
```

---

## Tools

### Inspect SPCX

Since SPCX is a ZIP file, you can inspect it with:

```bash
# List contents
unzip -l project.spcx

# Extract all
unzip project.spcx -d extracted/

# View manifest
unzip -p project.spcx manifest.json | jq .
```

### Convert SPC to SPCX

```csharp
var spcx = SpcxFile.ImportFromSpc("game.spc", analyze: true);
spcx.Save("game.spcx");
```

### Convert SPCX to SPC

```csharp
var spcx = SpcxFile.Load("game.spcx");
spcx.ExportToSpc("game_export.spc");
```
