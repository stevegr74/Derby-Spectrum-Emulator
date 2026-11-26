using Xunit;

namespace Derby.Tests;

public sealed class PalTvFilterTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void SinglePixelTextStrokeStaysDarkAgainstLightPaper(int scale)
    {
        const uint paper = 0xFFD7D7D7;
        uint[] input = [paper, paper, paper, 0xFF000000, paper, paper, paper];
        uint[] output = Render(input, scale);
        for (int y = 0; y < scale; y++)
        {
            int row = y * input.Length * scale;
            Assert.InRange(output[row + 3 * scale] & 255, 0u, 78u);
            Assert.InRange(output[row] & 255, 200u, 215u);
        }
    }

    [Fact]
    public void ColourSpreadsPastTheEdgeWithoutLiftingDistantBlack()
    {
        const uint black = 0xFF000000;
        uint[] input = [black, black, black, black, 0xFF00FFFF, 0xFF00FFFF, 0xFF00FFFF, 0xFF00FFFF];
        uint[] output = Render(input, 3);
        Assert.Equal(black, output[0]);
        uint fringe = output[3 * 3 + 2];
        Assert.True((fringe & 255) > ((fringe >> 16) & 255) + 30);
    }

    [Fact]
    public void OnlyBrightPixelsProduceAVerticalHalo()
    {
        const uint black = 0xFF000000;
        uint[] input = [black, 0xFFFFFFFF, black, black, 0xFF808080, black, black];
        var output = new uint[input.Length];
        PalTvFilter.Scale(input, 1, input.Length, output, 1, 1);
        Assert.InRange(output[0] & 255, 8u, 18u);
        Assert.Equal(black, output[3]);
        Assert.Equal(black, output[5]);
    }

    private static uint[] Render(uint[] input, int scale)
    {
        var output = new uint[input.Length * scale * scale];
        PalTvFilter.Scale(input, input.Length, 1, output, input.Length * scale, scale);
        return output;
    }
}
