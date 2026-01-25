namespace SpcPlugin.Core.Hardware;

/// <summary>
/// S-DSP hardware constraints and limits.
/// All values here are real hardware limits - no features should exceed these.
/// </summary>
public static class SnesDspLimits {
	#region Memory

	/// <summary>Total Audio RAM available (64KB).</summary>
	public const int TotalRam = 65536;

	/// <summary>Minimum address for sample data (after typical driver code).</summary>
	public const int MinSampleAddress = 0x0200;

	/// <summary>Maximum echo buffer size (15 * 2KB = 30KB).</summary>
	public const int MaxEchoBufferSize = 30720;

	/// <summary>Echo buffer granularity (2KB per delay unit).</summary>
	public const int EchoBufferGranularity = 2048;

	#endregion

	#region Voices

	/// <summary>Number of hardware voices.</summary>
	public const int VoiceCount = 8;

	/// <summary>Minimum voice volume (signed 8-bit).</summary>
	public const int MinVolume = -128;

	/// <summary>Maximum voice volume (signed 8-bit).</summary>
	public const int MaxVolume = 127;

	/// <summary>Minimum pitch value.</summary>
	public const int MinPitch = 0;

	/// <summary>Maximum pitch value (14-bit).</summary>
	public const int MaxPitch = 0x3FFF;

	/// <summary>Pitch value for 32kHz playback (normal speed).</summary>
	public const int NormalPitch = 0x1000;

	/// <summary>Number of sample directory entries.</summary>
	public const int MaxSamples = 256;

	#endregion

	#region BRR Format

	/// <summary>Samples decoded per BRR block.</summary>
	public const int BrrSamplesPerBlock = 16;

	/// <summary>Bytes per BRR block.</summary>
	public const int BrrBytesPerBlock = 9;

	/// <summary>Minimum BRR range value.</summary>
	public const int BrrMinRange = 0;

	/// <summary>Maximum BRR range value.</summary>
	public const int BrrMaxRange = 12;

	/// <summary>Number of BRR filter modes.</summary>
	public const int BrrFilterCount = 4;

	#endregion

	#region ADSR/GAIN

	/// <summary>Maximum attack rate (0-15).</summary>
	public const int MaxAttackRate = 15;

	/// <summary>Maximum decay rate (0-7).</summary>
	public const int MaxDecayRate = 7;

	/// <summary>Maximum sustain level (0-7).</summary>
	public const int MaxSustainLevel = 7;

	/// <summary>Maximum sustain/release rate (0-31).</summary>
	public const int MaxSustainRate = 31;

	/// <summary>Maximum direct GAIN value (0-127).</summary>
	public const int MaxGainValue = 127;

	#endregion

	#region Echo

	/// <summary>Minimum echo delay (0 = no echo).</summary>
	public const int MinEchoDelay = 0;

	/// <summary>Maximum echo delay (15 * 16ms = 240ms).</summary>
	public const int MaxEchoDelay = 15;

	/// <summary>Echo delay time per unit (16ms).</summary>
	public const int EchoDelayMsPerUnit = 16;

	/// <summary>Number of FIR filter coefficients.</summary>
	public const int FirCoefficientCount = 8;

	/// <summary>Minimum FIR coefficient (signed 8-bit).</summary>
	public const int MinFirCoefficient = -128;

	/// <summary>Maximum FIR coefficient (signed 8-bit).</summary>
	public const int MaxFirCoefficient = 127;

	/// <summary>Minimum echo feedback (signed 8-bit).</summary>
	public const int MinEchoFeedback = -128;

	/// <summary>Maximum echo feedback (signed 8-bit).</summary>
	public const int MaxEchoFeedback = 127;

	#endregion

	#region Noise

	/// <summary>Minimum noise clock rate (0).</summary>
	public const int MinNoiseRate = 0;

	/// <summary>Maximum noise clock rate (31).</summary>
	public const int MaxNoiseRate = 31;

