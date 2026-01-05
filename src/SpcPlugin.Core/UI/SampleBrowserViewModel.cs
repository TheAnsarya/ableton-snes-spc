using System.ComponentModel;
using System.Runtime.CompilerServices;
using SpcPlugin.Core.Audio;
using SpcPlugin.Core.Editing;
using SpcPlugin.Core.RealTime;

namespace SpcPlugin.Core.UI;

/// <summary>
/// View model for the sample browser/editor panel.
/// </summary>
public class SampleBrowserViewModel : INotifyPropertyChanged {
	private readonly SpcEngine _engine;
	private readonly RealtimeSampleEditor _sampleEditor;
	private int _selectedSampleIndex = -1;
	private SampleInfo? _selectedSample;
	private float[]? _selectedWaveform;
	private List<SampleInfo> _samples = [];

	public event PropertyChangedEventHandler? PropertyChanged;

	public SampleBrowserViewModel(SpcEngine engine) {
		_engine = engine;
		_sampleEditor = new RealtimeSampleEditor(engine);
	}

	/// <summary>
	/// List of all samples in the SPC.
	/// </summary>
	public List<SampleInfo> Samples {
		get => _samples;
		private set => SetField(ref _samples, value);
	}

	/// <summary>
	/// Currently selected sample index (-1 if none).
	/// </summary>
	public int SelectedSampleIndex {
		get => _selectedSampleIndex;
		set {
			if (SetField(ref _selectedSampleIndex, value)) {
				LoadSelectedSample();
			}
		}
	}

	/// <summary>
	/// Currently selected sample info.
	/// </summary>
	public SampleInfo? SelectedSample {
		get => _selectedSample;
		private set => SetField(ref _selectedSample, value);
	}

	/// <summary>
	/// Waveform data for the selected sample.
	/// </summary>
	public float[]? SelectedWaveform {
		get => _selectedWaveform;
		private set => SetField(ref _selectedWaveform, value);
	}

	/// <summary>
	/// Whether a sample is selected.
	/// </summary>
	public bool HasSelection => _selectedSampleIndex >= 0 && _selectedSample != null;

	/// <summary>
	/// Refreshes the sample list from the current SPC.
	/// </summary>
	public void RefreshSamples() {
		if (_engine.Editor == null) {
			Samples = [];
			return;
		}

		var list = new List<SampleInfo>();
		var dirAddress = _engine.Editor.SampleDirectoryAddress;

		// The sample directory has 4 bytes per entry
		for (int i = 0; i < 128; i++) {
			try {
				var info = _engine.Editor.GetSampleInfo(i);
				if (info.Length > 0) {
					list.Add(new SampleInfo {
						Index = i,
						StartAddress = info.StartAddress,
						LoopAddress = info.LoopAddress,
						Length = info.Length,
						DecodedLength = info.Length / 9 * 16 // 9 bytes -> 16 samples
					});
				}
			} catch {
				// Invalid sample entry
			}
		}

		Samples = list;

		if (_selectedSampleIndex >= 0 && _selectedSampleIndex < list.Count) {
			LoadSelectedSample();
		} else {
			SelectedSampleIndex = list.Count > 0 ? 0 : -1;
		}
	}

	private void LoadSelectedSample() {
		if (_selectedSampleIndex < 0 || _selectedSampleIndex >= _samples.Count) {
			SelectedSample = null;
			SelectedWaveform = null;
			return;
		}

		var sample = _samples[_selectedSampleIndex];
		SelectedSample = sample;

		// Decode BRR for visualization
		if (_engine.Editor != null && sample.Length > 0) {
			try {
				var brrData = _engine.Editor.GetSampleBrr(sample.Index);
				SelectedWaveform = SampleDisplay.DecodeBrrForDisplay(brrData);
			} catch {
				SelectedWaveform = null;
			}
		}

		OnPropertyChanged(nameof(HasSelection));
	}

