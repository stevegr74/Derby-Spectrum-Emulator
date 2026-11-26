namespace Derby;

public enum SpectrumDisplayFilter
{
    Digital,
    SoftCrt,
    SoftCrtWithMask,
    PalTv
}

internal static class SoftCrtFilter
{
    private const int LinearMax = 4095;
    private static readonly int[] Decode = BuildDecode();
    private static readonly byte[] Encode = BuildEncode();

    internal static void Scale(ReadOnlySpan<uint> source, int width, int height, Span<uint> destination,
        int stride, int scale, bool mask)
    {
        mask &= scale >= 3;
        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<uint> row = source.Slice(y * width, width);
            Light left = Light.From(row[0]);
            Light center = left;
            for (int x = 0; x < width; x++)
            {
                Light right = x == width - 1 ? center : Light.From(row[x + 1]);
                for (int subX = 0; subX < scale; subX++)
                {
                    (int near, int far) = scale switch
                    {
                        1 => (7, 7),
                        2 => (13, 3),
                        _ when subX == 1 => (4, 4),
                        _ => (17, 2)
                    };
                    bool rightHalf = subX * 2 >= scale;
                    int wl = rightHalf ? far : near;
                    int wr = rightHalf ? near : far;
                    // Chroma spreads farther than luma, so colour softens without smearing text.
                    int correction = ((center.Y - left.Y) * wl + (center.Y - right.Y) * wr) / 2;
                    int r = Mix(left.R, center.R, right.R, wl, wr, correction);
                    int g = Mix(left.G, center.G, right.G, wl, wr, correction);
                    int b = Mix(left.B, center.B, right.B, wl, wr, correction);
                    int beam = Math.Max(r, Math.Max(g, b));
                    int destX = x * scale + subX;
                    for (int subY = 0; subY < scale; subY++)
                    {
                        int gain = scale switch
                        {
                            2 when subY == 1 => 89 + beam * 8 / LinearMax,
                            3 when subY != 1 => 76 + beam * 18 / LinearMax,
                            _ => 100
                        };
                        // The selected channel gains what the other two lose, averaged over a triad.
                        int phase = mask ? (destX + (y & 1)) % 3 : -1;
                        destination[(y * scale + subY) * stride + destX] =
                            0xFF000000u |
                            ((uint)Output(r, gain, phase, 0) << 16) |
                            ((uint)Output(g, gain, phase, 1) << 8) |
                            Output(b, gain, phase, 2);
                    }
                }
                left = center;
                center = right;
            }
        }
    }

    private static int Mix(int left, int center, int right, int wl, int wr, int correction) =>
        center + ((left - center) * wl + (right - center) * wr + correction) / 100;

    private static byte Output(int value, int gain, int phase, int channel)
    {
        int maskGain = phase < 0 ? 100 : phase == channel ? 114 : 93;
        return Encode[Math.Clamp(value * gain * maskGain / 10000, 0, LinearMax)];
    }

    private static int[] BuildDecode() => Enumerable.Range(0, 256).Select(value =>
    {
        double s = value / 255.0;
        double linear = s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        // Reserve headroom so the mask can lift one phosphor without clipping bright white.
        return (int)Math.Round(linear * 0.87 * LinearMax);
    }).ToArray();

    private static byte[] BuildEncode() => Enumerable.Range(0, LinearMax + 1).Select(value =>
    {
        double linear = value / (double)LinearMax;
        double s = linear <= 0.0031308 ? 12.92 * linear : 1.055 * Math.Pow(linear, 1 / 2.4) - 0.055;
        return (byte)Math.Round(s * 255);
    }).ToArray();

    private readonly record struct Light(int R, int G, int B, int Y)
    {
        public static Light From(uint argb)
        {
            int r = Decode[(argb >> 16) & 255];
            int g = Decode[(argb >> 8) & 255];
            int b = Decode[argb & 255];
            return new(r, g, b, (54 * r + 183 * g + 19 * b) >> 8);
        }
    }
}
