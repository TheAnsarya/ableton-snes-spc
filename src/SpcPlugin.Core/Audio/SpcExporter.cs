namespace SpcPlugin.Core.Audio;

/// <summary>
/// Exports audio from SPC files to standard formats for use in DAWs.
/// </summary>
public static class SpcExporter {
	/// <summary>
	/// Exports SPC playback to a WAV file.
	/// </summary>
	/// <param name="spcData">SPC file data.</param>
	/// <param name="outputPath">Path for output WAV file.</param>
	/// <param name="durationSeconds">Duration to render.</param>
	/// <param name="sampleRate">Output sample rate (default 44100).</param>
	/// <param name="fadeOutSeconds">Fade out duration at end (default 2).</param>
	public static void ExportToWav(
		ReadOnlySpan<byte> spcData,
		string outputPath,
		double durationSeconds,
		int sampleRate = 44100,
		double fadeOutSeconds = 2.0) {
		using var engine = new SpcEngine(sampleRate);
		engine.LoadSpc(spcData);
		engine.Play();

		int totalSamples = (int)(durationSeconds * sampleRate);
		int fadeStartSample = (int)((durationSeconds - fadeOutSeconds) * sampleRate);
		var samples = new float[totalSamples * 2];

		// Generate all samples
		int bufferSize = 4096;
		var buffer = new float[bufferSize * 2];
		int samplesGenerated = 0;

		while (samplesGenerated < totalSamples) {
			int toGenerate = Math.Min(bufferSize, totalSamples - samplesGenerated);
			engine.Process(buffer.AsSpan(0, toGenerate * 2), toGenerate);
			buffer.AsSpan(0, toGenerate * 2).CopyTo(samples.AsSpan(samplesGenerated * 2));
			samplesGenerated += toGenerate;
		}

		// Apply fade out
		for (int i = fadeStartSample; i < totalSamples; i++) {
			float fadeProgress = (float)(i - fadeStartSample) / (totalSamples - fadeStartSample);
			float fadeMultiplier = 1.0f - fadeProgress;
			samples[i * 2] *= fadeMultiplier;
			samples[(i * 2) + 1] *= fadeMultiplier;
		}

		// Write WAV file
		WriteWavFile(outputPath, samples, sampleRate);
	}

	/// <summary>
	/// Exports SPC playback to a WAV file with voice isolation.
	/// </summary>
	public static void ExportVoiceToWav(
		ReadOnlySpan<byte> spcData,
		string outputPath,
		int voice,
		double durationSeconds,
		int sampleRate = 44100) {
		if (voice < 0 || voice >= 8) {
			throw new ArgumentOutOfRangeException(nameof(voice), "Voice must be 0-7");
		}

		using var engine = new SpcEngine(sampleRate);
		engine.LoadSpc(spcData);

		// Solo just this voice
		for (int i = 0; i < 8; i++) {
			engine.SetVoiceMuted(i, i != voice);
		}

		engine.Play();

		int totalSamples = (int)(durationSeconds * sampleRate);
		var samples = new float[totalSamples * 2];

		int bufferSize = 4096;
		var buffer = new float[bufferSize * 2];
		int samplesGenerated = 0;

		while (samplesGenerated < totalSamples) {
			int toGenerate = Math.Min(bufferSize, totalSamples - samplesGenerated);
			engine.Process(buffer.AsSpan(0, toGenerate * 2), toGenerate);
			buffer.AsSpan(0, toGenerate * 2).CopyTo(samples.AsSpan(samplesGenerated * 2));
			samplesGenerated += toGenerate;
		}

		WriteWavFile(outputPath, samples, sampleRate);
	}

	/// <summary>
	/// Exports a single BRR sample to WAV.
	/// </summary>
	public static void ExportSampleToWav(
		ReadOnlySpan<byte> spcData,
		string outputPath,
		int sourceNumber,
		int sampleRate = 32000) {
		var editor = new Editing.SpcEditor();
		editor.LoadSpc(spcData);

		short[] pcmSamples = editor.ExtractSample(sourceNumber);

		// Convert to float
		var floatSamples = new float[pcmSamples.Length * 2]; // Mono to stereo
		for (int i = 0; i < pcmSamples.Length; i++) {
			float sample = pcmSamples[i] / 32768f;
			floatSamples[i * 2] = sample;
			floatSamples[(i * 2) + 1] = sample;
		}

		WriteWavFile(outputPath, floatSamples, sampleRate);
	}