	/// <summary>
	/// Previews the selected sample (triggers on voice 0).
	/// </summary>
	public void PreviewSample() {
		if (!HasSelection || SelectedSample == null) return;
		_sampleEditor.TriggerSample(SelectedSample.Index, 0);
	}

	/// <summary>
	/// Exports the selected sample to WAV.
	/// </summary>
	public void ExportSampleToWav(string outputPath) {
		if (!HasSelection || SelectedSample == null || _engine.Editor == null) return;

		// Get SPC data and export
		var spcData = _engine.Editor.ExportSpc();
		SpcExporter.ExportSampleToWav(spcData, outputPath, SelectedSample.Index);
	}

	/// <summary>
	/// Imports a WAV file as a replacement for the selected sample.
	/// </summary>
	public bool ImportWavAsSample(string wavPath) {
		if (!HasSelection || SelectedSample == null) return false;

		try {
			_sampleEditor.ImportWavAsSample(SelectedSample.Index, wavPath);
			LoadSelectedSample(); // Refresh waveform
			return true;
		} catch {
			return false;
		}
	}

	/// <summary>
	/// Applies a filter to the selected sample.
	/// </summary>
	public void ApplyFilter(SampleFilter filter) {
		if (!HasSelection || SelectedSample == null) return;
		_sampleEditor.ApplyFilter(SelectedSample.Index, filter);
		LoadSelectedSample(); // Refresh waveform
	}

	/// <summary>
	/// Restores the selected sample to its original state.
	/// </summary>
	public void RestoreOriginal() {
		if (!HasSelection || SelectedSample == null) return;
		_sampleEditor.RestoreSample(SelectedSample.Index);
		LoadSelectedSample();
	}

	protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
		if (EqualityComparer<T>.Default.Equals(field, value)) return false;
		field = value;
		OnPropertyChanged(propertyName);
		return true;
	}
}

/// <summary>
/// Information about a sample in the SPC.
/// </summary>
public class SampleInfo {
	/// <summary>
	/// Sample index (0-127).
	/// </summary>
	public int Index { get; init; }

	/// <summary>
	/// Start address in ARAM.
	/// </summary>
	public ushort StartAddress { get; init; }

	/// <summary>
	/// Loop point address in ARAM.
	/// </summary>
	public ushort LoopAddress { get; init; }

	/// <summary>
	/// Length in bytes (BRR encoded).
	/// </summary>
	public int Length { get; init; }

	/// <summary>
	/// Length in samples when decoded.
	/// </summary>
	public int DecodedLength { get; init; }

	/// <summary>
	/// Display name.
	/// </summary>
	public string Name => $"Sample {Index:D3}";

	/// <summary>
	/// Whether this sample has a loop.
	/// </summary>
	public bool HasLoop => LoopAddress >= StartAddress && LoopAddress < StartAddress + Length;

	/// <summary>
	/// Duration in seconds (at base pitch).
	/// </summary>
	public double DurationSeconds => DecodedLength / 32000.0;
}

/// <summary>
/// View model for ADSR envelope editor.
/// </summary>
public class AdsrEditorViewModel : INotifyPropertyChanged {
	private readonly SpcEditor _editor;
	private readonly int _voiceIndex;

	public event PropertyChangedEventHandler? PropertyChanged;

	public AdsrEditorViewModel(SpcEditor editor, int voiceIndex) {
		_editor = editor;
		_voiceIndex = voiceIndex;
	}

	/// <summary>
	/// Attack rate (0-15, lower = faster).
	/// </summary>
	public int Attack {
		get {
			var info = _editor.GetVoiceInfo(_voiceIndex);
			return info.AttackRate;
		}
		set {
			var info = _editor.GetVoiceInfo(_voiceIndex);
			_editor.SetVoiceAdsr(_voiceIndex,
				(byte)Math.Clamp(value, 0, 15),
				info.DecayRate,
				info.SustainLevel,
				info.SustainRate);
			OnPropertyChanged();
			OnPropertyChanged(nameof(AttackTimeMs));
		}
	}

