namespace Derby;

internal static class PalTvFilter
{
    internal static void Scale(ReadOnlySpan<uint> source, int width, int height, Span<uint> destination,
        int stride, int scale)
    {
        Span<Signal> row = stackalloc Signal[width];
        for (int y = 0; y < height; y++)
        {
            int offset = y * width;
            int above = Math.Max(0, y - 1) * width;
            int below = Math.Min(height - 1, y + 1) * width;
            Signal l2 = Signal.From(source[offset]);
            Signal l3 = l2;
            Signal l1 = l2;
            Signal center = l2;
            Signal r1 = Signal.From(source[offset + Math.Min(1, width - 1)]);
            Signal r2 = Signal.From(source[offset + Math.Min(2, width - 1)]);
            for (int x = 0; x < width; x++)
            {
                Signal r3 = Signal.From(source[offset + Math.Min(x + 3, width - 1)]);
                // The composite reference spreads colour across the menu edge more than luma.
                int luma = (22 * (l1.Y + r1.Y) + 212 * center.Y + 128) >> 8;
                int u = SpreadChroma(l3.U + r3.U, l2.U + r2.U, l1.U + r1.U, center.U);
                int v = SpreadChroma(l3.V + r3.V, l2.V + r2.V, l1.V + r1.V, center.V);
                int neighbour = Math.Max(Math.Max(l1.Y, r1.Y),
                    Math.Max(Luma(source[above + x]), Luma(source[below + x])));
                // Only bright neighbours spill light, and never onto a brighter pixel.
                int halo = Math.Min(Math.Max(0, neighbour - 180), Math.Max(0, neighbour - center.Y)) / 6;
                row[x] = new Signal(luma + halo, u, v);
                (l3, l2, l1, center, r1, r2) = (l2, l1, center, r1, r2, r3);
            }

            int firstRow = y * scale * stride;
            Span<uint> output = destination.Slice(firstRow, (scale - 1) * stride + width * scale);
            for (int x = 0; x < width; x++)
            {
                Signal pixel = row[x];
                for (int subX = 0; subX < scale; subX++)
                {
                    int shift = subX * 2 + 1 - scale;
                    int blend = Math.Abs(shift) * 56 / scale;
                    Signal neighbour = row[Math.Clamp(x + Math.Sign(shift), 0, width - 1)];
                    int luma = Blend(pixel.Y, neighbour.Y, blend);
                    int u = Blend(pixel.U, neighbour.U, blend);
                    int v = Blend(pixel.V, neighbour.V, blend);
                    int r = luma + v;
                    int b = luma + u;
                    int g = luma - (77 * v + 29 * u) / 150;
                    int column = x * scale + subX;
                    uint bright = Pack(r, g, b);
                    if (scale == 1)
                    {
                        output[column] = bright;
                        continue;
                    }
                    int gain = 226 + luma / 12;
                    uint dim = Pack(r * gain >> 8, g * gain >> 8, b * gain >> 8);
                    if (scale == 2)
                    {
                        output[column] = bright;
                        output[column + stride] = dim;
                    }
                    else
                    {
                        output[column] = dim;
                        output[column + stride] = bright;
                        output[column + 2 * stride] = dim;
                    }
                }
            }
        }
    }

    private static int Blend(int current, int neighbour, int weight) =>
        current + ((neighbour - current) * weight >> 8);

    private static int SpreadChroma(int far, int middle, int near, int center) =>
        15 * (2 * far + 4 * middle + 6 * near + 8 * center) / 512;

    private static uint Pack(int r, int g, int b) => 0xFF000000u |
        ((uint)Math.Clamp(r, 0, 255) << 16) |
        ((uint)Math.Clamp(g, 0, 255) << 8) |
        (uint)Math.Clamp(b, 0, 255);

    private static int Luma(uint argb) =>
        (77 * (int)((argb >> 16) & 255) + 150 * (int)((argb >> 8) & 255) + 29 * (int)(argb & 255) + 128) >> 8;

    private readonly record struct Signal(int Y, int U, int V)
    {
        public static Signal From(uint argb)
        {
            int y = Luma(argb);
            int r = (int)((argb >> 16) & 255);
            int g = (int)((argb >> 8) & 255);
            int b = (int)(argb & 255);
            int spread = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
            // Match the cyan-to-paper contrast without tinting or dimming neutral shades.
            int rolloff = (y * spread + 1024) >> 11;
            return new(y - rolloff, b - y, r - y);
        }
    }
}
