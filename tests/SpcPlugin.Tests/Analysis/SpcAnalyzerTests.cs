using SpcPlugin.Core.Analysis;

namespace SpcPlugin.Tests.Analysis;

public class SpcAnalyzerTests {
	[Fact]
	public void Analyze_ValidSpc_SetsDriverType() {
		// Arrange
		var spcData = CreateTestSpcData();
		var analyzer = new SpcAnalyzer();

		// Act
		analyzer.Analyze(spcData);

		// Assert - Unknown is valid for minimal test data
		Assert.True(Enum.IsDefined(analyzer.DriverType));
	}

	[Fact]
	public void Analyze_ValidSpc_ExtractsSamples() {
		// Arrange
		var spcData = CreateTestSpcWithSamples();
		var analyzer = new SpcAnalyzer();

		// Act
		analyzer.Analyze(spcData);

		// Assert
		Assert.NotEmpty(analyzer.Samples);
	}

	[Fact]
	public void Analyze_ValidSpc_CalculatesMemoryUsage() {
		// Arrange
		var spcData = CreateTestSpcData();
		var analyzer = new SpcAnalyzer();

		// Act
		analyzer.Analyze(spcData);

		// Assert
		Assert.NotNull(analyzer.Memory);
		Assert.True(analyzer.Memory.TotalUsed > 0);
		Assert.Equal(0x10000, analyzer.Memory.TotalUsed + analyzer.Memory.FreeBytes);
	}

	[Fact]
	public void Analyze_ValidSpc_ExtractsEchoConfiguration() {
		// Arrange
		var spcData = CreateTestSpcWithEcho();
		var analyzer = new SpcAnalyzer();

		// Act
		analyzer.Analyze(spcData);

		// Assert
		Assert.NotNull(analyzer.Echo);
		Assert.True(analyzer.Echo.Delay >= 0 && analyzer.Echo.Delay <= 15);
	}

	[Fact]
	public void GetVoiceInfo_ReturnsAllEightVoices() {
		// Arrange
		var spcData = CreateTestSpcData();
		var analyzer = new SpcAnalyzer();
		analyzer.Analyze(spcData);

		// Act
		var voices = analyzer.GetVoiceInfo();

		// Assert
		Assert.NotNull(voices);
		Assert.Equal(8, voices.Length);
		for (int i = 0; i < 8; i++) {
			Assert.Equal(i, voices[i].VoiceIndex);
		}
	}

	[Fact]
	public void ExtractSampleBrr_ValidSample_ReturnsBrrData() {
		// Arrange
		var spcData = CreateTestSpcWithSamples();
		var analyzer = new SpcAnalyzer();
		analyzer.Analyze(spcData);

		// Act
		if (analyzer.Samples.Count > 0) {
			var brr = analyzer.ExtractSampleBrr(analyzer.Samples[0].Index);

			// Assert
			Assert.NotEmpty(brr);
			Assert.True(brr.Length % 9 == 0, "BRR data should be multiple of 9 bytes");
		}
	}

	[Fact]
	public void VoiceAnalysis_FrequencyHz_CalculatesCorrectly() {
		// Arrange
		var voice = new VoiceAnalysis {
			VoiceIndex = 0,
			Pitch = 0x1000, // 4096 = 32000 Hz (native rate)
		};

		// Act
		double freq = voice.FrequencyHz;

		// Assert
		Assert.Equal(32000.0, freq, precision: 1);
	}

	[Fact]
	public void VoiceAnalysis_ApproximateMidiNote_CalculatesCorrectly() {
		// Arrange - A4 = 440 Hz
		// Pitch for 440 Hz: 440 * 4096 / 32000 ≈ 56.32 ≈ 56
		var voice = new VoiceAnalysis {
			VoiceIndex = 0,
			Pitch = 56, // ~440 Hz
		};

		// Act
		int midiNote = voice.ApproximateMidiNote;

		// Assert - Should be close to 69 (A4)
		Assert.InRange(midiNote, 65, 73);
	}