	/// <summary>
	/// Attack time in milliseconds (approximate).
	/// </summary>
	public string AttackTimeMs {
		get {
			// Attack times: 4.1ms to 2.6s
			double[] times = [4.1, 10, 21, 42, 84, 168, 337, 674, 1349, 2698, 0, 0, 0, 0, 0, 0];
			int idx = Math.Clamp(15 - Attack, 0, 15);
			return idx < 10 ? $"{times[idx]:F1}ms" : "∞";
		}
	}

	/// <summary>
	/// Decay rate (0-7, lower = faster).
	/// </summary>
	public int Decay {
		get {
			var info = _editor.GetVoiceInfo(_voiceIndex);
			return info.DecayRate;
		}
		set {
			var info = _editor.GetVoiceInfo(_voiceIndex);
			_editor.SetVoiceAdsr(_voiceIndex,
				info.AttackRate,
				(byte)Math.Clamp(value, 0, 7),
				info.SustainLevel,
				info.SustainRate);
			OnPropertyChanged();
			OnPropertyChanged(nameof(DecayTimeMs));
		}
	}

	/// <summary>
	/// Decay time in milliseconds (approximate).
	/// </summary>
	public string DecayTimeMs {
		get {
			double[] times = [1.2, 5, 10, 20, 40, 80, 160, 320];
			int idx = Math.Clamp(7 - Decay, 0, 7);
			return $"{times[idx]:F0}ms";
		}
	}

	/// <summary>
	/// Sustain level (0-7).
	/// </summary>
	public int Sustain {
		get {
			var info = _editor.GetVoiceInfo(_voiceIndex);
			return info.SustainLevel;
		}
		set {
			var info = _editor.GetVoiceInfo(_voiceIndex);
			_editor.SetVoiceAdsr(_voiceIndex,
				info.AttackRate,
				info.DecayRate,
				(byte)Math.Clamp(value, 0, 7),
				info.SustainRate);
			OnPropertyChanged();
			OnPropertyChanged(nameof(SustainPercent));
		}
	}

	/// <summary>
	/// Sustain level as percentage.
	/// </summary>
	public string SustainPercent {
		get {
			double[] levels = [12.5, 25, 37.5, 50, 62.5, 75, 87.5, 100];
			int idx = Math.Clamp(Sustain, 0, 7);
			return $"{levels[idx]:F1}%";
		}
	}

	/// <summary>
	/// Release rate (0-31, lower = faster).
	/// </summary>
	public int Release {
		get {
			var info = _editor.GetVoiceInfo(_voiceIndex);
			return info.SustainRate;
		}
		set {
			var info = _editor.GetVoiceInfo(_voiceIndex);
			_editor.SetVoiceAdsr(_voiceIndex,
				info.AttackRate,
				info.DecayRate,
				info.SustainLevel,
				(byte)Math.Clamp(value, 0, 31));
			OnPropertyChanged();
			OnPropertyChanged(nameof(ReleaseTimeMs));
		}
	}

	/// <summary>
	/// Release time in milliseconds (approximate).
	/// </summary>
	public string ReleaseTimeMs {
		get {
			// Release times vary from ~0.6ms to ~5.2s
			double baseTime = 0.6;
			return $"{baseTime * Math.Pow(2, Release / 5.0):F0}ms";
		}
	}

