# SpcPlugin.Core API Reference

## Namespace: SpcPlugin.Core.Audio

### SpcEngine

The main audio engine that coordinates CPU and DSP emulation for VST3 integration.

```csharp
public class SpcEngine : IDisposable
```

#### Constructor

```csharp
public SpcEngine(int sampleRate = 44100)
```

Creates a new SPC engine instance.

| Parameter | Type | Description |
|-----------|------|-------------|
| sampleRate | int | Output sample rate (default: 44100) |

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `SampleRate` | int | Current output sample rate |
| `IsPlaying` | bool | Whether playback is active |
| `LoopEnabled` | bool | Whether to loop at song end |
| `MasterVolume` | float | Master output volume (0.0 - 2.0) |
| `TotalCycles` | long | Total CPU cycles executed |
| `PositionSeconds` | double | Current position in seconds |
| `PositionBeats` | double | Current position in beats (requires tempo) |
| `PositionBars` | double | Current position in bars (requires time signature) |
| `Cpu` | Spc700 | Direct access to CPU state |
| `Dsp` | SDsp | Direct access to DSP state |
| `Editor` | SpcEditor | SPC file editing interface |

#### Methods

##### LoadSpc

```csharp
public void LoadSpc(ReadOnlySpan<byte> spcData)
public void LoadSpcFile(string filePath)
```

Load SPC data from memory or file.

##### Playback Control

```csharp
public void Play()
public void Pause()
public void Stop()
public void Seek(double seconds)
```

##### Audio Generation

```csharp
public void Process(Span<float> output, int sampleCount)
```

Generate interleaved stereo audio samples.

| Parameter | Type | Description |
|-----------|------|-------------|
| output | Span\<float\> | Output buffer (length = sampleCount × 2) |
| sampleCount | int | Number of stereo samples to generate |

##### Voice Control

```csharp
public void SetVoiceMuted(int voice, bool muted)
public bool GetVoiceMuted(int voice)
public void SetVoiceSolo(int voice, bool solo)
public bool GetVoiceSolo(int voice)
public void SetVoiceVolume(int voice, float volume)
public float GetVoiceVolume(int voice)
public void MuteAll()
public void UnmuteAll()
public void ClearSolo()
```

##### DAW Sync

```csharp
public void SetHostTempo(double bpm)
public void SetTimeSignature(double numerator, double denominator)
```

---

### SpcExporter

Exports SPC audio and samples to standard file formats.

```csharp
public class SpcExporter
```

#### Constructor

```csharp
public SpcExporter(SpcEngine engine)
```

#### Methods

##### ExportToWav

```csharp
public void ExportToWav(string outputPath, double durationSeconds, double fadeOutSeconds = 2.0)
```

Export full SPC playback to a WAV file.

| Parameter | Type | Description |
|-----------|------|-------------|
| outputPath | string | Output WAV file path |
| durationSeconds | double | Total duration to render |
| fadeOutSeconds | double | Fade-out length at end |

##### ExportVoiceToWav

```csharp
public void ExportVoiceToWav(string outputPath, int voice, double durationSeconds)
```

Export a single isolated voice to WAV.

##### ExportSampleToWav

```csharp
public void ExportSampleToWav(string outputPath, int sourceNumber)
```

Export a BRR sample from the sample directory.

##### ExportAllSamplesToWav

```csharp
public void ExportAllSamplesToWav(string outputFolder)
```

Export all samples to individual WAV files.

---

## Namespace: SpcPlugin.Core.Editing

### SpcEditor

Provides editing capabilities for loaded SPC files.

```csharp
public class SpcEditor
```

#### Constructor

```csharp
public SpcEditor(Spc700 cpu, SDsp dsp)
```

#### Voice Editing

```csharp
public VoiceInfo GetVoiceInfo(int voice)
public void SetVoiceVolume(int voice, int volumeLeft, int volumeRight)
public void SetVoicePitch(int voice, int pitch)
public void SetVoiceSource(int voice, int sourceNumber)
public void SetVoiceAdsr(int voice, int attack, int decay, int sustain, int release)
public void SetVoiceGain(int voice, int gainValue)
public void KeyOnVoice(int voice)
public void KeyOffVoice(int voice)
```

#### Global DSP

