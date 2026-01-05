using SpcPlugin.Core.Audio;

namespace SpcPlugin.Core.Midi;

/// <summary>
/// Processes MIDI events and applies them to the SPC engine.
/// Supports voice triggering, volume control, and DSP parameter changes.
/// </summary>
public class MidiProcessor {
	private readonly SpcEngine _engine;

	// Per-voice state
	private readonly int[] _voiceVelocity = new int[8];
	private readonly int[] _voiceBasePitch = new int[8];
	private readonly int[] _voicePitchBend = new int[8]; // -8192 to +8191

	// Global state
	private int _pitchBendRange = 2; // Semitones
	private bool _sustainPedalDown;
	private readonly bool[] _voiceSustained = new bool[8];

	public MidiProcessor(SpcEngine engine) {
		_engine = engine;
	}

	/// <summary>
	/// Gets or sets the pitch bend range in semitones (default: 2).
	/// </summary>
	public int PitchBendRange {
		get => _pitchBendRange;
		set => _pitchBendRange = Math.Clamp(value, 1, 24);
	}

	/// <summary>
	/// Process a single MIDI event.
	/// </summary>
	public void ProcessEvent(MidiEvent evt) {
		switch (evt.Type) {
			case MidiEventType.NoteOn when evt.Velocity > 0:
				HandleNoteOn(evt.Note, evt.Velocity);
				break;

			case MidiEventType.NoteOn: // Note on with velocity 0 = note off
			case MidiEventType.NoteOff:
				HandleNoteOff(evt.Note);
				break;

			case MidiEventType.ControlChange:
				HandleControlChange(evt.Controller, evt.Value);
				break;

			case MidiEventType.ProgramChange:
				HandleProgramChange(evt.Program);
				break;

			case MidiEventType.PitchBend:
				HandlePitchBend(evt.Channel, evt.PitchBendValue);
				break;
		}
	}

	/// <summary>
	/// Process multiple MIDI events in order.
	/// </summary>
	public void ProcessEvents(ReadOnlySpan<MidiEvent> events) {
		foreach (var evt in events) {
			ProcessEvent(evt);
		}
	}

	/// <summary>
	/// Reset all MIDI state.
	/// </summary>
	public void Reset() {
		Array.Clear(_voiceVelocity);
		Array.Clear(_voiceBasePitch);
		Array.Clear(_voicePitchBend);
		Array.Clear(_voiceSustained);
		_sustainPedalDown = false;
	}

	private void HandleNoteOn(int note, int velocity) {
		int voice = MidiNoteMap.NoteToVoice(note);
		if (voice < 0) return;

		_voiceVelocity[voice] = velocity;

		// Unmute voice if it was muted
		_engine.SetVoiceMuted(voice, false);

		// Set voice volume based on velocity
		float volume = velocity / 127f;
		_engine.SetVoiceVolume(voice, volume);

		// Trigger key-on via DSP
		if (_engine.Editor != null) {
			// Store original pitch for pitch bend calculations
			_voiceBasePitch[voice] = GetCurrentVoicePitch(voice);

			// Key on the voice
			_engine.Editor.KeyOnVoice(voice);
		}

		// Clear sustained flag
		_voiceSustained[voice] = false;
	}

	private void HandleNoteOff(int note) {
		int voice = MidiNoteMap.NoteToVoice(note);
		if (voice < 0) return;

		if (_sustainPedalDown) {
			// Mark as sustained - will release when pedal is lifted
			_voiceSustained[voice] = true;
		} else {
			// Immediate key-off
			if (_engine.Editor != null) {
				_engine.Editor.KeyOffVoice(voice);
			}
		}

		_voiceVelocity[voice] = 0;
	}

