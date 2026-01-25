using SpcPlugin.Core.Audio;
using SpcPlugin.Core.Hardware;

namespace SpcPlugin.Core.Editing;

/// <summary>
/// Manages the sample bank for an SPC file.
/// Handles sample allocation, import, export, and memory management.
/// Respects real S-DSP constraints including echo buffer.
/// </summary>
public sealed class SampleBankManager {
	private readonly SpcEditor _editor;
	private readonly SampleEntry[] _samples = new SampleEntry[SnesDspLimits.MaxSamples];

	/// <summary>Start of sample data area in SPC RAM.</summary>
	public const int DefaultSampleDataStart = 0x2000;

	/// <summary>Event raised when a sample is added or modified.</summary>
	public event EventHandler<SampleChangedEventArgs>? SampleChanged;

	/// <summary>
	/// Creates a new sample bank manager.
	/// </summary>
	/// <param name="editor">SPC editor for memory access.</param>
	public SampleBankManager(SpcEditor editor) {
		_editor = editor;
		Refresh();
	}

	/// <summary>Gets the sample directory base address.</summary>
	public int DirectoryAddress => _editor.SampleDirectoryAddress;

	/// <summary>Gets the echo buffer start address.</summary>
	public int EchoBufferAddress => _editor.EchoBufferAddress;

	/// <summary>Gets the echo buffer size in bytes.</summary>
	public int EchoBufferSize => SnesDspLimits.EchoBufferSizeForDelay(_editor.EchoDelay);

	/// <summary>Gets the end of available sample space (before echo buffer).</summary>
	public int SampleDataEnd => EchoBufferAddress > 0 ? EchoBufferAddress : SnesDspLimits.TotalRam;

	/// <summary>Gets the number of samples with data.</summary>
	public int SampleCount {
		get {
			int count = 0;
			for (int i = 0; i < SnesDspLimits.MaxSamples; i++) {
				if (_samples[i].HasData) count++;
			}
			return count;
		}
	}

	/// <summary>Gets total used sample memory in bytes.</summary>
	public int UsedMemory {
		get {
			int total = 0;
			for (int i = 0; i < SnesDspLimits.MaxSamples; i++) {
				total += _samples[i].Length;
			}
			return total;
		}
	}

	/// <summary>Gets available sample memory in bytes (respects echo buffer).</summary>
	public int AvailableMemory => SampleDataEnd - DefaultSampleDataStart - UsedMemory;

	/// <summary>
	/// Gets information about a sample.
	/// </summary>
	public SampleEntry GetSample(int index) {
		if (!SnesDspLimits.IsValidSampleIndex(index)) {
			throw new ArgumentOutOfRangeException(nameof(index));
		}
		return _samples[index];
	}

	/// <summary>
	/// Gets all samples with data.
	/// </summary>
	public IEnumerable<SampleEntry> GetAllSamples() {
		for (int i = 0; i < SnesDspLimits.MaxSamples; i++) {
			if (_samples[i].HasData) {
				yield return _samples[i];
			}
		}
	}

	/// <summary>
	/// Refreshes sample information from SPC memory.
	/// </summary>
	public void Refresh() {
		for (int i = 0; i < SnesDspLimits.MaxSamples; i++) {
			var info = _editor.GetSampleInfo(i);
			var brr = _editor.GetSampleBrr(i);

			_samples[i] = new SampleEntry {
				Index = i,
				StartAddress = info.StartAddress,
				LoopAddress = info.LoopAddress,
				Length = info.Length,
				HasData = info.Length > 0,
				HasLoop = info.LoopAddress >= info.StartAddress && info.LoopAddress < info.StartAddress + info.Length,
				BlockCount = info.Length / SnesDspLimits.BrrBytesPerBlock,
				SampleCount = (info.Length / SnesDspLimits.BrrBytesPerBlock) * SnesDspLimits.BrrSamplesPerBlock,
			};
		}
	}

	/// <summary>
	/// Imports a WAV file into a sample slot.
	/// </summary>
	/// <param name="index">Sample index (0-255).</param>
	/// <param name="wavPath">Path to WAV file.</param>
	/// <param name="options">Import options.</param>
	/// <returns>Import result.</returns>
	public SampleImportResult ImportWav(int index, string wavPath, SampleImporter.ImportOptions? options = null) {
		try {
			var result = SampleImporter.ImportWav(wavPath, options);
			if (!result.Success) {
				return new SampleImportResult { Success = false, Error = result.Error };
			}

			return ImportBrr(index, result.BrrData, result.LoopBlock);
		} catch (Exception ex) {
			return new SampleImportResult { Success = false, Error = ex.Message };
		}
	}

