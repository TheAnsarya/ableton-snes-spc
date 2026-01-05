namespace SpcPlugin.Core.Emulation;

/// <summary>
/// S-DSP (Digital Signal Processor) emulator.
/// The S-DSP generates audio output based on BRR-encoded samples and DSP registers.
/// </summary>
public sealed class SDsp {
	private const int NumVoices = 8;
	private const int RegisterCount = 128;

	// Gaussian interpolation table (from real SNES DSP)
	private static readonly short[] GaussTable = [
		   0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,
		   1,    1,    1,    1,    1,    1,    1,    1,    1,    1,    1,    2,    2,    2,    2,    2,
		   2,    2,    3,    3,    3,    3,    3,    4,    4,    4,    4,    4,    5,    5,    5,    5,
		   6,    6,    6,    6,    7,    7,    7,    8,    8,    8,    9,    9,    9,   10,   10,   10,
		  11,   11,   11,   12,   12,   13,   13,   14,   14,   15,   15,   15,   16,   16,   17,   17,
		  18,   19,   19,   20,   20,   21,   21,   22,   23,   23,   24,   24,   25,   26,   27,   27,
		  28,   29,   29,   30,   31,   32,   32,   33,   34,   35,   36,   36,   37,   38,   39,   40,
		  41,   42,   43,   44,   45,   46,   47,   48,   49,   50,   51,   52,   53,   54,   55,   56,
		  58,   59,   60,   61,   62,   64,   65,   66,   67,   69,   70,   71,   73,   74,   76,   77,
		  78,   80,   81,   83,   84,   86,   87,   89,   90,   92,   94,   95,   97,   99,  100,  102,
		 104,  106,  107,  109,  111,  113,  115,  117,  118,  120,  122,  124,  126,  128,  130,  132,
		 134,  137,  139,  141,  143,  145,  147,  150,  152,  154,  156,  159,  161,  163,  166,  168,
		 171,  173,  175,  178,  180,  183,  186,  188,  191,  193,  196,  199,  201,  204,  207,  210,
		 212,  215,  218,  221,  224,  227,  230,  233,  236,  239,  242,  245,  248,  251,  254,  257,
		 260,  263,  267,  270,  273,  276,  280,  283,  286,  290,  293,  297,  300,  304,  307,  311,
		 314,  318,  321,  325,  328,  332,  336,  339,  343,  347,  351,  354,  358,  362,  366,  370,
		 374,  378,  381,  385,  389,  393,  397,  401,  405,  410,  414,  418,  422,  426,  430,  434,
		 439,  443,  447,  451,  456,  460,  464,  469,  473,  477,  482,  486,  491,  495,  499,  504,
		 508,  513,  517,  522,  527,  531,  536,  540,  545,  550,  554,  559,  563,  568,  573,  577,
		 582,  587,  592,  596,  601,  606,  611,  615,  620,  625,  630,  635,  640,  644,  649,  654,
		 659,  664,  669,  674,  678,  683,  688,  693,  698,  703,  708,  713,  718,  723,  728,  732,
		 737,  742,  747,  752,  757,  762,  767,  772,  777,  782,  787,  792,  797,  802,  806,  811,
		 816,  821,  826,  831,  836,  841,  846,  851,  855,  860,  865,  870,  875,  880,  884,  889,
		 894,  899,  904,  908,  913,  918,  923,  927,  932,  937,  941,  946,  951,  955,  960,  965,
		 969,  974,  978,  983,  988,  992,  997, 1001, 1005, 1010, 1014, 1019, 1023, 1027, 1032, 1036,
		1040, 1045, 1049, 1053, 1057, 1061, 1066, 1070, 1074, 1078, 1082, 1086, 1090, 1094, 1098, 1102,
		1106, 1109, 1113, 1117, 1121, 1125, 1128, 1132, 1136, 1139, 1143, 1146, 1150, 1153, 1157, 1160,
		1164, 1167, 1170, 1174, 1177, 1180, 1183, 1186, 1190, 1193, 1196, 1199, 1202, 1205, 1207, 1210,
		1213, 1216, 1219, 1221, 1224, 1227, 1229, 1232, 1234, 1237, 1239, 1241, 1244, 1246, 1248, 1251,
		1253, 1255, 1257, 1259, 1261, 1263, 1265, 1267, 1269, 1270, 1272, 1274, 1275, 1277, 1279, 1280,
		1282, 1283, 1284, 1286, 1287, 1288, 1290, 1291, 1292, 1293, 1294, 1295, 1296, 1297, 1297, 1298,
		1299, 1300, 1300, 1301, 1302, 1302, 1303, 1303, 1303, 1304, 1304, 1304, 1304, 1304, 1305, 1305,
	];

	private readonly byte[] _registers = new byte[RegisterCount];
	private readonly Voice[] _voices = new Voice[NumVoices];

	// Global DSP state
	private short _mainVolL;
	private short _mainVolR;
	private short _echoVolL;
	private short _echoVolR;
	private short _echoFbVol;  // Echo feedback volume
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
	private readonly sbyte[] _fir = new sbyte[8]; // FIR filter coefficients (signed)

