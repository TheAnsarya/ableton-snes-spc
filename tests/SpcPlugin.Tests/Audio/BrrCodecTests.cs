
using SpcPlugin.Core.Audio;

namespace SpcPlugin.Tests.Audio;
public class BrrCodecTests {
	[Fact]
	public void DecodeBlock_WithZeroShift_DecodesCorrectly() {
		// Arrange: BRR block with shift=0, filter=0
		byte[] block = [0x00, 0x12, 0x34, 0x56, 0x78, 0x9a, 0xbc, 0xde, 0xf0];
		short[] output = new short[16];
		short prev1 = 0, prev2 = 0;

		// Act
		BrrCodec.DecodeBlock(block, output, ref prev1, ref prev2);

		// Assert: First nibble should be 1, second should be 2
		Assert.Equal(1, output[0]);
		Assert.Equal(2, output[1]);
	}

	[Fact]
	public void DecodeBlock_WithShift_ScalesCorrectly() {
		// Arrange: BRR block with shift=4, filter=0
		byte[] block = [0x40, 0x11, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
		short[] output = new short[16];
		short prev1 = 0, prev2 = 0;

		// Act
		BrrCodec.DecodeBlock(block, output, ref prev1, ref prev2);

		// Assert: 1 << 4 = 16
		Assert.Equal(16, output[0]);
		Assert.Equal(16, output[1]);
	}

	[Fact]
	public void DecodeBlock_SignExtends_NegativeNibbles() {
		// Arrange: BRR block with nibble = 0xF (-1)
		byte[] block = [0x00, 0xff, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
		short[] output = new short[16];
		short prev1 = 0, prev2 = 0;

		// Act
		BrrCodec.DecodeBlock(block, output, ref prev1, ref prev2);

		// Assert: 0xF sign-extended = -1
		Assert.Equal(-1, output[0]);
		Assert.Equal(-1, output[1]);
	}

	[Fact]
	public void IsEndBlock_ReturnsTrue_WhenEndFlagSet() {
		Assert.True(BrrCodec.IsEndBlock(0x01));
		Assert.True(BrrCodec.IsEndBlock(0x03));
		Assert.True(BrrCodec.IsEndBlock(0xF1));
	}

	[Fact]
	public void IsEndBlock_ReturnsFalse_WhenEndFlagClear() {
		Assert.False(BrrCodec.IsEndBlock(0x00));
		Assert.False(BrrCodec.IsEndBlock(0x02));
		Assert.False(BrrCodec.IsEndBlock(0xFE));
	}

	[Fact]
	public void IsLoopBlock_ReturnsTrue_WhenLoopFlagSet() {
		Assert.True(BrrCodec.IsLoopBlock(0x02));
		Assert.True(BrrCodec.IsLoopBlock(0x03));
		Assert.True(BrrCodec.IsLoopBlock(0xF2));
	}

	[Fact]
	public void IsLoopBlock_ReturnsFalse_WhenLoopFlagClear() {
		Assert.False(BrrCodec.IsLoopBlock(0x00));
		Assert.False(BrrCodec.IsLoopBlock(0x01));
		Assert.False(BrrCodec.IsLoopBlock(0xFD));
	}

	[Fact]
	public void DecodeBlock_ThrowsOnSmallInput() {
		byte[] block = new byte[8]; // Too small
		short[] output = new short[16];
		short prev1 = 0, prev2 = 0;

		Assert.Throws<ArgumentException>(() =>
			BrrCodec.DecodeBlock(block, output, ref prev1, ref prev2));
	}

	[Fact]
	public void DecodeBlock_ThrowsOnSmallOutput() {
		byte[] block = new byte[9];
		short[] output = new short[8]; // Too small
		short prev1 = 0, prev2 = 0;

		Assert.Throws<ArgumentException>(() =>
			BrrCodec.DecodeBlock(block, output, ref prev1, ref prev2));
	}
}