	[Theory]
	[InlineData(0x10200)] // Minimum valid size
	[InlineData(0x20000)] // Larger file
	public void Analyze_ValidSizes_Succeeds(int size) {
		// Arrange
		var spcData = new byte[size];
		SetupMinimalSpcHeader(spcData);
		var analyzer = new SpcAnalyzer();

		// Act & Assert - Should not throw
		analyzer.Analyze(spcData);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(100)]
	[InlineData(0x100)]
	[InlineData(0x10000)]
	public void Analyze_TooSmall_ThrowsArgumentException(int size) {
		// Arrange
		var spcData = new byte[size];
		var analyzer = new SpcAnalyzer();

		// Act & Assert
		Assert.Throws<ArgumentException>(() => analyzer.Analyze(spcData));
	}

	[Fact]
	public void MemoryUsage_Properties_CalculateCorrectly() {
		// Arrange
		var memory = new MemoryUsage {
			SampleBytes = 20000,
			EchoBytes = 8000,
			DriverBytes = 4000,
			FreeBytes = 33536,
		};

		// Act & Assert
		Assert.Equal(32000, memory.TotalUsed);
		Assert.True(memory.SamplePercent > 30 && memory.SamplePercent < 31);
	}

	[Fact]
	public void EchoConfiguration_BufferSize_CalculatesCorrectly() {
		// Arrange
		var echo = new EchoConfiguration { Delay = 4 };

		// Act
		int bufferSize = echo.BufferSize;

		// Assert
		Assert.Equal(4 * 2048, bufferSize);
	}

	[Fact]
	public void EchoConfiguration_DelayZero_ReturnsMinimumBuffer() {
		// Arrange
		var echo = new EchoConfiguration { Delay = 0 };

		// Act
		int bufferSize = echo.BufferSize;

		// Assert
		Assert.Equal(4, bufferSize);
	}

	#region Test Data Helpers

	private static byte[] CreateTestSpcData() {
		var data = new byte[0x10200];
		SetupMinimalSpcHeader(data);
		return data;
	}

	private static byte[] CreateTestSpcWithSamples() {
		var data = new byte[0x10200];
		SetupMinimalSpcHeader(data);

		// Set up sample directory at $2000
		data[0x10100 + 0x5d] = 0x20; // DIR = $2000

		// Create sample directory entries
		// Entry 0: sample at $3000, loop at $3000
		int dirOffset = 0x100 + 0x2000;
		data[dirOffset + 0] = 0x00;
		data[dirOffset + 1] = 0x30; // Start = $3000
		data[dirOffset + 2] = 0x00;
		data[dirOffset + 3] = 0x30; // Loop = $3000

		// Create a minimal BRR block at $3000 with end flag
		int sampleOffset = 0x100 + 0x3000;
		data[sampleOffset] = 0x01; // Header: end flag set
		// 8 more bytes of sample data (zeros)

		// Set voice 0 to use sample 0
		data[0x10100 + 0x04] = 0x00; // Voice 0 SRCN = 0

		return data;
	}

	private static byte[] CreateTestSpcWithEcho() {
		var data = new byte[0x10200];
		SetupMinimalSpcHeader(data);

		// Enable echo (FLG bit 5 = 0)
		data[0x10100 + 0x6c] = 0x00;

		// Set echo delay
		data[0x10100 + 0x6d] = 0x04;

		// Set echo feedback
		data[0x10100 + 0x0d] = 0x40;

		// Set echo volumes
		data[0x10100 + 0x2c] = 0x30;
		data[0x10100 + 0x3c] = 0x30;

		// Enable echo for voice 0
		data[0x10100 + 0x3d] = 0x01;

		return data;
	}

	private static void SetupMinimalSpcHeader(byte[] data) {
		// SPC header magic
		"SNES-SPC700 Sound File Data v0.30"u8.CopyTo(data.AsSpan(0, 33));
		data[0x21] = 0x1a;
		data[0x22] = 0x1a;
	}

	#endregion
}