	/// <summary>
	/// Gets points for drawing the ADSR envelope curve.
	/// </summary>
	/// <param name="width">Target width.</param>
	/// <param name="height">Target height.</param>
	/// <returns>Array of (x, y) points.</returns>
	public (float x, float y)[] GetEnvelopeCurve(float width, float height) {
		var points = new List<(float x, float y)>();

		// Normalize times to fit in width
		float attackTime = 0.15f; // Simplified
		float decayTime = 0.1f;
		float sustainTime = 0.4f;
		float releaseTime = 0.2f;

		float sustainLevel = (Sustain + 1) / 8f;

		// Scale to width
		float totalTime = attackTime + decayTime + sustainTime + releaseTime;
		float scale = width / totalTime;

		// Attack phase (0 to 1)
		points.Add((0, height));
		points.Add((attackTime * scale, 0));

		// Decay phase (1 to sustain)
		points.Add(((attackTime + decayTime) * scale, height * (1 - sustainLevel)));

		// Sustain phase (hold at sustain level)
		points.Add(((attackTime + decayTime + sustainTime) * scale, height * (1 - sustainLevel)));

		// Release phase (sustain to 0)
		points.Add((width, height));

		return [.. points];
	}

	/// <summary>
	/// Refresh all properties.
	/// </summary>
	public void Refresh() {
		OnPropertyChanged(nameof(Attack));
		OnPropertyChanged(nameof(AttackTimeMs));
		OnPropertyChanged(nameof(Decay));
		OnPropertyChanged(nameof(DecayTimeMs));
		OnPropertyChanged(nameof(Sustain));
		OnPropertyChanged(nameof(SustainPercent));
		OnPropertyChanged(nameof(Release));
		OnPropertyChanged(nameof(ReleaseTimeMs));
	}

	protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}

/// <summary>
/// View model for the echo/reverb settings panel.
/// </summary>
public class EchoEditorViewModel : INotifyPropertyChanged {
	private readonly SpcEditor _editor;
	private readonly bool[] _voiceEchoEnabled = new bool[8];

	public event PropertyChangedEventHandler? PropertyChanged;

	public EchoEditorViewModel(SpcEditor editor) {
		_editor = editor;
		RefreshFromDsp();
	}

	/// <summary>
	/// Echo delay (0-15, each unit = 16ms).
	/// </summary>
	public int Delay {
		get => _editor.EchoDelay;
		set {
			_editor.EchoDelay = (byte)Math.Clamp(value, 0, 15);
			OnPropertyChanged();
			OnPropertyChanged(nameof(DelayMs));
			OnPropertyChanged(nameof(DelayFormatted));
		}
	}

	/// <summary>
	/// Delay in milliseconds.
	/// </summary>
	public int DelayMs => Delay * 16;

	/// <summary>
	/// Formatted delay string.
	/// </summary>
	public string DelayFormatted => $"{DelayMs}ms";

	/// <summary>
	/// Echo feedback (-128 to 127).
	/// </summary>
	public int Feedback {
		get => _editor.EchoFeedback;
		set {
			_editor.EchoFeedback = (sbyte)Math.Clamp(value, -128, 127);
			OnPropertyChanged();
			OnPropertyChanged(nameof(FeedbackPercent));
		}
	}

	/// <summary>
	/// Feedback as percentage.
	/// </summary>
	public string FeedbackPercent => $"{Feedback * 100 / 128:F0}%";

	/// <summary>
	/// Echo volume left (-128 to 127).
	/// </summary>
	public int VolumeLeft {
		get => _editor.EchoVolume.Left;
		set {
			var current = _editor.EchoVolume;
			_editor.EchoVolume = ((sbyte)Math.Clamp(value, -128, 127), current.Right);
			OnPropertyChanged();
		}
	}

	/// <summary>
	/// Echo volume right (-128 to 127).
	/// </summary>
	public int VolumeRight {
		get => _editor.EchoVolume.Right;
		set {
			var current = _editor.EchoVolume;
			_editor.EchoVolume = (current.Left, (sbyte)Math.Clamp(value, -128, 127));
			OnPropertyChanged();
		}
	}

	/// <summary>
	/// Gets/sets echo enable for a voice.
	/// </summary>
	public bool GetVoiceEchoEnabled(int voice) {
		return (_editor.EchoEnable & (1 << voice)) != 0;
	}

