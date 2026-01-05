using SpcPlugin.Core.Midi;

namespace SpcPlugin.Tests;

public class MidiTests {
	[Fact]
	public void MidiEvent_NoteOn_CreatesCorrectEvent() {
		var evt = MidiEvent.NoteOn(channel: 0, note: 60, velocity: 100);

		Assert.Equal(MidiEventType.NoteOn, evt.Type);
		Assert.Equal(0, evt.Channel);
		Assert.Equal(60, evt.Note);
		Assert.Equal(100, evt.Velocity);
	}

	[Fact]
	public void MidiEvent_NoteOff_CreatesCorrectEvent() {
		var evt = MidiEvent.NoteOff(channel: 1, note: 72, velocity: 64);

		Assert.Equal(MidiEventType.NoteOff, evt.Type);
		Assert.Equal(1, evt.Channel);
		Assert.Equal(72, evt.Note);
		Assert.Equal(64, evt.Velocity);
	}

	[Fact]
	public void MidiEvent_ControlChange_CreatesCorrectEvent() {
		var evt = MidiEvent.ControlChange(channel: 0, controller: 7, value: 100);

		Assert.Equal(MidiEventType.ControlChange, evt.Type);
		Assert.Equal(7, evt.Controller);
		Assert.Equal(100, evt.Value);
	}

	[Fact]
	public void MidiEvent_PitchBend_CreatesCorrectEvent() {
		var evt = MidiEvent.PitchBend(channel: 0, value: 0); // Center

		Assert.Equal(MidiEventType.PitchBend, evt.Type);
		Assert.Equal(0, evt.PitchBendValue);
	}

	[Fact]
	public void MidiEvent_PitchBend_PositiveValue() {
		var evt = MidiEvent.PitchBend(channel: 0, value: 8191); // Max up

		Assert.Equal(MidiEventType.PitchBend, evt.Type);
		Assert.Equal(8191, evt.PitchBendValue);
	}

	[Fact]
	public void MidiEvent_PitchBend_NegativeValue() {
		var evt = MidiEvent.PitchBend(channel: 0, value: -8192); // Max down

		Assert.Equal(MidiEventType.PitchBend, evt.Type);
		Assert.Equal(-8192, evt.PitchBendValue);
	}

	[Theory]
	[InlineData(60, 0)]
	[InlineData(61, 1)]
	[InlineData(62, 2)]
	[InlineData(67, 7)]
	public void MidiNoteMap_NoteToVoice_MapsCorrectly(int note, int expectedVoice) {
		int voice = MidiNoteMap.NoteToVoice(note);
		Assert.Equal(expectedVoice, voice);
	}

	[Theory]
	[InlineData(59)]  // Below range
	[InlineData(68)]  // Above range
	[InlineData(0)]   // Far below
	[InlineData(127)] // Far above
	public void MidiNoteMap_NoteToVoice_ReturnsNegativeForOutOfRange(int note) {
		int voice = MidiNoteMap.NoteToVoice(note);
		Assert.Equal(-1, voice);
	}

	[Theory]
	[InlineData(0, 60)]
	[InlineData(1, 61)]
	[InlineData(7, 67)]
	public void MidiNoteMap_VoiceToNote_MapsCorrectly(int voice, int expectedNote) {
		int note = MidiNoteMap.VoiceToNote(voice);
		Assert.Equal(expectedNote, note);
	}

	[Fact]
	public void MidiControllers_HasExpectedValues() {
		Assert.Equal(1, MidiControllers.ModWheel);
		Assert.Equal(7, MidiControllers.Volume);
		Assert.Equal(10, MidiControllers.Pan);
		Assert.Equal(64, MidiControllers.Sustain);
		Assert.Equal(102, MidiControllers.VoiceMute);
		Assert.Equal(103, MidiControllers.VoiceSolo);
	}
}