	private void HandleControlChange(int controller, int value) {
		switch (controller) {
			case MidiControllers.Volume:
				// CC7: Master volume
				_engine.MasterVolume = (value / 127f) * 2f; // 0-200%
				break;

			case MidiControllers.Pan:
				// CC10: Pan (not directly supported, but could affect voice balance)
				// TODO: Implement stereo balance
				break;

			case MidiControllers.Expression:
				// CC11: Expression (secondary volume)
				// Apply as multiplier to current volume
				break;

			case MidiControllers.Sustain:
				// CC64: Sustain pedal
				bool wasDown = _sustainPedalDown;
				_sustainPedalDown = value >= 64;

				// Release all sustained voices when pedal is lifted
				if (wasDown && !_sustainPedalDown) {
					ReleaseSustainedVoices();
				}
				break;

			case MidiControllers.VoiceMute:
				// Toggle mute for specified voice
				if (value is >= 0 and < 8) {
					bool currentMute = _engine.GetVoiceMuted(value);
					_engine.SetVoiceMuted(value, !currentMute);
				}
				break;

			case MidiControllers.VoiceSolo:
				// Toggle solo for specified voice
				if (value is >= 0 and < 8) {
					bool currentSolo = _engine.GetVoiceSolo(value);
					_engine.SetVoiceSolo(value, !currentSolo);
				}
				break;

			case MidiControllers.MasterVolume:
				// CC104: Master volume (0-127 -> 0-200%)
				_engine.MasterVolume = (value / 127f) * 2f;
				break;

			case MidiControllers.EchoFeedback:
				// CC105: Echo feedback
				_engine.Editor?.SetEchoFeedback((sbyte)(value - 64)); // Center at 64
				break;

			case MidiControllers.EchoDelay:
				// CC106: Echo delay (0-15)
				_engine.Editor?.SetEchoDelay(value / 8); // 0-127 -> 0-15
				break;

			case MidiControllers.NoiseFreq:
				// CC107: Noise frequency
				// Would need to add this to editor
				break;

			case MidiControllers.LoopEnable:
				// CC108: Loop toggle
				_engine.LoopEnabled = value >= 64;
				break;

			case MidiControllers.PlayStop:
				// CC109: Play/stop toggle
				if (value >= 64) {
					if (_engine.IsPlaying) {
						_engine.Pause();
					} else {
						_engine.Play();
					}
				}
				break;

			case MidiControllers.Reset:
				// CC110: Reset to beginning
				if (value >= 64) {
					_engine.Stop();
				}
				break;

			// All Notes Off
			case 123:
				AllNotesOff();
				break;

			// All Sound Off
			case 120:
				AllSoundOff();
				break;

			// Reset All Controllers
			case 121:
				ResetControllers();
				break;
		}
	}

	private void HandleProgramChange(int program) {
		// Program change could be used to load different SPC files
		// or switch between different sample sets
		// For now, this is a no-op
	}

	private void HandlePitchBend(int channel, int bendValue) {
		// Apply pitch bend to all voices (or could be per-channel)
		// bendValue is -8192 to +8191

		for (int voice = 0; voice < 8; voice++) {
			if (_voiceVelocity[voice] > 0) {
				_voicePitchBend[voice] = bendValue;
				ApplyPitchBend(voice);
			}
		}
	}

	private void ApplyPitchBend(int voice) {
		if (_engine.Editor == null) return;

		// Calculate pitch multiplier from bend value
		// bendValue -8192 to +8191 maps to -PitchBendRange to +PitchBendRange semitones
		double semitones = (_voicePitchBend[voice] / 8192.0) * _pitchBendRange;
		double pitchMultiplier = Math.Pow(2.0, semitones / 12.0);

		// Apply to base pitch
		int newPitch = (int)(_voiceBasePitch[voice] * pitchMultiplier);
		newPitch = Math.Clamp(newPitch, 0, 0x3FFF); // 14-bit pitch value

		_engine.Editor.SetVoicePitch(voice, (ushort)newPitch);
	}

	private int GetCurrentVoicePitch(int voice) {
		var info = _engine.Editor?.GetVoiceInfo(voice);
		return info?.Pitch ?? 0x1000; // Default to center pitch
	}

	private void ReleaseSustainedVoices() {
		for (int voice = 0; voice < 8; voice++) {
			if (_voiceSustained[voice]) {
				_engine.Editor?.KeyOffVoice(voice);
				_voiceSustained[voice] = false;
			}
		}
	}

	private void AllNotesOff() {
		for (int voice = 0; voice < 8; voice++) {
			_engine.Editor?.KeyOffVoice(voice);
			_voiceVelocity[voice] = 0;
		}
		Array.Clear(_voiceSustained);
	}

	private void AllSoundOff() {
		// Immediately silence all voices
		for (int voice = 0; voice < 8; voice++) {
			_engine.SetVoiceMuted(voice, true);
			_voiceVelocity[voice] = 0;
		}
		Array.Clear(_voiceSustained);
	}

	private void ResetControllers() {
		Array.Clear(_voicePitchBend);
		_sustainPedalDown = false;
		Array.Clear(_voiceSustained);
		_engine.MasterVolume = 1.0f;
	}
}
