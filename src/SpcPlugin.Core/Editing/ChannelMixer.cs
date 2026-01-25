using SpcPlugin.Core.Hardware;

namespace SpcPlugin.Core.Editing;

/// <summary>
/// Channel mixer model for the 8 S-DSP voices.
/// Provides volume, pan, solo, mute, and per-voice effects controls.
/// All values map directly to real S-DSP registers.
/// </summary>
public sealed class ChannelMixer {
	private readonly SpcEditor _editor;
	private readonly ChannelState[] _channels = new ChannelState[8];
	private readonly object _lock = new();
	private int _soloMask;

	/// <summary>Event raised when a channel parameter changes.</summary>
	public event EventHandler<ChannelChangedEventArgs>? ChannelChanged;

	/// <summary>Event raised when channel levels are updated.</summary>
	public event EventHandler<LevelsUpdatedEventArgs>? LevelsUpdated;

	/// <summary>
	/// Creates a new channel mixer.
	/// </summary>
	/// <param name="editor">SPC editor for DSP access.</param>
	public ChannelMixer(SpcEditor editor) {
		_editor = editor;

		for (int i = 0; i < SnesDspLimits.VoiceCount; i++) {
			_channels[i] = new ChannelState {
				Index = i,
				Volume = 1.0f,
				Pan = 0.0f,
				PhaseInverted = false,
				IsMuted = false,
				IsSolo = false,
				EchoEnabled = false,
				NoiseEnabled = false,
				PitchModEnabled = false,
			};
		}

		SyncFromDsp();
	}

	/// <summary>
	/// Gets channel state by index.
	/// </summary>
	public ChannelState this[int channel] {
		get {
			ValidateChannel(channel);
			lock (_lock) {
				return _channels[channel].Clone();
			}
		}
	}

	/// <summary>
	/// Gets the number of channels (always 8).
	/// </summary>
	public int ChannelCount => 8;

	/// <summary>
	/// Gets whether any channel is soloed.
	/// </summary>
	public bool HasSolo => _soloMask != 0;

	/// <summary>
	/// Gets the solo mask (bit per channel).
	/// </summary>
	public int SoloMask => _soloMask;

	/// <summary>
	/// Gets the mute mask (bit per channel).
	/// </summary>
	public int MuteMask {
		get {
			int mask = 0;
			lock (_lock) {
				for (int i = 0; i < 8; i++) {
					if (_channels[i].IsMuted) mask |= 1 << i;
				}
			}
			return mask;
		}
	}

	/// <summary>
	/// Sets the volume for a channel.
	/// </summary>
	/// <param name="channel">Channel index (0-7).</param>
	/// <param name="volume">Volume (0.0 - 1.0).</param>
	public void SetVolume(int channel, float volume) {
		ValidateChannel(channel);
		volume = Math.Clamp(volume, 0, 1);

		lock (_lock) {
			if (Math.Abs(_channels[channel].Volume - volume) < 0.001f) return;
			_channels[channel].Volume = volume;
		}

		ApplyChannelVolume(channel);
		OnChannelChanged(channel, ChannelParameter.Volume);
	}

	/// <summary>
	/// Sets the pan for a channel.
	/// </summary>
	/// <param name="channel">Channel index (0-7).</param>
	/// <param name="pan">Pan (-1.0 = left, 0.0 = center, 1.0 = right).</param>
	public void SetPan(int channel, float pan) {
		ValidateChannel(channel);
		pan = Math.Clamp(pan, -1, 1);

		lock (_lock) {
			if (Math.Abs(_channels[channel].Pan - pan) < 0.001f) return;
			_channels[channel].Pan = pan;
		}

		ApplyChannelVolume(channel);
		OnChannelChanged(channel, ChannelParameter.Pan);
	}

	/// <summary>
	/// Sets the mute state for a channel.
	/// </summary>
	/// <param name="channel">Channel index (0-7).</param>
	/// <param name="muted">Whether to mute.</param>
	public void SetMute(int channel, bool muted) {
		ValidateChannel(channel);

		lock (_lock) {
			if (_channels[channel].IsMuted == muted) return;
			_channels[channel].IsMuted = muted;
		}

		ApplyChannelVolume(channel);
		OnChannelChanged(channel, ChannelParameter.Mute);
	}

	/// <summary>
	/// Toggles mute state for a channel.
	/// </summary>
	public void ToggleMute(int channel) {
		ValidateChannel(channel);
		lock (_lock) {
			SetMute(channel, !_channels[channel].IsMuted);
		}
	}

	/// <summary>
	/// Sets the solo state for a channel.
	/// </summary>
	/// <param name="channel">Channel index (0-7).</param>
	/// <param name="solo">Whether to solo.</param>
	public void SetSolo(int channel, bool solo) {
		ValidateChannel(channel);

		lock (_lock) {
			if (_channels[channel].IsSolo == solo) return;
			_channels[channel].IsSolo = solo;
			UpdateSoloMask();
		}

		// Apply volumes to all channels (solo affects others)
		for (int i = 0; i < 8; i++) {
			ApplyChannelVolume(i);
		}

		OnChannelChanged(channel, ChannelParameter.Solo);
	}

