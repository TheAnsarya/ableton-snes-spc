namespace SpcPlugin.Core.Audio;

/// <summary>
/// WAV file parser for importing audio samples.
/// Supports standard PCM WAV files (8, 16, 24, 32-bit) and 32-bit float.
/// </summary>
public sealed class WavFile {
	/// <summary>Sample rate in Hz.</summary>
	public int SampleRate { get; private set; }

	/// <summary>Number of audio channels.</summary>
	public int Channels { get; private set; }

	/// <summary>Bits per sample in original file.</summary>
	public int BitsPerSample { get; private set; }

	/// <summary>Audio format (1 = PCM, 3 = IEEE Float).</summary>
	public int AudioFormat { get; private set; }

	/// <summary>Total number of samples per channel.</summary>
	public int SampleCount { get; private set; }

	/// <summary>Duration in seconds.</summary>
	public double Duration => SampleRate > 0 ? (double)SampleCount / SampleRate : 0;

	/// <summary>Audio data as normalized 16-bit samples (interleaved if multi-channel).</summary>
	public short[] Samples { get; private set; } = [];

	/// <summary>Audio data as mono 16-bit samples (mixed down if multi-channel).</summary>
	public short[] MonoSamples => GetMonoSamples();

	/// <summary>
	/// Loads a WAV file from disk.
	/// </summary>
	/// <param name="path">Path to WAV file.</param>
	/// <returns>Parsed WAV file.</returns>
	public static WavFile Load(string path) {
		var data = File.ReadAllBytes(path);
		return Parse(data);
	}

	/// <summary>
	/// Parses WAV data from a byte array.
	/// </summary>
	/// <param name="data">WAV file data.</param>
	/// <returns>Parsed WAV file.</returns>
	public static WavFile Parse(byte[] data) {
		var wav = new WavFile();
		wav.ParseInternal(data);
		return wav;
	}

	/// <summary>
	/// Creates a WAV file from 16-bit PCM samples.
	/// </summary>
	/// <param name="samples">16-bit PCM samples.</param>
	/// <param name="sampleRate">Sample rate in Hz.</param>
	/// <param name="channels">Number of channels (1 = mono, 2 = stereo).</param>
	/// <returns>WAV file data as byte array.</returns>
	public static byte[] Create(short[] samples, int sampleRate = 44100, int channels = 1) {
		int dataSize = samples.Length * 2;
		int fileSize = 44 + dataSize;
		var wav = new byte[fileSize];

		// RIFF header
		wav[0] = (byte)'R';
		wav[1] = (byte)'I';
		wav[2] = (byte)'F';
		wav[3] = (byte)'F';
		WriteInt32(wav, 4, fileSize - 8);
		wav[8] = (byte)'W';
		wav[9] = (byte)'A';
		wav[10] = (byte)'V';
		wav[11] = (byte)'E';

		// fmt chunk
		wav[12] = (byte)'f';
		wav[13] = (byte)'m';
		wav[14] = (byte)'t';
		wav[15] = (byte)' ';
		WriteInt32(wav, 16, 16);          // chunk size
		WriteInt16(wav, 20, 1);           // audio format (PCM)
		WriteInt16(wav, 22, (short)channels);
		WriteInt32(wav, 24, sampleRate);
		WriteInt32(wav, 28, sampleRate * channels * 2);  // byte rate
		WriteInt16(wav, 32, (short)(channels * 2));      // block align
		WriteInt16(wav, 34, 16);          // bits per sample

		// data chunk
		wav[36] = (byte)'d';
		wav[37] = (byte)'a';
		wav[38] = (byte)'t';
		wav[39] = (byte)'a';
		WriteInt32(wav, 40, dataSize);

		// Sample data
		for (int i = 0; i < samples.Length; i++) {
			WriteInt16(wav, 44 + i * 2, samples[i]);
		}

		return wav;
	}

