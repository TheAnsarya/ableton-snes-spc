using SpcPlugin.Core.Editing;

namespace SpcPlugin.Tests.Editing;

/// <summary>
/// Tests for loop point editing functionality.
/// </summary>
public class LoopPointEditorTests {
	[Fact]
	public void LoadSamples_SetsCorrectLength() {
		// Arrange
		var editor = new LoopPointEditor();
		var samples = new short[160]; // 10 blocks

		// Act
		editor.LoadSamples(samples);

		// Assert
		Assert.True(editor.HasSample);
		Assert.Equal(160, editor.SampleCount);
		Assert.Equal(10, editor.BlockCount);
	}

	[Fact]
	public void LoadSamples_WithLoop_AlignsToBlocks() {
		// Arrange
		var editor = new LoopPointEditor();
		var samples = new short[160];

		// Act - Loop at sample 50 (should align to 48)
		editor.LoadSamples(samples, loopStart: 50);

		// Assert
		Assert.True(editor.HasLoop);
		Assert.Equal(48, editor.LoopStart); // Aligned to block 3
		Assert.Equal(3, editor.LoopStartBlock);
	}

	[Fact]
	public void SetLoopStart_AlignsToBlockBoundary() {
		// Arrange
		var editor = new LoopPointEditor();
		editor.LoadSamples(new short[320], loopStart: 0);

		// Act
		editor.SetLoopStart(100); // Should align to 96

		// Assert
		Assert.Equal(96, editor.LoopStart);
		Assert.Equal(6, editor.LoopStartBlock);
	}

	[Fact]
	public void SetLoopEnd_EnsuresAfterLoopStart() {
		// Arrange
		var editor = new LoopPointEditor();
		editor.LoadSamples(new short[320], loopStart: 160);

		// Act - Try to set loop end before loop start
		editor.SetLoopEnd(80);

		// Assert - Should be at least one block after start
		Assert.True(editor.LoopEnd > editor.LoopStart);
	}

	[Fact]
	public void DisableLoop_ClearsLoopPoints() {
		// Arrange
		var editor = new LoopPointEditor();
		editor.LoadSamples(new short[160], loopStart: 32);
		Assert.True(editor.HasLoop);

		// Act
		editor.DisableLoop();

		// Assert
		Assert.False(editor.HasLoop);
		Assert.Equal(-1, editor.LoopStart);
	}

	[Fact]
	public void GetWaveform_ReturnsCorrectSize() {
		// Arrange
		var editor = new LoopPointEditor();
		var samples = new short[320];
		for (int i = 0; i < samples.Length; i++) {
			samples[i] = (short)(Math.Sin(i * 0.1) * 10000);
		}
		editor.LoadSamples(samples);

		// Act
		var waveform = editor.GetWaveform(100);

		// Assert
		Assert.Equal(100, waveform.Samples.Length);
	}

	[Fact]
	public void GetWaveform_WithLoop_IncludesMarkers() {
		// Arrange
		var editor = new LoopPointEditor();
		editor.LoadSamples(new short[320], loopStart: 80);

		// Act
		var waveform = editor.GetWaveform(100);

		// Assert
		Assert.True(waveform.HasLoop);
		Assert.True(waveform.LoopStartX >= 0);
		Assert.True(waveform.LoopEndX > waveform.LoopStartX);
	}

	[Fact]
	public void LoopPointChanged_RaisesEvent() {
		// Arrange
		var editor = new LoopPointEditor();
		editor.LoadSamples(new short[320], loopStart: 0);
		var eventRaised = false;
		editor.LoopPointChanged += (_, _) => eventRaised = true;

		// Act
		editor.SetLoopStart(64);

		// Assert
		Assert.True(eventRaised);
	}

	[Fact]
	public void ExportBrr_ProducesValidData() {
		// Arrange
		var editor = new LoopPointEditor();
		var samples = new short[160];
		for (int i = 0; i < samples.Length; i++) {
			samples[i] = (short)(Math.Sin(i * 0.1) * 10000);
		}
		editor.LoadSamples(samples, loopStart: 32);

		// Act
		var brr = editor.ExportBrr();

		// Assert
		Assert.NotEmpty(brr);
		Assert.Equal(0, brr.Length % 9);
	}
}
