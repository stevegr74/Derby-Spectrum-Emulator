using Derby.Tape;

namespace Derby
{
    public readonly record struct SpectrumStatusOverlayData(
        int FramesPerSecond,
        string ModelLabel,
        TapeTransportState TapeState,
        bool TapeStoppedAtMarker,
        bool TapeJustInserted,
        string DisplayMode,
        SpectrumDisplayFilter Filter);

    public readonly record struct SpectrumStatusOverlayMetrics(
        int Width,
        int Height,
        int Margin,
        float FontSize,
        int LineHeight);

    public static class SpectrumStatusOverlay
    {
        public const int TapeClearanceY = 46;

        public static string[] FormatLines(SpectrumStatusOverlayData data) =>
        [
            $"FPS: {data.FramesPerSecond}",
            $"Model: {data.ModelLabel}",
            $"Tape: {FormatTapeState(data.TapeState, data.TapeStoppedAtMarker, data.TapeJustInserted)}",
            $"Display: {data.DisplayMode}",
            $"Filter: {FilterLabel(data.Filter)}"
        ];

        private static string FilterLabel(SpectrumDisplayFilter filter) => filter switch
        {
            SpectrumDisplayFilter.SoftCrt => "Soft CRT",
            SpectrumDisplayFilter.SoftCrtWithMask => "Soft CRT + Mask",
            SpectrumDisplayFilter.PalTv => "PAL TV",
            _ => "Digital"
        };

        public static Rectangle GetBounds(int bitmapWidth, int scale, bool avoidFullTapeOverlay)
        {
            if (bitmapWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(bitmapWidth));
            if (scale <= 0)
                throw new ArgumentOutOfRangeException(nameof(scale));

            SpectrumStatusOverlayMetrics metrics = GetMetrics(scale);
            int x = bitmapWidth - metrics.Width - metrics.Margin;
            int y = avoidFullTapeOverlay ? TapeClearanceY * scale : metrics.Margin;
            return new Rectangle(x, y, metrics.Width, metrics.Height);
        }

        public static SpectrumStatusOverlayMetrics GetMetrics(int scale) => scale switch
        {
            1 => new SpectrumStatusOverlayMetrics(115, 50, 6, 8f, 10),
            2 => new SpectrumStatusOverlayMetrics(170, 70, 8, 12f, 14),
            3 => new SpectrumStatusOverlayMetrics(195, 80, 10, 14f, 16),
            _ => throw new ArgumentOutOfRangeException(nameof(scale))
        };

        public static void Draw(
            Bitmap bitmap,
            int scale,
            SpectrumStatusOverlayData data,
            bool avoidFullTapeOverlay)
        {
            ArgumentNullException.ThrowIfNull(bitmap);

            Rectangle bounds = GetBounds(bitmap.Width, scale, avoidFullTapeOverlay);
            SpectrumStatusOverlayMetrics metrics = GetMetrics(scale);
            string[] lines = FormatLines(data);
            Color accent = Color.FromArgb(142, 220, 198);

            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var accentBrush = new SolidBrush(accent);
            using var shadowBrush = new SolidBrush(Color.FromArgb(190, 8, 12, 14));
            using var font = new Font("Consolas", metrics.FontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            using var textFormat = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Far,
                FormatFlags = StringFormatFlags.NoWrap
            };

            int textY = bounds.Y;
            for (int index = 0; index < lines.Length; index++)
            {
                var lineBounds = new RectangleF(
                    bounds.X,
                    textY + (index * metrics.LineHeight),
                    bounds.Width,
                    metrics.LineHeight + 2);
                var shadowBounds = lineBounds;
                shadowBounds.Offset(1, 1);
                graphics.DrawString(lines[index], font, shadowBrush, shadowBounds, textFormat);
                graphics.DrawString(lines[index], font, accentBrush, lineBounds, textFormat);
            }
        }

        private static string FormatTapeState(TapeTransportState state, bool markerStop, bool justInserted)
        {
            return state switch
            {
                TapeTransportState.Playing => "Playing",
                TapeTransportState.Stopped when markerStop => "Auto-stopped",
                TapeTransportState.Stopped when justInserted => "Inserted",
                TapeTransportState.Stopped => "Stopped",
                TapeTransportState.Ended => "Ended",
                _ => "None"
            };
        }
    }
}