```csharp
public void SetMainVolume(int left, int right)
public void SetEchoVolume(int left, int right)
public void SetEchoFeedback(int feedback)
public void SetEchoDelay(int delay)
public void SetFirCoefficients(ReadOnlySpan<sbyte> coefficients)
public void SetEchoEnabled(int voiceMask)
public void SetNoiseEnabled(int voiceMask)
public void SetPitchModEnabled(int voiceMask)
```

#### Sample Directory

```csharp
public SampleInfo GetSampleInfo(int sourceNumber)
public void SetSampleAddress(int sourceNumber, ushort startAddress, ushort loopAddress)
public short[] DecodeBrrSample(int sourceNumber)
public void EncodeBrrSample(int sourceNumber, ReadOnlySpan<short> pcmData, ushort address)
```

#### Export

```csharp
public byte[] ExportSpc(SpcMetadata? metadata = null)
```

Export current state as SPC file data.

---

### VoiceInfo

Information about a single S-DSP voice.

```csharp
public record VoiceInfo(
    int VolumeLeft,
    int VolumeRight,
    int Pitch,
    int SourceNumber,
    int AdsrValue,
    int GainValue,
    bool UseAdsr,
    int EnvelopeLevel,
    int OutputLevel
);
```

### SampleInfo

Information about a BRR sample.

```csharp
public record SampleInfo(
    int SourceNumber,
    ushort StartAddress,
    ushort LoopAddress,
    int SampleLength,
    bool HasLoop
);
```

### SpcMetadata

ID666 tag metadata for SPC files.

```csharp
public record SpcMetadata(
    string SongTitle,
    string GameTitle,
    string DumperName,
    string Comments,
    string DumpDate,
    int PlayLength,
    int FadeLength
);
```

---

## Namespace: SpcPlugin.Core.Emulation

### Spc700

Sony SPC700 CPU emulator with full 256-instruction support.

```csharp
public class Spc700
```

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `A` | byte | Accumulator |
| `X` | byte | X index register |
| `Y` | byte | Y index register |
| `SP` | byte | Stack pointer |
| `PC` | ushort | Program counter |
| `PSW` | byte | Processor status word |
| `Cycles` | long | Total cycles executed |

#### Memory Access

```csharp
public byte ReadMemory(ushort address)
public void WriteMemory(ushort address, byte value)
public ReadOnlySpan<byte> GetRam()
```

#### Execution

```csharp
public int Step()
public void Reset()
```

---

### SDsp

Sony S-DSP audio processor emulator.

```csharp
public class SDsp
```

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Registers` | byte[] | 128 DSP registers |

#### Methods

```csharp
public byte ReadRegister(int address)
public void WriteRegister(int address, byte value)
public (short left, short right) GenerateSample()
public void Clock(int cycles)
```

#### Register Addresses

```csharp
// Per-voice registers (x = voice 0-7)
public const int VOLL = 0x00;   // x0: Volume left
public const int VOLR = 0x01;   // x1: Volume right
public const int PL = 0x02;     // x2: Pitch low
public const int PH = 0x03;     // x3: Pitch high
public const int SRCN = 0x04;   // x4: Source number
public const int ADSR1 = 0x05;  // x5: ADSR settings 1
public const int ADSR2 = 0x06;  // x6: ADSR settings 2
public const int GAIN = 0x07;   // x7: GAIN settings
public const int ENVX = 0x08;   // x8: Envelope value (read-only)
public const int OUTX = 0x09;   // x9: Output value (read-only)

// Global registers
public const int MVOLL = 0x0C;  // Main volume left
public const int MVOLR = 0x1C;  // Main volume right
public const int EVOLL = 0x2C;  // Echo volume left
public const int EVOLR = 0x3C;  // Echo volume right
public const int KON = 0x4C;    // Key on
public const int KOFF = 0x5C;   // Key off
public const int FLG = 0x6C;    // Flags (reset, mute, echo, noise)
public const int ENDX = 0x7C;   // Voice end flags (read-only)
public const int EFB = 0x0D;    // Echo feedback
public const int PMON = 0x2D;   // Pitch modulation enable
public const int NON = 0x3D;    // Noise enable
public const int EON = 0x4D;    // Echo enable
public const int DIR = 0x5D;    // Sample directory offset
public const int ESA = 0x6D;    // Echo buffer start
public const int EDL = 0x7D;    // Echo delay
public const int FIR = 0x0F;    // FIR filter coefficients (x = 0-7)
```

---

## Namespace: SpcPlugin.Core.Interop

### NativeExports

Static methods exported for native (C++) interop using `[UnmanagedCallersOnly]`.

All functions use the naming convention `spc_*` and are exported with C calling convention.

#### Engine Lifecycle

```c
// Create engine, returns handle (0 on failure)
intptr_t spc_engine_create(int sampleRate);