	/// <summary>
	/// Toggles solo state for a channel.
	/// </summary>
	public void ToggleSolo(int channel) {
		ValidateChannel(channel);
		lock (_lock) {
			SetSolo(channel, !_channels[channel].IsSolo);
		}
	}

	/// <summary>
	/// Clears all solo states.
	/// </summary>
	public void ClearSolo() {
		lock (_lock) {
			for (int i = 0; i < 8; i++) {
				_channels[i].IsSolo = false;
			}
			_soloMask = 0;
		}

		for (int i = 0; i < 8; i++) {
			ApplyChannelVolume(i);
		}
	}

	/// <summary>
	/// Sets echo enabled for a channel.
	/// </summary>
	public void SetEchoEnabled(int channel, bool enabled) {
		ValidateChannel(channel);

		lock (_lock) {
			if (_channels[channel].EchoEnabled == enabled) return;
			_channels[channel].EchoEnabled = enabled;
		}

		ApplyEchoMask();
		OnChannelChanged(channel, ChannelParameter.Echo);
	}

	/// <summary>
	/// Sets noise enabled for a channel.
	/// </summary>
	public void SetNoiseEnabled(int channel, bool enabled) {
		ValidateChannel(channel);

		lock (_lock) {
			if (_channels[channel].NoiseEnabled == enabled) return;
			_channels[channel].NoiseEnabled = enabled;
		}

		ApplyNoiseMask();
		OnChannelChanged(channel, ChannelParameter.Noise);
	}

	/// <summary>
	/// Sets pitch modulation enabled for a channel.
	/// Note: Voice 0 cannot receive pitch mod (no previous voice).
	/// </summary>
	public void SetPitchModEnabled(int channel, bool enabled) {
		ValidateChannel(channel);

		// Voice 0 cannot have pitch mod (it modulates from the previous voice)
		if (channel == 0 && enabled) {
			return;
		}

		lock (_lock) {
			if (_channels[channel].PitchModEnabled == enabled) return;
			_channels[channel].PitchModEnabled = enabled;
		}

		ApplyPitchModMask();
		OnChannelChanged(channel, ChannelParameter.PitchMod);
	}

	/// <summary>
	/// Sets phase inversion for a channel.
	/// The S-DSP uses signed volumes, so negative values invert phase.
	/// </summary>
	public void SetPhaseInverted(int channel, bool inverted) {
		ValidateChannel(channel);

		lock (_lock) {
			if (_channels[channel].PhaseInverted == inverted) return;
			_channels[channel].PhaseInverted = inverted;
		}

		ApplyChannelVolume(channel);
		OnChannelChanged(channel, ChannelParameter.Phase);
	}

	/// <summary>
	/// Updates peak levels for all channels (call from audio thread).
	/// </summary>
	/// <param name="levels">Array of 8 level values (0-1).</param>
	public void UpdateLevels(float[] levels) {
		if (levels.Length < 8) return;

		var levelsCopy = new float[8];
		Array.Copy(levels, levelsCopy, 8);

		LevelsUpdated?.Invoke(this, new LevelsUpdatedEventArgs(levelsCopy));
	}

	/// <summary>
	/// Synchronizes mixer state from DSP registers.
	/// </summary>
	public void SyncFromDsp() {
		byte echoMask = _editor.EchoEnable;
		byte noiseMask = _editor.NoiseEnable;
		byte pitchModMask = _editor.PitchModulation;

		lock (_lock) {
			for (int i = 0; i < SnesDspLimits.VoiceCount; i++) {
				var voiceInfo = _editor.GetVoiceInfo(i);

				// S-DSP volumes are signed - negative = phase inverted
				float left = voiceInfo.VolumeLeft / (float)SnesDspLimits.MaxVolume;
				float right = voiceInfo.VolumeRight / (float)SnesDspLimits.MaxVolume;

				// Detect phase inversion (both channels negative)
				bool inverted = voiceInfo.VolumeLeft < 0 && voiceInfo.VolumeRight < 0;
				if (inverted) {
					left = -left;
					right = -right;
				}

				float maxVol = Math.Max(Math.Abs(left), Math.Abs(right));

				_channels[i].Volume = maxVol;
				_channels[i].Pan = maxVol > 0.001f ? (right - left) / (2 * maxVol) : 0;
				_channels[i].PhaseInverted = inverted;

				// Effects from DSP register flags
				_channels[i].EchoEnabled = (echoMask & (1 << i)) != 0;
				_channels[i].NoiseEnabled = (noiseMask & (1 << i)) != 0;
				_channels[i].PitchModEnabled = (pitchModMask & (1 << i)) != 0;
			}
		}
	}

	/// <summary>
	/// Resets all channels to default state.
	/// </summary>
	public void Reset() {
		lock (_lock) {
			for (int i = 0; i < 8; i++) {
				_channels[i].Volume = 1.0f;
				_channels[i].Pan = 0.0f;
				_channels[i].IsMuted = false;
				_channels[i].IsSolo = false;
			}
			_soloMask = 0;
		}

		for (int i = 0; i < 8; i++) {
			ApplyChannelVolume(i);
		}
	}

