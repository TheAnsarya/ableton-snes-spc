using SpcPlugin.Core.Audio;

namespace SpcPlugin.Tests.Audio;

/// <summary>
/// Tests for WAV file parsing.
/// </summary>
public class WavFileTests {
	[Fact]
	public void Create_GeneratesValidWavHeader() {
		// Arrange
		var samples = new short[] { 0, 1000, 2000, 3000, 4000, 5000, 6000, 7000 };

		// Act
		var wavData = WavFile.Create(samples, 44100, 1);

		// Assert
		Assert.True(wavData.Length >= 44 + samples.Length * 2);

		// Check RIFF header
		Assert.Equal((byte)'R', wavData[0]);
		Assert.Equal((byte)'I', wavData[1]);
		Assert.Equal((byte)'F', wavData[2]);
		Assert.Equal((byte)'F', wavData[3]);

		// Check WAVE format
		Assert.Equal((byte)'W', wavData[8]);
		Assert.Equal((byte)'A', wavData[9]);
		Assert.Equal((byte)'V', wavData[10]);
		Assert.Equal((byte)'E', wavData[11]);
	}

	[Fact]
	public void Parse_RoundTrip_PreservesData() {
		// Arrange
		var original = new short[] { -32768, -16384, 0, 16383, 32767 };

		// Act
		var wavData = WavFile.Create(original, 32000, 1);
		var parsed = WavFile.Parse(wavData);

		// Assert
		Assert.Equal(32000, parsed.SampleRate);
		Assert.Equal(1, parsed.Channels);
		Assert.Equal(16, parsed.BitsPerSample);
		Assert.Equal(original.Length, parsed.SampleCount);
		Assert.Equal(original, parsed.Samples);
	}

	[Fact]
	public void MonoSamples_StereoInput_MixesDown() {
		// Arrange - Create stereo WAV
		var stereoSamples = new short[] {
			1000, 2000,  // Frame 1: L=1000, R=2000
			3000, 4000,  // Frame 2: L=3000, R=4000
		};
		var wavData = WavFile.Create(stereoSamples, 44100, 2);

		// Act
		var parsed = WavFile.Parse(wavData);
		var mono = parsed.MonoSamples;

		// Assert
		Assert.Equal(2, parsed.SampleCount);
		Assert.Equal(2, mono.Length);
		Assert.Equal(1500, mono[0]); // (1000 + 2000) / 2
		Assert.Equal(3500, mono[1]); // (3000 + 4000) / 2
	}

	[Fact]
	public void Parse_InvalidData_ThrowsException() {
		// Arrange
		var invalidData = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };

		// Act & Assert
		Assert.Throws<InvalidDataException>(() => WavFile.Parse(invalidData));
	}

	[Fact]
	public void Duration_CalculatesCorrectly() {
		// Arrange
		var samples = new short[32000]; // 1 second at 32kHz

		// Act
		var wavData = WavFile.Create(samples, 32000, 1);
		var parsed = WavFile.Parse(wavData);

		// Assert
		Assert.Equal(1.0, parsed.Duration, 3);
	}
}