	/// <summary>
	/// Imports BRR data into a sample slot.
	/// </summary>
	/// <param name="index">Sample index (0-255).</param>
	/// <param name="brrData">BRR encoded data.</param>
	/// <param name="loopBlock">Loop point in blocks (-1 for no loop).</param>
	/// <returns>Import result.</returns>
	public SampleImportResult ImportBrr(int index, byte[] brrData, int loopBlock = -1) {
		if (!SnesDspLimits.IsValidSampleIndex(index)) {
			return new SampleImportResult { Success = false, Error = "Invalid sample index (0-255)" };
		}

		if (brrData.Length == 0) {
			return new SampleImportResult { Success = false, Error = "Empty BRR data" };
		}

		if (brrData.Length % SnesDspLimits.BrrBytesPerBlock != 0) {
			return new SampleImportResult { Success = false, Error = "BRR data must be multiple of 9 bytes" };
		}

		// Find available memory location (respects echo buffer)
		int address = FindAvailableSpace(brrData.Length, index);
		if (address < 0) {
			return new SampleImportResult { Success = false, Error = $"Not enough memory for sample (need {brrData.Length} bytes, have {AvailableMemory} bytes)" };
		}

		// Verify address doesn't overlap with echo buffer
		if (EchoBufferAddress > 0 && address + brrData.Length > EchoBufferAddress) {
			return new SampleImportResult { Success = false, Error = "Sample would overlap with echo buffer" };
		}

		// Write BRR data to RAM
		var ram = _editor.Ram;
		brrData.CopyTo(ram.Slice(address, brrData.Length));

		// Calculate loop address
		int loopAddress = loopBlock >= 0 ? address + loopBlock * SnesDspLimits.BrrBytesPerBlock : address;

		// Update sample directory
		_editor.SetSampleInfo(index, (ushort)address, (ushort)loopAddress);

		// Update local cache
		_samples[index] = new SampleEntry {
			Index = index,
			StartAddress = (ushort)address,
			LoopAddress = (ushort)loopAddress,
			Length = brrData.Length,
			HasData = true,
			HasLoop = loopBlock >= 0,
			BlockCount = brrData.Length / SnesDspLimits.BrrBytesPerBlock,
			SampleCount = (brrData.Length / SnesDspLimits.BrrBytesPerBlock) * SnesDspLimits.BrrSamplesPerBlock,
		};

		OnSampleChanged(index, SampleChangeType.Added);

		return new SampleImportResult {
			Success = true,
			SampleIndex = index,
			Address = address,
			Length = brrData.Length,
		};
	}

	/// <summary>
	/// Exports a sample as BRR data.
	/// </summary>
	/// <param name="index">Sample index.</param>
	/// <returns>BRR data, or empty array if not found.</returns>
	public byte[] ExportBrr(int index) {
		return _editor.GetSampleBrr(index);
	}

	/// <summary>
	/// Exports a sample as WAV data.
	/// </summary>
	/// <param name="index">Sample index.</param>
	/// <param name="sampleRate">Output sample rate.</param>
	/// <returns>WAV data, or empty array if not found.</returns>
	public byte[] ExportWav(int index, int sampleRate = 32000) {
		var brr = ExportBrr(index);
		if (brr.Length == 0) return [];

		return SampleImporter.ExportBrrToWavData(brr, sampleRate);
	}

	/// <summary>
	/// Exports a sample to a WAV file.
	/// </summary>
	/// <param name="index">Sample index.</param>
	/// <param name="path">Output file path.</param>
	/// <param name="sampleRate">Output sample rate.</param>
	public void ExportWavToFile(int index, string path, int sampleRate = 32000) {
		var brr = ExportBrr(index);
		if (brr.Length == 0) return;

		SampleImporter.ExportBrrToWav(brr, path, sampleRate);
	}

	/// <summary>
	/// Deletes a sample from the bank.
	/// </summary>
	/// <param name="index">Sample index.</param>
	public void DeleteSample(int index) {
		if (index < 0 || index > 255) return;

		// Clear directory entry
		_editor.SetSampleInfo(index, 0, 0);

		// Update cache
		_samples[index] = new SampleEntry { Index = index };

		OnSampleChanged(index, SampleChangeType.Removed);
	}

	/// <summary>
	/// Copies a sample to another slot.
	/// </summary>
	/// <param name="sourceIndex">Source sample index.</param>
	/// <param name="destIndex">Destination sample index.</param>
	/// <returns>True if successful.</returns>
	public bool CopySample(int sourceIndex, int destIndex) {
		var brr = ExportBrr(sourceIndex);
		if (brr.Length == 0) return false;

		var srcEntry = _samples[sourceIndex];
		int loopBlock = srcEntry.HasLoop ? (srcEntry.LoopAddress - srcEntry.StartAddress) / 9 : -1;

		var result = ImportBrr(destIndex, brr, loopBlock);
		return result.Success;
	}

	/// <summary>
	/// Moves a sample to another slot.
	/// </summary>
	/// <param name="sourceIndex">Source sample index.</param>
	/// <param name="destIndex">Destination sample index.</param>
	/// <returns>True if successful.</returns>
	public bool MoveSample(int sourceIndex, int destIndex) {
		if (!CopySample(sourceIndex, destIndex)) return false;
		DeleteSample(sourceIndex);
		return true;
	}

	/// <summary>
	/// Gets decoded PCM samples for a sample.
	/// </summary>
	/// <param name="index">Sample index.</param>
	/// <returns>PCM samples, or empty array if not found.</returns>
	public short[] GetPcmSamples(int index) {
		return _editor.ExtractSample(index);
	}

