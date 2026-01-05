namespace SpcPlugin.Core.Emulation;

/// <summary>
/// S-DSP (Digital Signal Processor) emulator.
/// The S-DSP generates audio output based on BRR-encoded samples and DSP registers.
/// </summary>
public sealed class SDsp {
	private const int NumVoices = 8;
	private const int RegisterCount = 128;

	private readonly byte[] _registers = new byte[RegisterCount];
	private readonly Voice[] _voices = new Voice[NumVoices];

	// Global DSP state
	private short _mainVolL;
	private short _mainVolR;
	private short _echoVolL;
	private short _echoVolR;
	private byte _keyOn;
	private byte _keyOff;
	private byte _flags;
	private byte _endx;
	private byte _pmon;  // Pitch modulation
	private byte _non;   // Noise enable
	private byte _eon;   // Echo enable
	private byte _dir;   // Sample directory page
	private byte _esa;   // Echo buffer start
	private byte _edl;   // Echo delay
	private readonly byte[] _fir = new byte[8]; // FIR filter coefficients

	// Noise generator
	private int _noise = 0x4000;
	private int _noiseRate;

	/// <summary>
	/// Reference to SPC700 RAM for sample data access.
	/// </summary>
	public byte[]? Ram { get; set; }

	public SDsp() {
		for (int i = 0; i < NumVoices; i++) {
			_voices[i] = new Voice(this, i);
		}
	}

	/// <summary>
	/// Reads a DSP register value.
	/// </summary>
	public byte ReadRegister(int address) {
		return address switch {
			0x7c => _endx,
			_ => _registers[address & 0x7f],
		};
	}

	/// <summary>
	/// Writes a value to a DSP register.
	/// </summary>
	public void WriteRegister(int address, byte value) {
		address &= 0x7f;
		_registers[address] = value;

		switch (address) {
			case 0x0c: _mainVolL = (sbyte)value; break;
			case 0x1c: _mainVolR = (sbyte)value; break;
			case 0x2c: _echoVolL = (sbyte)value; break;
			case 0x3c: _echoVolR = (sbyte)value; break;
			case 0x4c: _keyOn = value; ProcessKeyOn(); break;
			case 0x5c: _keyOff = value; ProcessKeyOff(); break;
			case 0x6c:
				_flags = value;
				_noiseRate = value & 0x1f;
				break;
			case 0x7c: _endx = 0; break;
			case 0x0d: _pmon = value; break;
			case 0x2d: _non = value; break;
			case 0x3d: _eon = value; break;
			case 0x4d: _dir = value; break;
			case 0x5d: _esa = value; break;
			case 0x6d: _edl = value; break;
			case >= 0x0f and <= 0x7f when (address & 0x0f) == 0x0f:
				_fir[(address >> 4) & 7] = value;
				break;
		}

		int voice = address >> 4;
		if (voice < NumVoices) {
			int reg = address & 0x0f;
			UpdateVoiceRegister(voice, reg, value);
		}
	}

	/// <summary>
	/// Generates audio samples into the output buffer.
	/// </summary>
	public void GenerateSamples(Span<float> output, int sampleCount) {
		if (Ram == null) {
			output[..(sampleCount * 2)].Clear();
			return;
		}

		for (int i = 0; i < sampleCount; i++) {
			// Update noise generator
			UpdateNoise();

			int mixL = 0;
			int mixR = 0;

			for (int v = 0; v < NumVoices; v++) {
				var voice = _voices[v];
				if (!voice.Playing) continue;

				// Get sample (noise or BRR)
				short sample;
				if ((_non & (1 << v)) != 0) {
					sample = (short)_noise;
				} else {
					sample = voice.GetSample(Ram);
				}

				// Apply envelope
				sample = (short)((sample * voice.EnvLevel) >> 11);

				// Apply voice volume
				int volL = (sbyte)_registers[(v << 4) | 0x00];
				int volR = (sbyte)_registers[(v << 4) | 0x01];

				mixL += (sample * volL) >> 7;
				mixR += (sample * volR) >> 7;

				// Update envelope
				voice.UpdateEnvelope();
			}

			// Apply main volume
			mixL = (mixL * _mainVolL) >> 7;
			mixR = (mixR * _mainVolR) >> 7;

			// Clamp and convert to float
			output[i * 2] = Math.Clamp(mixL, short.MinValue, short.MaxValue) / 32768f;
			output[i * 2 + 1] = Math.Clamp(mixR, short.MinValue, short.MaxValue) / 32768f;
		}
	}