	// Echo state
	private int _echoPos;        // Current position in echo buffer
	private int _echoLength;     // Echo buffer length in samples
	private readonly short[] _echoHistL = new short[8]; // FIR history left
	private readonly short[] _echoHistR = new short[8]; // FIR history right
	private int _echoHistPos;    // FIR history position

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
			case 0x0d: _echoFbVol = (sbyte)value; break; // EFB (echo feedback)
			case 0x2d: _non = value; break;
			case 0x3d: _eon = value; break;
			case 0x4d: _dir = value; break;
			case 0x5d: _esa = value; break;
			case 0x6d:
				_edl = (byte)(value & 0x0f);
				_echoLength = _edl == 0 ? 4 : _edl * 512; // Echo buffer size in samples (x2 for stereo)
				break;
			case 0x1d: _pmon = value; break; // PMON moved to correct address
			case >= 0x0f and <= 0x7f when (address & 0x0f) == 0x0f:
				_fir[(address >> 4) & 7] = (sbyte)value;
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

		// Check if echo is enabled (bit 5 of FLG disables echo)
		bool echoEnabled = (_flags & 0x20) == 0;
		int echoBase = _esa << 8;
		if (_echoLength == 0) _echoLength = 4; // Minimum echo delay

		for (int i = 0; i < sampleCount; i++) {
			// Update noise generator
			UpdateNoise();

			int mixL = 0;
			int mixR = 0;
			int echoInL = 0;
			int echoInR = 0;

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

				int sampleL = (sample * volL) >> 7;
				int sampleR = (sample * volR) >> 7;

				mixL += sampleL;
				mixR += sampleR;

				// Add to echo input if voice has echo enabled
				if ((_eon & (1 << v)) != 0) {
					echoInL += sampleL;
					echoInR += sampleR;
				}

				// Update envelope
				voice.UpdateEnvelope();
			}

			// Process echo if enabled
			int echoOutL = 0;
			int echoOutR = 0;

			if (echoEnabled && _echoLength > 0) {
				// Read echo from buffer
				int echoAddr = echoBase + (_echoPos * 4);
				if (echoAddr + 3 < Ram.Length) {
					short echoL = (short)(Ram[echoAddr] | (Ram[echoAddr + 1] << 8));
					short echoR = (short)(Ram[echoAddr + 2] | (Ram[echoAddr + 3] << 8));

					// Apply FIR filter
					_echoHistL[_echoHistPos] = echoL;
					_echoHistR[_echoHistPos] = echoR;

					int firL = 0, firR = 0;
					for (int f = 0; f < 8; f++) {
						int idx = (_echoHistPos - f + 8) & 7;
						firL += _echoHistL[idx] * _fir[f];
						firR += _echoHistR[idx] * _fir[f];
					}
					echoOutL = firL >> 7;
					echoOutR = firR >> 7;

					_echoHistPos = (_echoHistPos + 1) & 7;

					// Write new echo (input + feedback)
					int newEchoL = echoInL + ((echoOutL * _echoFbVol) >> 7);
					int newEchoR = echoInR + ((echoOutR * _echoFbVol) >> 7);

					// Clamp and write back
					newEchoL = Math.Clamp(newEchoL, short.MinValue, short.MaxValue);
					newEchoR = Math.Clamp(newEchoR, short.MinValue, short.MaxValue);

					// Only write if echo write is enabled (bit 4 of FLG)
					if ((_flags & 0x10) == 0) {
						Ram[echoAddr] = (byte)newEchoL;
						Ram[echoAddr + 1] = (byte)(newEchoL >> 8);
						Ram[echoAddr + 2] = (byte)newEchoR;
						Ram[echoAddr + 3] = (byte)(newEchoR >> 8);
					}
				}

				// Advance echo position
				_echoPos++;
				if (_echoPos >= _echoLength) {
					_echoPos = 0;
				}
			}

			// Mix with main volume
			int outL = (mixL * _mainVolL) >> 7;
			int outR = (mixR * _mainVolR) >> 7;

			// Add echo output
			outL += (echoOutL * _echoVolL) >> 7;
			outR += (echoOutR * _echoVolR) >> 7;

			// Clamp and convert to float
			output[i * 2] = Math.Clamp(outL, short.MinValue, short.MaxValue) / 32768f;
			output[i * 2 + 1] = Math.Clamp(outR, short.MinValue, short.MaxValue) / 32768f;
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

			// Gaussian interpolation using 4 sample points
			// The fraction determines where between samples we are
			int idx = _sampleIndex;
			int frac = (_pitchCounter >> 4) & 0xff;

			// Get 4 samples for interpolation (oldest to newest)
			short s0 = _samples[(idx + 9) % 12];  // oldest
			short s1 = _samples[(idx + 10) % 12];
			short s2 = _samples[(idx + 11) % 12];
			short s3 = _samples[idx];              // newest

			// Apply Gaussian interpolation coefficients
			// The table is indexed by the fractional position (0-255)
			int g0 = GaussTable[255 - frac];
			int g1 = GaussTable[511 - frac];
			int g2 = GaussTable[256 + frac];
			int g3 = GaussTable[frac];

			int result = (s0 * g0 + s1 * g1 + s2 * g2 + s3 * g3) >> 11;

			return (short)Math.Clamp(result, short.MinValue, short.MaxValue);
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
