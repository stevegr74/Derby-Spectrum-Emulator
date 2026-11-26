using System.Drawing;
using System.Drawing.Imaging;
using Xunit;

namespace Derby.Tests;

public sealed class SoftCrtFilterTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void MaskIsOmittedAtSmallScales(int scale)
    {
        uint[] source = [0xFF000000, 0xFF00FFFF, 0xFFFFFFFF, 0xFF000000];
        var unmasked = new uint[4 * scale * scale];
        var masked = new uint[unmasked.Length];
        SoftCrtFilter.Scale(source, 2, 2, unmasked, 2 * scale, scale, false);
        SoftCrtFilter.Scale(source, 2, 2, masked, 2 * scale, scale, true);
        Assert.Equal(unmasked, masked);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void ThinWhiteStrokeKeepsContrastAndDoesNotBleedVertically(int scale)
    {
        const uint black = 0xFF000000;
        uint[] source = [black, black, black, black, black, 0xFFFFFFFF, black, black, black, black, black, black];
        int stride = 4 * scale;
        var target = new uint[stride * 3 * scale];
        SoftCrtFilter.Scale(source, 4, 3, target, stride, scale, false);
        int row = (scale + (scale == 3 ? 1 : 0)) * stride;
        Assert.InRange(target[row + scale] & 255, 225u, 255u);
        Assert.InRange(target[row + scale - 1] & 255, 1u, 90u);
        Assert.Equal(black, target[row + 3 * scale]);
        Assert.All(target[..(stride * scale)], value => Assert.Equal(black, value));
        Assert.All(target[(stride * 2 * scale)..], value => Assert.Equal(black, value));
    }

    [Fact]
    public void MaskAddsSubpixelDetailWithoutAMeaningfulBrightnessShift()
    {
        uint[] white = [0xFFFFFFFF, 0xFFFFFFFF];
        var unmasked = new uint[18];
        var masked = new uint[18];
        SoftCrtFilter.Scale(white, 2, 1, unmasked, 6, 3, false);
        SoftCrtFilter.Scale(white, 2, 1, masked, 6, 3, true);
        Assert.Equal(3, masked[..3].Distinct().Count());
        Assert.Equal(masked[..3], masked[3..6]);
        Assert.InRange(Math.Abs(unmasked.Average(p => (int)(p & 255)) -
            masked.Average(p => (int)(p & 255))), 0.0, 2.0);
    }

    [Fact]
    public void DigitalRemainsTheDefaultAndFilteringDoesNotChangeSourcePixels()
    {
        var frame = new SpectrumFrameBuffer();
        frame.Render(new byte[0x1b00], borderColor: 1, flashPhase: false);
        uint[] native = frame.Pixels.ToArray();
        using var bitmap = new Bitmap(SpectrumFrameBuffer.Width, SpectrumFrameBuffer.Height, PixelFormat.Format32bppArgb);
        SpectrumRenderer.RenderScaledToBitmap(bitmap, frame, 1, SpectrumDisplayFilter.SoftCrt);
        Assert.Equal(native, frame.Pixels.ToArray());
        SpectrumRenderer.RenderScaledToBitmap(bitmap, frame, 1);
        Assert.Equal(unchecked((int)native[0]), bitmap.GetPixel(0, 0).ToArgb());
    }
}
