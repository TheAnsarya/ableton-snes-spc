namespace SpcPlugin.Core.Editing;

/// <summary>
/// Base class for SPC memory editing commands.
/// </summary>
public abstract class SpcEditCommand : IEditCommand {
	protected readonly byte[] Memory;
	protected readonly int Address;
	protected readonly byte[] OldData;
	protected readonly byte[] NewData;

	public abstract string Description { get; }

	protected SpcEditCommand(byte[] memory, int address, byte[] oldData, byte[] newData) {
		Memory = memory;
		Address = address;
		OldData = (byte[])oldData.Clone();
		NewData = (byte[])newData.Clone();
	}

	public void Execute() => Apply(NewData);
	public void Undo() => Apply(OldData);
	public void Redo() => Execute();

	private void Apply(byte[] data) {
		Array.Copy(data, 0, Memory, Address, data.Length);
	}
}

/// <summary>
/// Command for editing voice volume.
/// </summary>
public sealed class VoiceVolumeCommand : SpcEditCommand {
	private readonly int _voice;
	private readonly int _left;
	private readonly int _right;

	public override string Description => $"Set Voice {_voice} Volume (L:{_left}, R:{_right})";

	public VoiceVolumeCommand(byte[] memory, int voice, int dspBase, int oldLeft, int oldRight, int newLeft, int newRight)
		: base(memory, dspBase + voice * 0x10, [ClampByte(oldLeft), ClampByte(oldRight)], [ClampByte(newLeft), ClampByte(newRight)]) {
		_voice = voice;
		_left = newLeft;
		_right = newRight;
	}

	private static byte ClampByte(int value) => (byte)Math.Clamp(value, -128, 127);
}

/// <summary>
/// Command for editing voice pitch.
/// </summary>
public sealed class VoicePitchCommand : SpcEditCommand {
	private readonly int _voice;
	private readonly int _pitch;

	public override string Description => $"Set Voice {_voice} Pitch to {_pitch:X4}";

	public VoicePitchCommand(byte[] memory, int voice, int dspBase, int oldPitch, int newPitch)
		: base(memory, dspBase + voice * 0x10 + 2,
			[(byte)(oldPitch & 0xFF), (byte)((oldPitch >> 8) & 0x3F)],
			[(byte)(newPitch & 0xFF), (byte)((newPitch >> 8) & 0x3F)]) {
		_voice = voice;
		_pitch = newPitch;
	}
}

/// <summary>
/// Command for editing voice source (sample) number.
/// </summary>
public sealed class VoiceSourceCommand : SpcEditCommand {
	private readonly int _voice;
	private readonly int _source;

	public override string Description => $"Set Voice {_voice} Source to {_source}";

	public VoiceSourceCommand(byte[] memory, int voice, int dspBase, byte oldSource, byte newSource)
		: base(memory, dspBase + voice * 0x10 + 4, [oldSource], [newSource]) {
		_voice = voice;
		_source = newSource;
	}
}

/// <summary>
/// Command for editing voice ADSR settings.
/// </summary>
public sealed class VoiceAdsrCommand : SpcEditCommand {
	private readonly int _voice;

	public override string Description => $"Set Voice {_voice} ADSR";

	public VoiceAdsrCommand(byte[] memory, int voice, int dspBase, byte[] oldAdsr, byte[] newAdsr)
		: base(memory, dspBase + voice * 0x10 + 5, oldAdsr, newAdsr) {
		_voice = voice;
	}
}

/// <summary>
/// Command for editing voice GAIN setting.
/// </summary>
public sealed class VoiceGainCommand : SpcEditCommand {
	private readonly int _voice;
	private readonly int _gain;

	public override string Description => $"Set Voice {_voice} GAIN to {_gain:X2}";

	public VoiceGainCommand(byte[] memory, int voice, int dspBase, byte oldGain, byte newGain)
		: base(memory, dspBase + voice * 0x10 + 7, [oldGain], [newGain]) {
		_voice = voice;
		_gain = newGain;
	}
}

/// <summary>
/// Command for editing global DSP register.
/// </summary>
public sealed class DspRegisterCommand : SpcEditCommand {
	private readonly string _registerName;