	/// <summary>
	/// Compacts sample memory by removing gaps.
	/// </summary>
	/// <returns>Bytes freed.</returns>
	public int Compact() {
		// Collect all samples with data
		var activeSamples = new List<(int Index, byte[] Data, int LoopOffset)>();

		for (int i = 0; i < 256; i++) {
			if (_samples[i].HasData) {
				var brr = ExportBrr(i);
				int loopOffset = _samples[i].HasLoop ? _samples[i].LoopAddress - _samples[i].StartAddress : -1;
				activeSamples.Add((i, brr, loopOffset));
			}
		}

		if (activeSamples.Count == 0) return 0;

		int beforeUsed = UsedMemory;

		// Rewrite all samples starting from base address
		int currentAddress = DefaultSampleDataStart;
		foreach (var (idx, data, loopOffset) in activeSamples) {
			// Write data
			data.CopyTo(_editor.Ram.Slice(currentAddress, data.Length));

			// Update directory
			int loopAddr = loopOffset >= 0 ? currentAddress + loopOffset : currentAddress;
			_editor.SetSampleInfo(idx, (ushort)currentAddress, (ushort)loopAddr);

			// Update cache
			_samples[idx] = new SampleEntry {
				Index = idx,
				StartAddress = (ushort)currentAddress,
				LoopAddress = (ushort)loopAddr,
				Length = data.Length,
				HasData = true,
				HasLoop = loopOffset >= 0,
				BlockCount = data.Length / 9,
				SampleCount = (data.Length / 9) * 16,
			};

			currentAddress += data.Length;
		}

		// Clear remaining space up to echo buffer
		var ram = _editor.Ram;
		int clearEnd = SampleDataEnd;
		if (clearEnd > currentAddress) {
			ram.Slice(currentAddress, clearEnd - currentAddress).Clear();
		}

		return beforeUsed - UsedMemory;
	}

	/// <summary>
	/// Finds the first available sample slot.
	/// </summary>
	/// <returns>Sample index, or -1 if all slots are full.</returns>
	public int FindEmptySlot() {
		for (int i = 0; i < 256; i++) {
			if (!_samples[i].HasData) return i;
		}
		return -1;
	}

	private int FindAvailableSpace(int size, int preferredIndex) {
		// Simple allocation: find end of last sample or use preferred address
		int endAddress = DefaultSampleDataStart;

		for (int i = 0; i < 256; i++) {
			if (i == preferredIndex) continue;
			if (_samples[i].HasData) {
				int sampleEnd = _samples[i].StartAddress + _samples[i].Length;
				if (sampleEnd > endAddress) endAddress = sampleEnd;
			}
		}

		// Check if there's room (respects echo buffer)
		if (endAddress + size > SampleDataEnd) {
			return -1;
		}

		return endAddress;
	}

	private void OnSampleChanged(int index, SampleChangeType changeType) {
		SampleChanged?.Invoke(this, new SampleChangedEventArgs(index, changeType));
	}
}

/// <summary>
/// Information about a sample in the bank.
/// </summary>
public struct SampleEntry {
	/// <summary>Sample index (0-255).</summary>
	public int Index { get; init; }

	/// <summary>Start address in SPC RAM.</summary>
	public ushort StartAddress { get; init; }

	/// <summary>Loop address in SPC RAM.</summary>
	public ushort LoopAddress { get; init; }

	/// <summary>Length in bytes.</summary>
	public int Length { get; init; }

	/// <summary>Whether this slot has sample data.</summary>
	public bool HasData { get; init; }

	/// <summary>Whether the sample has a loop point.</summary>
	public bool HasLoop { get; init; }

	/// <summary>Number of BRR blocks.</summary>
	public int BlockCount { get; init; }

	/// <summary>Number of PCM samples.</summary>
	public int SampleCount { get; init; }

	/// <summary>Duration in seconds (at 32 kHz).</summary>
	public double Duration => SampleCount / 32000.0;

	/// <summary>Loop block index.</summary>
	public int LoopBlock => HasLoop ? (LoopAddress - StartAddress) / 9 : -1;
}

/// <summary>
/// Result of a sample import operation.
/// </summary>
public struct SampleImportResult {
	/// <summary>Whether import was successful.</summary>
	public bool Success { get; init; }

	/// <summary>Error message if failed.</summary>
	public string? Error { get; init; }

	/// <summary>Destination sample index.</summary>
	public int SampleIndex { get; init; }

	/// <summary>Address where sample was stored.</summary>
	public int Address { get; init; }

	/// <summary>Length of stored data.</summary>
	public int Length { get; init; }
}

/// <summary>
/// Type of sample change.
/// </summary>
public enum SampleChangeType {
	Added,
	Modified,
	Removed,
}

/// <summary>
/// Event args for sample changes.
/// </summary>
public sealed class SampleChangedEventArgs : EventArgs {
	public int SampleIndex { get; }
	public SampleChangeType ChangeType { get; }

	public SampleChangedEventArgs(int index, SampleChangeType changeType) {
		SampleIndex = index;
		ChangeType = changeType;
	}
}
