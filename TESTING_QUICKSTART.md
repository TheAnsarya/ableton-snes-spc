# SNES SPC Plugin - Manual Testing Quick Start

> **Version**: 0.4.0-beta  
> **Date**: January 2026  
> **Tester**: ________________  

This guide walks through the core testing workflow: installing the plugin, opening an SPC file, editing it, saving as SPCX, and exporting back to SPC.

---

## 📋 Pre-Test Checklist

Before starting, ensure you have:

- [ ] Windows 10/11 (64-bit)
- [ ] A VST3-compatible DAW (Ableton Live, FL Studio, REAPER, etc.)
- [ ] One or more SPC files for testing
- [ ] The release package (`SnesSpcVst3-0.4.0-beta-win64.zip`)

**Where to get SPC files:**
- [Zophar's Domain](https://www.zophar.net/music/nintendo-snes-spc)
- [SNESmusic.org](https://snesmusic.org/)

---

## Test 1: Installing the Plugin

### Steps

1. **Extract** the release package to a folder

2. **Open PowerShell** in the extracted folder

3. **Run the installer:**
   ```powershell
   # User install (no admin required)
   .\install.ps1 -UserInstall
   
   # OR system-wide install (requires admin)
   .\install.ps1
   ```

4. **Verify installation** - check the folder exists:
   - User: `%LOCALAPPDATA%\Programs\Common\VST3\SnesSpcVst3.vst3`
   - System: `C:\Program Files\Common Files\VST3\SnesSpcVst3.vst3`

### Expected Result
- [ ] Install script completes without errors
- [ ] `.vst3` folder exists in the target location

### Notes
```
Date: 
Result: PASS / FAIL
Notes: 
```

---

## Test 2: Loading the Plugin in DAW

### Steps (Ableton Live)

1. **Open** Ableton Live

2. **Rescan plugins:**
   - Go to `Options` > `Preferences` > `Plug-ins`
   - Click `Rescan plug-ins`
   - Wait for scan to complete

3. **Find the plugin:**
   - Go to `Browser` (left side) > `Plug-ins` > `VST3`
   - Look for **"SNES SPC Player"**

4. **Add to a track:**
   - Create a new MIDI track
   - Double-click the plugin, or drag it to the track

### Steps (Other DAWs)

| DAW | Plugin Location |
|-----|-----------------|
| FL Studio | Plugin Manager > Scan, then Channels > Add One > SNES SPC Player |
| REAPER | FX > Add > VST3i: SNES SPC Player |
| Studio One | Effects > Instruments > SNES SPC Player |

### Expected Result
- [ ] Plugin appears in plugin list after rescan
- [ ] Plugin window opens without crash
- [ ] GUI displays correctly (should see waveform area, controls)

### Notes
```
Date: 
DAW Version: 
Result: PASS / FAIL
Notes: 
```

---

## Test 3: Opening an SPC File

### Steps

1. **With plugin window open**, locate the **File Browser** or **Open** button

2. **Open an SPC file** using one of these methods:

   **Method A: Drag & Drop**
   - Drag an SPC file from Windows Explorer
   - Drop it on the plugin window
   
   **Method B: File Browser (if available)**
   - Click the folder/open icon
   - Navigate to your SPC file
   - Click Open

3. **Verify the file loaded:**
   - Song title should appear (if present in SPC metadata)
   - Waveform area should update
   - File info should display

### Expected Result
- [ ] SPC file loads without error
- [ ] Metadata displayed (song title, game name, etc.)
- [ ] No crash or hang

### Notes
```
Date: 
SPC File Tested: 
File Size: 
Result: PASS / FAIL
Notes: 
```

---

## Test 4: Playing Back SPC Audio

### Steps

1. **Click Play** button in the plugin window (or press Space if keyboard shortcuts work)

2. **Verify audio playback:**
   - Audio should play through your DAW's master output
   - Music should sound like authentic SNES audio

3. **Test playback controls:**
   - Pause/Resume
   - Stop
   - Seek (if available)

4. **Test voice controls:**
   - Mute individual voices (Voice 1-8)
   - Solo individual voices
   - Adjust voice volumes

### Expected Result
- [ ] Audio plays back clearly
- [ ] No crackling, popping, or distortion
- [ ] Voice mute/solo works correctly
- [ ] Playback can be paused and resumed

### Notes
```
Date: 
Audio Quality: Good / Acceptable / Poor
CPU Usage: 
Result: PASS / FAIL
Notes: 
```

---

## Test 5: Editing SPC Settings

### Steps

1. **With an SPC file loaded**, make some edits:

   **Voice Settings:**
   - [ ] Mute Voice 1
   - [ ] Solo Voice 3
   - [ ] Adjust Voice 5 volume to 50%
   
   **Global Settings:**
   - [ ] Adjust Master Volume
   - [ ] Toggle Loop On/Off
   
   **Echo Settings (if available):**
   - [ ] Enable/disable echo on a voice
   - [ ] Adjust echo delay

2. **Play back** to hear your changes

### Expected Result
- [ ] All edits apply immediately
- [ ] Audio reflects the changes
- [ ] No crashes when editing

### Notes
```
Date: 
Edits Made: 
Result: PASS / FAIL
Notes: 
```

---

## Test 6: Saving as SPCX Project

### Steps

1. **With edits made**, save as SPCX project:
   - Look for **"Save Project"** or **"Export SPCX"** option
   - Or use File menu if available

2. **Choose a save location** and filename (e.g., `test-project.spcx`)

3. **Click Save**

4. **Verify the file was created:**
   - Navigate to save location
   - File should have `.spcx` extension
   - File size should be reasonable (similar to original SPC + metadata)

5. **Optional: Inspect SPCX contents**
   - SPCX is a ZIP file - rename to `.zip` and open
   - Should contain: `manifest.json`, `state.json`, `spc-data.bin`

### Expected Result
- [ ] SPCX file created successfully
- [ ] File contains expected structure (if inspected)
- [ ] No errors during save

### Notes
```
Date: 
SPCX File Path: 
File Size: 
Result: PASS / FAIL
Notes: 
```

---

## Test 7: Reloading SPCX Project

### Steps

1. **Close the current project** (close plugin or create new instance)

2. **Open the SPCX file** you saved in Test 6:
   - Drag & drop onto plugin, OR
   - Use File > Open Project

3. **Verify state was restored:**
   - [ ] Same SPC file loaded
   - [ ] Voice mutes/solos restored
   - [ ] Volume settings restored
   - [ ] Other edits preserved

4. **Play back** to confirm audio is correct

### Expected Result
- [ ] SPCX project loads successfully
- [ ] All saved settings restored
- [ ] Audio plays correctly

### Notes
```
Date: 
Result: PASS / FAIL
Notes: 
```

---

## Test 8: Exporting Back to SPC

### Steps

1. **With an SPC loaded and edited**, export to SPC format:
   - Look for **"Export SPC"** or **"Save As SPC"** option
   - Or use File menu

2. **Choose export location** and filename (e.g., `test-export.spc`)

3. **Click Export**

4. **Verify the exported SPC:**
   - File created with `.spc` extension
   - File size ~66KB (standard SPC size)
   
5. **Test the exported SPC:**
   - Load it in another SPC player (e.g., [SNESAmp](http://www.alpha-ii.com/), [Winamp plugin](https://www.zophar.net/utilities/audio/spc-plug-in.html))
   - OR reload in this plugin to verify

### Expected Result
- [ ] SPC file exported successfully
- [ ] File is valid SPC format (~66KB)
- [ ] Exported SPC plays correctly in other players
- [ ] Audio changes preserved (if applicable)

### Notes
```
Date: 
Exported SPC Path: 
Verified In: 
Result: PASS / FAIL
Notes: 
```

---

## 📊 Test Summary

| Test | Description | Result | Notes |
|------|-------------|--------|-------|
| 1 | Install Plugin | ⬜ | |
| 2 | Load in DAW | ⬜ | |
| 3 | Open SPC File | ⬜ | |
| 4 | Play Back Audio | ⬜ | |
| 5 | Edit Settings | ⬜ | |
| 6 | Save SPCX | ⬜ | |
| 7 | Reload SPCX | ⬜ | |
| 8 | Export SPC | ⬜ | |

**Overall Result:** ⬜ PASS / ⬜ FAIL

**Tester Signature:** ________________  
**Date:** ________________

---

## 🐛 Bug Reporting

If you encounter issues, please report them at:  
https://github.com/TheAnsarya/ableton-snes-spc/issues

Include:
1. Test case number that failed
2. Exact steps to reproduce
3. Expected vs actual result
4. Screenshot/video if possible
5. System info (Windows version, DAW version, etc.)

---

## 📚 Additional Resources

- [Full Testing Guide](VST3_TESTING_GUIDE.md) - Comprehensive test cases
- [User Guide](../docs/USER_GUIDE.md) - Complete usage documentation
- [SPC Format Reference](../docs/SPC_FORMAT.md) - Technical format details
