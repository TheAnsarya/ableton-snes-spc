# Ableton Live Compatibility Guide

This document describes VST3 plugin compatibility with Ableton Live, including both the latest version and Ableton Live Suite 10.1.43.

## Supported Ableton Live Versions

| Version | Status | Notes |
|---------|--------|-------|
| Live 12.x | ✅ Fully Supported | Latest version, full VST3 support |
| Live 11.x | ✅ Fully Supported | Full VST3 support |
| Live 10.1+ | ✅ Supported | VST3 support added in 10.1 |
| Live 10.0.x | ⚠️ Limited | Use VST2 wrapper if needed |
| Live 9.x | ❌ Not Supported | VST3 not available |

### Ableton Live Suite 10.1.43 Specific Notes

This version was released in 2020 and has full VST3 support with the following considerations:

1. **Plugin Scanning**: Live 10.1.43 scans VST3 plugins on startup
2. **Parameter Automation**: Full support for VST3 parameter automation
3. **MIDI Support**: VST3 instruments receive MIDI via VST3 event system
4. **Sample Rate**: Supports 44.1, 48, 88.2, 96 kHz sample rates
5. **Buffer Sizes**: 64-2048 samples supported

## Installation

### Windows

1. Copy `SnesSpcVst3.vst3` folder to:
   ```
   C:\Program Files\Common Files\VST3\
   ```

2. Copy `SpcPlugin.Core.dll` (Native AOT) next to the plugin:
   ```
   C:\Program Files\Common Files\VST3\SnesSpcVst3.vst3\Contents\x86_64-win\
   ```

3. Restart Ableton Live or rescan plugins via:
   - Options → Preferences → Plug-Ins → Rescan Plug-Ins

### macOS

1. Copy `SnesSpcVst3.vst3` bundle to:
   ```
   ~/Library/Audio/Plug-Ins/VST3/
   ```
   or for all users:
   ```
   /Library/Audio/Plug-Ins/VST3/
   ```

2. If using Native AOT, copy the `.dylib` file to the bundle's MacOS folder

3. Restart Live or rescan plugins

### Linux (via Wine or native)

1. Copy to:
   ```
   ~/.vst3/
   ```

## Ableton Live Configuration

### Enabling VST3 Support

1. Open **Options** → **Preferences** (or `Ctrl/Cmd + ,`)
2. Navigate to **Plug-Ins** tab
3. Ensure **Use VST3 Plug-In System Folders** is enabled
4. Click **Rescan Plug-Ins** if the plugin doesn't appear

### Plugin Categories

The SNES SPC Player appears under:
- **Instruments** → **Plug-Ins** → **SNES SPC Player**

Or search for "SNES" or "SPC" in the browser.

## VST3 Features Used

### Parameter Automation

All parameters can be automated in Live:

| Parameter | Range | Description |
|-----------|-------|-------------|
| Master Volume | 0-100% | Overall output volume |
| Play/Pause | Toggle | Start/stop playback |
| Loop | Toggle | Enable loop playback |
| Voice 1-8 Enable | Toggle | Mute individual voices |
| Voice 1-8 Volume | 0-100% | Per-voice volume |
| Voice 1-8 Solo | Toggle | Solo individual voices |
| Pitch Bend 1-8 | 0-100% | Per-channel pitch bend |
| Pitch Bend Range | 1-24 st | Pitch bend range in semitones |

### MIDI Input

The plugin receives MIDI from Live's MIDI routing:

1. Create a **MIDI Track**
2. Drop the SNES SPC Player onto the track
3. Set **MIDI From** to your controller or another track
4. Arm the track for recording

MIDI mappings:
- **Note On/Off** → Trigger samples on voices
- **CC 7** (Volume) → Master volume
- **CC 11** (Expression) → Per-voice volume
- **CC 1** (Mod Wheel) → Modulation
- **Pitch Bend** → Per-channel pitch adjustment
- **Aftertouch** → Voice modulation

### State Saving

The plugin saves its complete state with the Live Set:
- Loaded SPC file (embedded in preset)
- Voice mute/solo states
- Volume settings
- Current position

## Troubleshooting

### Plugin Not Found

1. Verify the plugin is in the correct VST3 folder
2. Check that `SpcPlugin.Core.dll` is alongside the plugin
3. Ensure the plugin architecture matches Live (64-bit)
4. Try running Live as Administrator (Windows)

### No Audio Output

1. Verify an SPC file is loaded (drag-drop onto plugin)
2. Check the Play button is enabled
3. Verify voice mute states
4. Check Live's track volume and master output

### Clicks/Pops/Dropouts

1. Increase buffer size: Options → Audio → Buffer Size
2. Reduce CPU load from other plugins
3. Ensure audio driver is set correctly
4. On Windows, prefer ASIO driver

### Plugin Crashes on Load

1. Update to the latest plugin version
2. Verify .NET Native AOT library is present
3. Check Windows Event Viewer for crash details
4. Try Safe Mode in Live (hold Alt while starting)

### Automation Not Working

1. Verify the parameter is mapped correctly
2. Check that automation is enabled for the track
3. Try clicking the parameter in the plugin UI first

## Live 10.1.43 Specific Issues

### Known Limitations

1. **VST3 Context Menu**: Limited right-click menu compared to Live 11+
2. **Plugin Delay Compensation**: May need manual adjustment
3. **Sidechain**: VST3 sidechain routing less flexible than Live 11+

### Workarounds

For features not available in Live 10.1.43:

1. **Preset Management**: Use plugin's internal preset browser
2. **File Loading**: Drag-drop SPC files directly onto plugin
3. **Parameter Naming**: Some parameter names may appear truncated

## Performance Optimization

### CPU Usage

The SNES SPC emulator is lightweight, but for optimal performance:

1. **Freeze Tracks**: Freeze tracks when not actively editing
2. **Use Native AOT Build**: Faster than .NET runtime
3. **Reduce Voice Count**: Mute unused voices

### Latency

- Plugin adds minimal latency (~1ms at 44.1kHz)
- Total latency depends on buffer size
- For recording, use 64-128 sample buffer

### Memory

- Each plugin instance uses ~2-4 MB RAM
- Embedded SPC files add to project file size
- Consider bouncing/freezing for large projects

## Testing Checklist

Before using in production:

- [ ] Plugin loads without errors
- [ ] Audio outputs correctly (both L and R)
- [ ] SPC files load via drag-drop
- [ ] All 8 voices play
- [ ] Mute/solo works
- [ ] Automation records and plays back
- [ ] State saves/loads with project
- [ ] No clicks/pops at your buffer size
- [ ] MIDI triggers samples

## Contact & Support

For issues specific to Ableton Live compatibility:
1. Check GitHub Issues for known problems
2. Include Live version and plugin version in reports
3. Attach crash logs if available