	/// <summary>
	/// Loads DSP register state from SPC file.
	/// </summary>
	public void LoadFromSpc(ReadOnlySpan<byte> dspRegisters) {
		if (dspRegisters.Length < 128) {
			throw new ArgumentException("DSP register data too small", nameof(dspRegisters));
		}

		for (int i = 0; i < 128; i++) {
			WriteRegister(i, dspRegisters[i]);
		}
	}

	/// <summary>
	/// Marks a voice as having reached its end block.
	/// </summary>
	internal void SetEndx(int voice) {
		_endx |= (byte)(1 << voice);
	}

	/// <summary>
	/// Gets the sample directory base address.
	/// </summary>
	internal int DirectoryAddress => _dir << 8;

	private void UpdateNoise() {
		// LFSR noise generator
		_noise = (_noise >> 1) ^ ((_noise & 1) != 0 ? 0x8000 : 0);
	}

	private void ProcessKeyOn() {
		if (Ram == null) return;

		for (int i = 0; i < NumVoices; i++) {
			if ((_keyOn & (1 << i)) != 0) {
				_voices[i].KeyOn(Ram);
				_endx &= (byte)~(1 << i);
			}
		}
		_keyOn = 0;
	}

	private void ProcessKeyOff() {
		for (int i = 0; i < NumVoices; i++) {
			if ((_keyOff & (1 << i)) != 0) {
				_voices[i].KeyOff();
			}
		}
	}

	private void UpdateVoiceRegister(int voice, int reg, byte value) {
		var v = _voices[voice];
		switch (reg) {
			case 0x02: v.PitchL = value; break;
			case 0x03: v.PitchH = value; break;
			case 0x04: v.Srcn = value; break;
			case 0x05: v.Adsr1 = value; break;
			case 0x06: v.Adsr2 = value; break;
			case 0x07: v.Gain = value; break;
		}
	}

	private sealed class Voice {
		private readonly SDsp _dsp;
		private readonly int _voiceIndex;

		public bool Playing { get; private set; }
		public byte PitchL { get; set; }
		public byte PitchH { get; set; }
		public byte Srcn { get; set; }
		public byte Adsr1 { get; set; }
		public byte Adsr2 { get; set; }
		public byte Gain { get; set; }
		public int EnvLevel { get; private set; }

		// BRR state
		private int _brrAddress;
		private int _brrOffset;
		private int _pitchCounter;
		private readonly short[] _samples = new short[12]; // Decoded BRR samples + history
		private int _sampleIndex;
		private short _prev1, _prev2;

		// Envelope state
		private EnvelopeMode _envMode;
		private int _envStep;

		public Voice(SDsp dsp, int index) {
			_dsp = dsp;
			_voiceIndex = index;
		}

		public void KeyOn(byte[] ram) {
			Playing = true;

			// Get sample address from directory
			int dirAddr = _dsp.DirectoryAddress + (Srcn << 2);
			_brrAddress = ram[dirAddr] | (ram[dirAddr + 1] << 8);
			_brrOffset = 0;
			_pitchCounter = 0;
			_sampleIndex = 0;
			_prev1 = _prev2 = 0;

			// Initialize envelope
			EnvLevel = 0;
			_envStep = 0;
			_envMode = (Adsr1 & 0x80) != 0 ? EnvelopeMode.Attack : EnvelopeMode.Direct;
		}

		public void KeyOff() {
			_envMode = EnvelopeMode.Release;
		}

		public short GetSample(byte[] ram) {
			if (!Playing) return 0;

			// Advance pitch counter
			int pitch = PitchL | ((PitchH & 0x3f) << 8);
			_pitchCounter += pitch;

			// Decode new BRR block when needed
			while (_pitchCounter >= 0x1000) {
				_pitchCounter -= 0x1000;
				AdvanceSample(ram);
			}

			// Gaussian interpolation (simplified to linear for now)
			int idx = _sampleIndex;
			int frac = (_pitchCounter >> 4) & 0xff;
			short s0 = _samples[(idx + 10) % 12];
			short s1 = _samples[(idx + 11) % 12];
			return (short)(s0 + ((s1 - s0) * frac >> 8));
		}