	public override string Description => $"Set {_registerName}";

	public DspRegisterCommand(byte[] memory, int address, string registerName, byte oldValue, byte newValue)
		: base(memory, address, [oldValue], [newValue]) {
		_registerName = registerName;
	}
}

/// <summary>
/// Command for editing sample data.
/// </summary>
public sealed class SampleDataCommand : SpcEditCommand {
	private readonly int _sampleIndex;

	public override string Description => $"Edit Sample {_sampleIndex} Data";

	public SampleDataCommand(byte[] memory, int address, int sampleIndex, byte[] oldData, byte[] newData)
		: base(memory, address, oldData, newData) {
		_sampleIndex = sampleIndex;
	}
}

/// <summary>
/// Command for editing sample directory entry.
/// </summary>
public sealed class SampleDirectoryCommand : SpcEditCommand {
	private readonly int _sampleIndex;
	private readonly ushort _startAddress;
	private readonly ushort _loopAddress;

	public override string Description => $"Set Sample {_sampleIndex} Directory (Start:{_startAddress:X4}, Loop:{_loopAddress:X4})";

	public SampleDirectoryCommand(byte[] memory, int address, int sampleIndex,
		ushort oldStart, ushort oldLoop, ushort newStart, ushort newLoop)
		: base(memory, address, ToBytes(oldStart, oldLoop), ToBytes(newStart, newLoop)) {
		_sampleIndex = sampleIndex;
		_startAddress = newStart;
		_loopAddress = newLoop;
	}

	private static byte[] ToBytes(ushort start, ushort loop) => [
		(byte)(start & 0xFF), (byte)(start >> 8),
		(byte)(loop & 0xFF), (byte)(loop >> 8)
	];
}

/// <summary>
/// Command for editing echo buffer.
/// </summary>
public sealed class EchoBufferCommand : SpcEditCommand {
	public override string Description => "Edit Echo Buffer";

	public EchoBufferCommand(byte[] memory, int address, byte[] oldData, byte[] newData)
		: base(memory, address, oldData, newData) { }
}

/// <summary>
/// Command for editing FIR filter coefficients.
/// </summary>
public sealed class FirFilterCommand : SpcEditCommand {
	public override string Description => "Set FIR Filter Coefficients";

	public FirFilterCommand(byte[] memory, int dspBase, sbyte[] oldCoeffs, sbyte[] newCoeffs)
		: base(memory, dspBase + 0x0F, ToBytes(oldCoeffs), ToBytes(newCoeffs)) { }

	private static byte[] ToBytes(sbyte[] coeffs) {
		var result = new byte[8];
		for (int i = 0; i < 8; i++) {
			result[i] = (byte)(i < coeffs.Length ? coeffs[i] : 0);
		}
		return result;
	}

	// Note: FIR coefficients are at DSP offsets 0x0F, 0x1F, 0x2F, 0x3F, 0x4F, 0x5F, 0x6F, 0x7F
	// This is a simplified version - actual implementation should scatter across those addresses
}

/// <summary>
/// Compound command that groups multiple commands into one undoable action.
/// </summary>
public sealed class CompoundCommand : IEditCommand {
	private readonly List<IEditCommand> _commands;

	public string Description { get; }

	public CompoundCommand(string description, IEnumerable<IEditCommand> commands) {
		Description = description;
		_commands = commands.ToList();
	}

	public CompoundCommand(string description, params IEditCommand[] commands)
		: this(description, (IEnumerable<IEditCommand>)commands) { }

	public void Execute() {
		foreach (var cmd in _commands) {
			cmd.Execute();
		}
	}

	public void Undo() {
		// Undo in reverse order
		for (int i = _commands.Count - 1; i >= 0; i--) {
			_commands[i].Undo();
		}
	}

	public void Redo() => Execute();
}

/// <summary>
/// Generic memory edit command for arbitrary data.
/// </summary>
public sealed class MemoryEditCommand : SpcEditCommand {
	private readonly string _description;

	public override string Description => _description;

	public MemoryEditCommand(byte[] memory, int address, string description, byte[] oldData, byte[] newData)
		: base(memory, address, oldData, newData) {
		_description = description;
	}
}