// Destroy engine
void spc_engine_destroy(intptr_t engine);
```

#### SPC Loading

```c
// Load SPC from memory, returns 1 on success
int spc_load_data(intptr_t engine, const uint8_t* data, int length);

// Load SPC from file path (UTF-8), returns 1 on success
int spc_load_file(intptr_t engine, const uint8_t* pathUtf8, int pathLength);
```

#### Playback Control

```c
void spc_play(intptr_t engine);
void spc_pause(intptr_t engine);
void spc_stop(intptr_t engine);
int spc_is_playing(intptr_t engine);  // Returns 1 if playing
void spc_seek(intptr_t engine, double seconds);
double spc_get_position(intptr_t engine);
```

#### Audio Generation

```c
// Generate interleaved stereo float samples
void spc_process(intptr_t engine, float* output, int sampleCount);
```

#### Master Controls

```c
void spc_set_master_volume(intptr_t engine, float volume);
float spc_get_master_volume(intptr_t engine);
void spc_set_loop_enabled(intptr_t engine, int enabled);
int spc_get_loop_enabled(intptr_t engine);
```

#### Voice Control

```c
void spc_set_voice_muted(intptr_t engine, int voice, int muted);
int spc_get_voice_muted(intptr_t engine, int voice);
void spc_set_voice_solo(intptr_t engine, int voice, int solo);
int spc_get_voice_solo(intptr_t engine, int voice);
void spc_set_voice_volume(intptr_t engine, int voice, float volume);
float spc_get_voice_volume(intptr_t engine, int voice);
void spc_mute_all(intptr_t engine);
void spc_unmute_all(intptr_t engine);
void spc_clear_solo(intptr_t engine);
```

#### DAW Sync

```c
void spc_set_host_tempo(intptr_t engine, double bpm);
void spc_set_time_signature(intptr_t engine, double numerator, double denominator);
double spc_get_position_beats(intptr_t engine);
double spc_get_position_bars(intptr_t engine);
```

#### Info

```c
int64_t spc_get_total_cycles(intptr_t engine);
int spc_get_sample_rate(intptr_t engine);
void spc_set_sample_rate(intptr_t engine, int sampleRate);
```

---

## Example Usage

### Basic Playback

```csharp
using SpcPlugin.Core.Audio;

using var engine = new SpcEngine(44100);
engine.LoadSpcFile("music.spc");
engine.Play();

var buffer = new float[1024 * 2]; // Stereo
while (engine.IsPlaying) {
    engine.Process(buffer, 1024);
    // Send buffer to audio output...
}
```

### Voice Isolation

```csharp
// Solo voice 2 (third voice)
for (int i = 0; i < 8; i++) {
    engine.SetVoiceSolo(i, i == 2);
}

// Or mute all except voice 2
for (int i = 0; i < 8; i++) {
    engine.SetVoiceMuted(i, i != 2);
}
```

### Export to WAV

```csharp
var exporter = new SpcExporter(engine);

// Export full song (3 minutes with 2 second fade)
exporter.ExportToWav("output.wav", 180, 2.0);

// Export single sample
exporter.ExportSampleToWav("sample_05.wav", 5);
```

### Edit DSP Parameters

```csharp
var editor = engine.Editor;

// Change voice 0 to use sample 10
editor.SetVoiceSource(0, 10);

// Set ADSR envelope
editor.SetVoiceAdsr(0, attack: 15, decay: 7, sustain: 7, release: 31);

// Enable echo on voices 0, 1, 2
editor.SetEchoEnabled(0b00000111);
```

### Native Interop (C++)

```cpp
#include "spc_native.h"

intptr_t engine = spc_engine_create(44100);
spc_load_file(engine, "music.spc", strlen("music.spc"));
spc_play(engine);

float buffer[1024 * 2];
while (spc_is_playing(engine)) {
    spc_process(engine, buffer, 1024);
    // Output audio...
}

spc_engine_destroy(engine);
```