	private void ApplyChannelVolume(int channel) {
		var state = _channels[channel];

		// Calculate effective volume
		float effectiveVolume = state.Volume;

		// Apply mute
		if (state.IsMuted) {
			effectiveVolume = 0;
		}

		// Apply solo (if any channel is soloed, others are muted)
		if (_soloMask != 0 && !state.IsSolo) {
			effectiveVolume = 0;
		}

		// Calculate L/R from volume and pan
		// Pan law: constant power panning
		float panL = state.Pan <= 0 ? 1.0f : 1.0f - state.Pan;
		float panR = state.Pan >= 0 ? 1.0f : 1.0f + state.Pan;

		// S-DSP uses signed 8-bit volumes (-128 to 127)
		// Negative values = phase inversion (real hardware feature)
		int sign = state.PhaseInverted ? -1 : 1;
		sbyte volL = SnesDspLimits.ClampVolume((int)(effectiveVolume * panL * SnesDspLimits.MaxVolume * sign));
		sbyte volR = SnesDspLimits.ClampVolume((int)(effectiveVolume * panR * SnesDspLimits.MaxVolume * sign));

		_editor.SetVoiceVolume(channel, volL, volR);
	}

	private void ApplyEchoMask() {
		byte mask = 0;
		lock (_lock) {
			for (int i = 0; i < 8; i++) {
				if (_channels[i].EchoEnabled) mask |= (byte)(1 << i);
			}
		}
		_editor.EchoEnable = mask;
	}

	private void ApplyNoiseMask() {
		byte mask = 0;
		lock (_lock) {
			for (int i = 0; i < 8; i++) {
				if (_channels[i].NoiseEnabled) mask |= (byte)(1 << i);
			}
		}
		_editor.NoiseEnable = mask;
	}

	private void ApplyPitchModMask() {
		byte mask = 0;
		lock (_lock) {
			for (int i = 0; i < 8; i++) {
				if (_channels[i].PitchModEnabled) mask |= (byte)(1 << i);
			}
		}
		_editor.PitchModulation = mask;
	}

	private void UpdateSoloMask() {
		_soloMask = 0;
		for (int i = 0; i < 8; i++) {
			if (_channels[i].IsSolo) _soloMask |= 1 << i;
		}
	}

	private static void ValidateChannel(int channel) {
		if (channel < 0 || channel > 7) {
			throw new ArgumentOutOfRangeException(nameof(channel), "Channel must be 0-7");
		}
	}

	private void OnChannelChanged(int channel, ChannelParameter parameter) {
		ChannelChanged?.Invoke(this, new ChannelChangedEventArgs(channel, parameter));
	}
}

/// <summary>
/// State of a single mixer channel.
/// All properties map to real S-DSP capabilities.
/// </summary>
public sealed class ChannelState {
	/// <summary>Channel index (0-7).</summary>
	public int Index { get; init; }

	/// <summary>Volume (0.0 - 1.0).</summary>
	public float Volume { get; set; }

	/// <summary>Pan (-1.0 = left, 0.0 = center, 1.0 = right).</summary>
	public float Pan { get; set; }

	/// <summary>Phase inversion (S-DSP supports negative volumes).</summary>
	public bool PhaseInverted { get; set; }

	/// <summary>Whether the channel is muted.</summary>
	public bool IsMuted { get; set; }

	/// <summary>Whether the channel is soloed.</summary>
	public bool IsSolo { get; set; }

	/// <summary>Whether echo is enabled (EON bit).</summary>
	public bool EchoEnabled { get; set; }

	/// <summary>Whether noise is enabled instead of sample (NON bit).</summary>
	public bool NoiseEnabled { get; set; }

	/// <summary>Whether pitch modulation from previous voice is enabled (PMON bit).</summary>
	public bool PitchModEnabled { get; set; }

	/// <summary>Creates a copy of this state.</summary>
	public ChannelState Clone() => new() {
		Index = Index,
		Volume = Volume,
		Pan = Pan,
		PhaseInverted = PhaseInverted,
		IsMuted = IsMuted,
		IsSolo = IsSolo,
		EchoEnabled = EchoEnabled,
		NoiseEnabled = NoiseEnabled,
		PitchModEnabled = PitchModEnabled,
	};
}

/// <summary>
/// Channel parameter types for change events.
/// </summary>
public enum ChannelParameter {
	Volume,
	Pan,
	Phase,
	Mute,
	Solo,
	Echo,
	Noise,
	PitchMod,
}

/// <summary>
/// Event args for channel changes.
/// </summary>
public sealed class ChannelChangedEventArgs : EventArgs {
	public int Channel { get; }
	public ChannelParameter Parameter { get; }

	public ChannelChangedEventArgs(int channel, ChannelParameter parameter) {
		Channel = channel;
		Parameter = parameter;
	}
}

/// <summary>
/// Event args for level updates.
/// </summary>
public sealed class LevelsUpdatedEventArgs : EventArgs {
	/// <summary>Peak levels for each channel (0-1).</summary>
	public float[] Levels { get; }

	public LevelsUpdatedEventArgs(float[] levels) {
		Levels = levels;
	}
}