	private void ParseInternal(byte[] data) {
		if (data.Length < 44) {
			throw new InvalidDataException("File too small to be a valid WAV file");
		}

		// Check RIFF header
		if (data[0] != 'R' || data[1] != 'I' || data[2] != 'F' || data[3] != 'F') {
			throw new InvalidDataException("Not a valid RIFF file");
		}

		// Check WAVE format
		if (data[8] != 'W' || data[9] != 'A' || data[10] != 'V' || data[11] != 'E') {
			throw new InvalidDataException("Not a valid WAVE file");
		}

		// Parse chunks
		int offset = 12;
		int dataOffset = -1;
		int dataSize = 0;

		while (offset + 8 <= data.Length) {
			string chunkId = System.Text.Encoding.ASCII.GetString(data, offset, 4);
			int chunkSize = ReadInt32(data, offset + 4);

			if (chunkId == "fmt ") {
				ParseFmtChunk(data, offset + 8, chunkSize);
			} else if (chunkId == "data") {
				dataOffset = offset + 8;
				dataSize = chunkSize;
			}

			offset += 8 + chunkSize;

			// Align to word boundary
			if (offset % 2 != 0 && offset < data.Length) {
				offset++;
			}
		}

		if (dataOffset < 0) {
			throw new InvalidDataException("No data chunk found in WAV file");
		}

		// Parse sample data
		ParseSampleData(data, dataOffset, dataSize);
	}

	private void ParseFmtChunk(byte[] data, int offset, int size) {
		if (size < 16) {
			throw new InvalidDataException("fmt chunk too small");
		}

		AudioFormat = ReadInt16(data, offset);
		Channels = ReadInt16(data, offset + 2);
		SampleRate = ReadInt32(data, offset + 4);
		// byte rate at offset + 8
		// block align at offset + 12
		BitsPerSample = ReadInt16(data, offset + 14);

		if (AudioFormat != 1 && AudioFormat != 3) {
			throw new InvalidDataException($"Unsupported audio format: {AudioFormat} (only PCM and IEEE Float supported)");
		}

		if (Channels < 1 || Channels > 8) {
			throw new InvalidDataException($"Unsupported channel count: {Channels}");
		}
	}

	private void ParseSampleData(byte[] data, int offset, int size) {
		int bytesPerSample = BitsPerSample / 8;
		int totalSamples = size / bytesPerSample;
		SampleCount = totalSamples / Channels;

		Samples = new short[totalSamples];

		for (int i = 0; i < totalSamples; i++) {
			int sampleOffset = offset + i * bytesPerSample;
			if (sampleOffset + bytesPerSample > data.Length) break;

			Samples[i] = AudioFormat == 3
				? ConvertFloatSample(data, sampleOffset, bytesPerSample)
				: ConvertPcmSample(data, sampleOffset, bytesPerSample);
		}
	}

	private short ConvertPcmSample(byte[] data, int offset, int bytesPerSample) {
		return bytesPerSample switch {
			1 => (short)((data[offset] - 128) * 256),  // 8-bit unsigned
			2 => (short)(data[offset] | (data[offset + 1] << 8)),  // 16-bit signed
			3 => (short)((data[offset + 1] | (data[offset + 2] << 8))),  // 24-bit (use high 16 bits)
			4 => (short)((data[offset + 2] | (data[offset + 3] << 8))),  // 32-bit (use high 16 bits)
			_ => throw new InvalidDataException($"Unsupported bits per sample: {BitsPerSample}"),
		};
	}

	private short ConvertFloatSample(byte[] data, int offset, int bytesPerSample) {
		float value = bytesPerSample switch {
			4 => BitConverter.ToSingle(data, offset),
			8 => (float)BitConverter.ToDouble(data, offset),
			_ => throw new InvalidDataException($"Unsupported float format: {bytesPerSample * 8}-bit"),
		};

		// Clamp and convert to 16-bit
		value = Math.Clamp(value, -1.0f, 1.0f);
		return (short)(value * 32767);
	}

	private short[] GetMonoSamples() {
		if (Channels == 1) return Samples;

		var mono = new short[SampleCount];
		for (int i = 0; i < SampleCount; i++) {
			int sum = 0;
			for (int ch = 0; ch < Channels; ch++) {
				sum += Samples[i * Channels + ch];
			}
			mono[i] = (short)(sum / Channels);
		}
		return mono;
	}

	private static int ReadInt16(byte[] data, int offset) {
		return data[offset] | (data[offset + 1] << 8);
	}

	private static int ReadInt32(byte[] data, int offset) {
		return data[offset] |
			   (data[offset + 1] << 8) |
			   (data[offset + 2] << 16) |
			   (data[offset + 3] << 24);
	}

	private static void WriteInt16(byte[] data, int offset, short value) {
		data[offset] = (byte)value;
		data[offset + 1] = (byte)(value >> 8);
	}

	private static void WriteInt32(byte[] data, int offset, int value) {
		data[offset] = (byte)value;
		data[offset + 1] = (byte)(value >> 8);
		data[offset + 2] = (byte)(value >> 16);
		data[offset + 3] = (byte)(value >> 24);
	}
}