	#endregion

	#region Timing

	/// <summary>S-DSP sample rate (32kHz).</summary>
	public const int SampleRate = 32000;

	/// <summary>SNES master clock frequency.</summary>
	public const int MasterClock = 21477270;

	/// <summary>SPC700 clock divider.</summary>
	public const int Spc700ClockDivider = 21;

	#endregion

	#region Validation Helpers

	/// <summary>Validates a voice index (0-7).</summary>
	public static bool IsValidVoice(int voice) => voice >= 0 && voice < VoiceCount;

	/// <summary>Validates a volume value (-128 to 127).</summary>
	public static bool IsValidVolume(int volume) => volume >= MinVolume && volume <= MaxVolume;

	/// <summary>Validates a pitch value (0-16383).</summary>
	public static bool IsValidPitch(int pitch) => pitch >= MinPitch && pitch <= MaxPitch;

	/// <summary>Validates a sample index (0-255).</summary>
	public static bool IsValidSampleIndex(int index) => index >= 0 && index < MaxSamples;

	/// <summary>Validates an echo delay (0-15).</summary>
	public static bool IsValidEchoDelay(int delay) => delay >= MinEchoDelay && delay <= MaxEchoDelay;

	/// <summary>Validates a FIR coefficient (-128 to 127).</summary>
	public static bool IsValidFirCoefficient(int coeff) => coeff >= MinFirCoefficient && coeff <= MaxFirCoefficient;

	/// <summary>Validates a noise rate (0-31).</summary>
	public static bool IsValidNoiseRate(int rate) => rate >= MinNoiseRate && rate <= MaxNoiseRate;

	/// <summary>Validates an ADSR attack rate (0-15).</summary>
	public static bool IsValidAttackRate(int rate) => rate >= 0 && rate <= MaxAttackRate;

	/// <summary>Validates an ADSR decay rate (0-7).</summary>
	public static bool IsValidDecayRate(int rate) => rate >= 0 && rate <= MaxDecayRate;

	/// <summary>Validates an ADSR sustain level (0-7).</summary>
	public static bool IsValidSustainLevel(int level) => level >= 0 && level <= MaxSustainLevel;

	/// <summary>Validates an ADSR sustain/release rate (0-31).</summary>
	public static bool IsValidSustainRate(int rate) => rate >= 0 && rate <= MaxSustainRate;

	/// <summary>Clamps a volume to valid range.</summary>
	public static sbyte ClampVolume(int volume) =>
		(sbyte)Math.Clamp(volume, MinVolume, MaxVolume);

	/// <summary>Clamps a pitch to valid range.</summary>
	public static ushort ClampPitch(int pitch) =>
		(ushort)Math.Clamp(pitch, MinPitch, MaxPitch);

	/// <summary>Converts frequency in Hz to pitch value.</summary>
	public static int FrequencyToPitch(double frequency) =>
		(int)Math.Clamp(frequency / SampleRate * NormalPitch, MinPitch, MaxPitch);

	/// <summary>Converts pitch value to frequency in Hz.</summary>
	public static double PitchToFrequency(int pitch) =>
		(double)pitch / NormalPitch * SampleRate;

	/// <summary>Aligns a sample position to BRR block boundary.</summary>
	public static int AlignToBrrBlock(int samplePosition) =>
		(samplePosition / BrrSamplesPerBlock) * BrrSamplesPerBlock;

	/// <summary>Calculates echo buffer size in bytes for given delay.</summary>
	public static int EchoBufferSizeForDelay(int delay) =>
		Math.Clamp(delay, 0, MaxEchoDelay) * EchoBufferGranularity;

	/// <summary>Calculates available RAM after echo buffer allocation.</summary>
	public static int AvailableRamWithEcho(int echoDelay, int echoStartAddress) =>
		echoStartAddress - MinSampleAddress;

	#endregion
}