	public void SetVoiceEchoEnabled(int voice, bool enabled) {
		if (enabled)
			_editor.EchoEnable |= (byte)(1 << voice);
		else
			_editor.EchoEnable &= (byte)~(1 << voice);
		OnPropertyChanged($"Voice{voice}EchoEnabled");
	}

	// Individual voice echo properties for binding
	public bool Voice0EchoEnabled { get => GetVoiceEchoEnabled(0); set => SetVoiceEchoEnabled(0, value); }
	public bool Voice1EchoEnabled { get => GetVoiceEchoEnabled(1); set => SetVoiceEchoEnabled(1, value); }
	public bool Voice2EchoEnabled { get => GetVoiceEchoEnabled(2); set => SetVoiceEchoEnabled(2, value); }
	public bool Voice3EchoEnabled { get => GetVoiceEchoEnabled(3); set => SetVoiceEchoEnabled(3, value); }
	public bool Voice4EchoEnabled { get => GetVoiceEchoEnabled(4); set => SetVoiceEchoEnabled(4, value); }
	public bool Voice5EchoEnabled { get => GetVoiceEchoEnabled(5); set => SetVoiceEchoEnabled(5, value); }
	public bool Voice6EchoEnabled { get => GetVoiceEchoEnabled(6); set => SetVoiceEchoEnabled(6, value); }
	public bool Voice7EchoEnabled { get => GetVoiceEchoEnabled(7); set => SetVoiceEchoEnabled(7, value); }

	/// <summary>
	/// FIR filter coefficients (8 values).
	/// </summary>
	public sbyte[] FirCoefficients => _editor.EchoFir;

	/// <summary>
	/// Sets a FIR coefficient.
	/// </summary>
	public void SetFirCoefficient(int index, sbyte value) {
		if (index >= 0 && index < 8) {
			var fir = _editor.EchoFir;
			fir[index] = value;
			_editor.EchoFir = fir;
			OnPropertyChanged(nameof(FirCoefficients));
		}
	}

	/// <summary>
	/// Applies a preset FIR filter.
	/// </summary>
	public void ApplyFirPreset(EchoFirPreset preset) {
		_editor.EchoFir = preset switch {
			EchoFirPreset.None => [127, 0, 0, 0, 0, 0, 0, 0],
			EchoFirPreset.LowPass => [64, 32, 16, 8, 4, 2, 1, 0],
			EchoFirPreset.HighPass => [64, -32, 16, -8, 4, -2, 1, 0],
			EchoFirPreset.BandPass => [0, 32, 64, 32, 0, -16, -32, -16],
			EchoFirPreset.Reverb => [64, 48, 32, 24, 16, 12, 8, 4],
			_ => [127, 0, 0, 0, 0, 0, 0, 0]
		};
		OnPropertyChanged(nameof(FirCoefficients));
	}

	private void RefreshFromDsp() {
		for (int i = 0; i < 8; i++) {
			_voiceEchoEnabled[i] = GetVoiceEchoEnabled(i);
		}
	}

	/// <summary>
	/// Refresh all properties.
	/// </summary>
	public void Refresh() {
		RefreshFromDsp();
		OnPropertyChanged(nameof(Delay));
		OnPropertyChanged(nameof(DelayMs));
		OnPropertyChanged(nameof(DelayFormatted));
		OnPropertyChanged(nameof(Feedback));
		OnPropertyChanged(nameof(FeedbackPercent));
		OnPropertyChanged(nameof(VolumeLeft));
		OnPropertyChanged(nameof(VolumeRight));
		OnPropertyChanged(nameof(FirCoefficients));
		for (int i = 0; i < 8; i++) {
			OnPropertyChanged($"Voice{i}EchoEnabled");
		}
	}

	protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}

/// <summary>
/// Preset FIR filter configurations.
/// </summary>
public enum EchoFirPreset {
	None,
	LowPass,
	HighPass,
	BandPass,
	Reverb
}