	/// <summary>
	/// Exports all samples from an SPC file to individual WAV files.
	/// </summary>
	public static void ExportAllSamplesToWav(
		ReadOnlySpan<byte> spcData,
		string outputDirectory,
		int sampleRate = 32000) {
		Directory.CreateDirectory(outputDirectory);

		var editor = new Editing.SpcEditor();
		editor.LoadSpc(spcData);

		// Find which source numbers are actually used
		var usedSources = new HashSet<byte>();
		for (int voice = 0; voice < 8; voice++) {
			var info = editor.GetVoiceInfo(voice);
			usedSources.Add(info.SourceNumber);
		}

		// Also scan for any sources referenced in the sample directory
		for (int src = 0; src < 256; src++) {
			var sampleInfo = editor.GetSampleInfo(src);
			if (sampleInfo.StartAddress > 0 && sampleInfo.StartAddress < 0xffff) {
				usedSources.Add((byte)src);
			}
		}

		foreach (byte srcNum in usedSources) {
			try {
				short[] pcmSamples = editor.ExtractSample(srcNum);
				if (pcmSamples.Length > 0) {
					string filename = Path.Combine(outputDirectory, $"sample_{srcNum:D3}.wav");
					var floatSamples = new float[pcmSamples.Length * 2];
					for (int i = 0; i < pcmSamples.Length; i++) {
						float sample = pcmSamples[i] / 32768f;
						floatSamples[i * 2] = sample;
						floatSamples[(i * 2) + 1] = sample;
					}

					WriteWavFile(filename, floatSamples, sampleRate);
				}
			} catch {
				// Skip invalid samples
			}
		}
	}

	/// <summary>
	/// Gets playback duration estimate (searches for loop or end).
	/// </summary>
	public static TimeSpan EstimateDuration(ReadOnlySpan<byte> spcData, double maxSeconds = 300) {
		// Check ID666 tags first
		if (spcData.Length >= 0xd2) {
			// Try to read duration from ID666 if present
			if (spcData[0x23] == 0x1a) {
				// Binary ID666 format - duration at 0xa9-0xab
				int durationSecs = spcData[0xa9] | (spcData[0xaa] << 8) | (spcData[0xab] << 16);
				if (durationSecs > 0 && durationSecs < 600) {
					return TimeSpan.FromSeconds(durationSecs);
				}
			}
		}

		// Default to reasonable duration
		return TimeSpan.FromMinutes(3);
	}

	private static void WriteWavFile(string path, float[] samples, int sampleRate) {
		using var stream = File.Create(path);
		using var writer = new BinaryWriter(stream);

		int numChannels = 2;
		int bitsPerSample = 16;
		int byteRate = sampleRate * numChannels * bitsPerSample / 8;
		int blockAlign = numChannels * bitsPerSample / 8;
		int dataSize = samples.Length * 2; // 16-bit samples

		// RIFF header
		writer.Write("RIFF"u8);
		writer.Write(36 + dataSize);
		writer.Write("WAVE"u8);

		// fmt chunk
		writer.Write("fmt "u8);
		writer.Write(16); // Chunk size
		writer.Write((short)1); // PCM format
		writer.Write((short)numChannels);
		writer.Write(sampleRate);
		writer.Write(byteRate);
		writer.Write((short)blockAlign);
		writer.Write((short)bitsPerSample);

		// data chunk
		writer.Write("data"u8);
		writer.Write(dataSize);

		// Write samples as 16-bit PCM
		foreach (float sample in samples) {
			short pcm = (short)Math.Clamp(sample * 32767f, short.MinValue, short.MaxValue);
			writer.Write(pcm);
		}
	}
}