		private void AdvanceSample(byte[] ram) {
			_sampleIndex = (_sampleIndex + 1) % 12;

			if (_brrOffset == 0) {
				// Decode new BRR block
				DecodeBrrBlock(ram);
			}

			// Get sample from decoded buffer
			_samples[(_sampleIndex + 11) % 12] = _samples[_brrOffset];
			_brrOffset++;

			if (_brrOffset >= 16) {
				// Check for end/loop
				byte header = ram[_brrAddress];
				if ((header & 0x01) != 0) {
					// End block
					_dsp.SetEndx(_voiceIndex);
					if ((header & 0x02) != 0) {
						// Loop
						int dirAddr = _dsp.DirectoryAddress + (Srcn << 2);
						_brrAddress = ram[dirAddr + 2] | (ram[dirAddr + 3] << 8);
					} else {
						Playing = false;
						EnvLevel = 0;
					}
				} else {
					_brrAddress += 9;
				}
				_brrOffset = 0;
			}
		}

		private void DecodeBrrBlock(byte[] ram) {
			int addr = _brrAddress;
			byte header = ram[addr];
			int shift = header >> 4;
			int filter = (header >> 2) & 0x03;

			for (int i = 0; i < 16; i++) {
				int nibble = (i & 1) == 0
					? ram[addr + 1 + (i >> 1)] >> 4
					: ram[addr + 1 + (i >> 1)] & 0x0f;

				// Sign extend
				if (nibble >= 8) nibble -= 16;

				// Apply shift
				int sample = nibble << shift;

				// Apply filter
				sample = filter switch {
					0 => sample,
					1 => sample + _prev1 + (-_prev1 >> 4),
					2 => sample + (_prev1 << 1) + (-(_prev1 * 3) >> 5) - _prev2 + (_prev2 >> 4),
					3 => sample + (_prev1 << 1) + ((-_prev1 * 13) >> 6) - _prev2 + ((_prev2 * 3) >> 4),
					_ => sample,
				};

				// Clamp
				sample = Math.Clamp(sample, short.MinValue, short.MaxValue);
				_samples[i] = (short)sample;

				_prev2 = _prev1;
				_prev1 = (short)sample;
			}
		}

		public void UpdateEnvelope() {
			if (!Playing) return;

			switch (_envMode) {
				case EnvelopeMode.Attack:
					EnvLevel += GetAttackRate();
					if (EnvLevel >= 0x7ff) {
						EnvLevel = 0x7ff;
						_envMode = EnvelopeMode.Decay;
					}
					break;

				case EnvelopeMode.Decay:
					EnvLevel -= ((EnvLevel - 1) >> 8) + 1;
					int sustainLevel = ((Adsr2 >> 5) + 1) << 8;
					if (EnvLevel <= sustainLevel) {
						_envMode = EnvelopeMode.Sustain;
					}
					break;

				case EnvelopeMode.Sustain:
					int sr = Adsr2 & 0x1f;
					if (sr != 0) {
						EnvLevel -= ((EnvLevel - 1) >> 8) + 1;
					}
					if (EnvLevel < 0) EnvLevel = 0;
					break;

				case EnvelopeMode.Release:
					EnvLevel -= 8;
					if (EnvLevel <= 0) {
						EnvLevel = 0;
						Playing = false;
					}
					break;

				case EnvelopeMode.Direct:
					// Direct GAIN mode
					if ((Gain & 0x80) == 0) {
						EnvLevel = (Gain & 0x7f) << 4;
					} else {
						// TODO: Implement bent line envelope modes
						EnvLevel = 0x7ff;
					}
					break;
			}
		}

		private int GetAttackRate() {
			int ar = (Adsr1 & 0x0f) << 1;
			return ar == 0 ? 0 : (0x7ff / (ar + 1));
		}

		private enum EnvelopeMode {
			Attack,
			Decay,
			Sustain,
			Release,
			Direct
		}
	}
}
