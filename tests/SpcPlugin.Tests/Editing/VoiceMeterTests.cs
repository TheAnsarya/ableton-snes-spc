using SpcPlugin.Core.Editing;

namespace SpcPlugin.Tests.Editing;

/// <summary>
/// Tests for voice metering functionality.
/// </summary>
public class VoiceMeterTests {
	[Fact]
	public void ProcessVoiceOutputs_UpdatesPeakLevels() {
		// Arrange
		var meter = new VoiceMeter();
		var outputs = new float[8][];
		for (int i = 0; i < 8; i++) {
			outputs[i] = new float[64];
			outputs[i][32] = 0.5f; // Peak at sample 32
		}

		// Act
		meter.ProcessVoiceOutputs(outputs, 64);

		// Assert
		for (int i = 0; i < 8; i++) {
			Assert.True(meter.GetPeakLevel(i) >= 0.4f);
		}
	}

	[Fact]
	public void ProcessVoiceOutputs_DetectsClipping() {
		// Arrange
		var meter = new VoiceMeter();
		var outputs = new float[8][];
		for (int i = 0; i < 8; i++) {
			outputs[i] = new float[64];
		}
		outputs[0][10] = 1.0f; // Clip voice 0

		// Act
		meter.ProcessVoiceOutputs(outputs, 64);

		// Assert
		Assert.True(meter.IsClipping(0));
		Assert.False(meter.IsClipping(1));
	}

	[Fact]
	public void Reset_ClearsAllLevels() {
		// Arrange
		var meter = new VoiceMeter();
		var outputs = new float[8][];
		for (int i = 0; i < 8; i++) {
			outputs[i] = new float[64];
			outputs[i][0] = 0.8f;
		}
		meter.ProcessVoiceOutputs(outputs, 64);

		// Act
		meter.Reset();

		// Assert
		for (int i = 0; i < 8; i++) {
			Assert.Equal(0, meter.GetPeakLevel(i));
			Assert.Equal(0, meter.GetRmsLevel(i));
		}
	}

	[Fact]
	public void GetSnapshot_ReturnsAllValues() {
		// Arrange
		var meter = new VoiceMeter();
		var outputs = new float[8][];
		for (int i = 0; i < 8; i++) {
			outputs[i] = new float[64];
			outputs[i][0] = (i + 1) * 0.1f;
		}
		meter.ProcessVoiceOutputs(outputs, 64);

		// Act
		var snapshot = meter.GetSnapshot();

		// Assert
		Assert.Equal(8, snapshot.PeakLevels.Length);
		Assert.Equal(8, snapshot.RmsLevels.Length);
		Assert.Equal(8, snapshot.PeakHold.Length);
		Assert.Equal(8, snapshot.Clipping.Length);
	}

	[Fact]
	public void LinearToDb_ConvertsCorrectly() {
		// Assert
		Assert.Equal(0, VoiceMeter.LinearToDb(1.0f), 1);
		Assert.Equal(-6, VoiceMeter.LinearToDb(0.5f), 1);
		Assert.Equal(-20, VoiceMeter.LinearToDb(0.1f), 1);
	}

	[Fact]
	public void DbToLinear_ConvertsCorrectly() {
		// Assert
		Assert.Equal(1.0f, VoiceMeter.DbToLinear(0), 0.01f);
		Assert.Equal(0.5f, VoiceMeter.DbToLinear(-6), 0.05f);
		Assert.Equal(0.1f, VoiceMeter.DbToLinear(-20), 0.01f);
	}

	[Fact]
	public void LevelsUpdated_RaisesEvent() {
		// Arrange
		var meter = new VoiceMeter();
		var eventRaised = false;
		meter.LevelsUpdated += (_, _) => eventRaised = true;

		var outputs = new float[8][];
		for (int i = 0; i < 8; i++) {
			outputs[i] = new float[64];
		}

		// Act
		meter.ProcessVoiceOutputs(outputs, 64);

		// Assert
		Assert.True(eventRaised);
	}
}
