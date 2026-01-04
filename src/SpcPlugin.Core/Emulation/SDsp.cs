namespace SpcPlugin.Core.Emulation;

/// <summary>
/// S-DSP (Digital Signal Processor) emulator.
/// The S-DSP generates audio output based on BRR-encoded samples and DSP registers.
/// </summary>
public sealed class SDsp {
	private const int NumVoices = 8;
	private const int RegisterCount = 128;
	private const int SampleRate = 32000; // Native SNES sample rate

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

	/// <summary>
	/// Reference to SPC700 RAM for sample data access.
	/// </summary>
	public Memory<byte>? Ram { get; set; }

	public SDsp() {
		for (int i = 0; i < NumVoices; i++) {
			_voices[i] = new Voice();
		}
	}

	/// <summary>
	/// Reads a DSP register value.
	/// </summary>
	public byte ReadRegister(int address) {
		return address switch {
			0x7c => _endx, // ENDX is special - cleared on read
			_ => _registers[address & 0x7f],
		};
	}

	/// <summary>
	/// Writes a value to a DSP register.
	/// </summary>
	public void WriteRegister(int address, byte value) {
		address &= 0x7f;
		_registers[address] = value;

		// Handle special registers
		switch (address) {
			case 0x0c: _mainVolL = (sbyte)value; break;
			case 0x1c: _mainVolR = (sbyte)value; break;
			case 0x2c: _echoVolL = (sbyte)value; break;
			case 0x3c: _echoVolR = (sbyte)value; break;
			case 0x4c: _keyOn = value; ProcessKeyOn(); break;
			case 0x5c: _keyOff = value; ProcessKeyOff(); break;
			case 0x6c: _flags = value; break;
			case 0x7c: _endx = 0; break; // ENDX write clears it
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

		// Voice registers (0xX0-0xX9 for voice X)
		int voice = address >> 4;
		if (voice < NumVoices) {
			int reg = address & 0x0f;
			UpdateVoiceRegister(voice, reg, value);
		}
	}

	/// <summary>
	/// Generates audio samples into the output buffer.
	/// </summary>
	/// <param name="output">Interleaved stereo output buffer (L, R, L, R, ...).</param>
	/// <param name="sampleCount">Number of stereo sample pairs to generate.</param>
	public void GenerateSamples(Span<float> output, int sampleCount) {
		for (int i = 0; i < sampleCount; i++) {
			// Mix all voices
			int mixL = 0;
			int mixR = 0;

			for (int v = 0; v < NumVoices; v++) {
				if (!_voices[v].Playing) continue;

				short sample = ProcessVoice(v);
				int volL = (sbyte)_registers[(v << 4) | 0x00];
				int volR = (sbyte)_registers[(v << 4) | 0x01];

				mixL += (sample * volL) >> 7;
				mixR += (sample * volR) >> 7;
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

	private void ProcessKeyOn() {
		for (int i = 0; i < NumVoices; i++) {
			if ((_keyOn & (1 << i)) != 0) {
				_voices[i].KeyOn();
				_endx &= (byte)~(1 << i);
			}
		}
		_keyOn = 0; // Key-on is edge triggered
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

	private short ProcessVoice(int voiceIndex) {
		var voice = _voices[voiceIndex];

		// TODO: Implement BRR decoding, envelope, interpolation
		// This is a placeholder returning silence
		return 0;
	}

	private sealed class Voice {
		public bool Playing { get; private set; }
		public byte PitchL { get; set; }
		public byte PitchH { get; set; }
		public byte Srcn { get; set; }
		public byte Adsr1 { get; set; }
		public byte Adsr2 { get; set; }
		public byte Gain { get; set; }

		// Internal state
		private int _brrOffset;
		private int _envLevel;
		private EnvelopeState _envState;

		public void KeyOn() {
			Playing = true;
			_brrOffset = 0;
			_envLevel = 0;
			_envState = EnvelopeState.Attack;
		}

		public void KeyOff() {
			_envState = EnvelopeState.Release;
		}

		private enum EnvelopeState {
			Attack,
			Decay,
			Sustain,
			Release
		}
	}
}
