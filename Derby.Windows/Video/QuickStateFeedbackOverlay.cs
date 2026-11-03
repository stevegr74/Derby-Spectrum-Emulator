namespace Derby
{
    internal sealed class QuickStateFeedbackOverlay
    {
        private const double VisibleSeconds = 2.5;

        private string? text;
        private long expiresAtTicks;

        internal void Show(string message, long nowTicks)
        {
            text = message;
            expiresAtTicks = nowTicks +
                (long)(VisibleSeconds * System.Diagnostics.Stopwatch.Frequency);
        }

        internal void Draw(
            Bitmap bitmap,
            int scale,
            long nowTicks,
            bool isStatusOverlayVisible,
            bool avoidFullTapeOverlay)
        {
            if (text == null || nowTicks >= expiresAtTicks)
                return;

            Color accent = text.EndsWith("RESTORED", StringComparison.Ordinal)
                ? Color.FromArgb(255, 190, 55)
                : Color.FromArgb(60, 220, 190);
            int width = 150 * scale;
            int height = 30 * scale;
            int x = bitmap.Width - width - (8 * scale);
            int y = isStatusOverlayVisible
                ? SpectrumStatusOverlay.GetBounds(bitmap.Width, scale, avoidFullTapeOverlay).Bottom + (8 * scale)
                : avoidFullTapeOverlay ? SpectrumStatusOverlay.TapeClearanceY * scale : 8 * scale;

            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var backgroundBrush = new SolidBrush(Color.FromArgb(210, 8, 12, 14));
            using var accentBrush = new SolidBrush(accent);
            using var borderPen = new Pen(Color.FromArgb(235, accent), Math.Max(1, scale));
            using var font = new Font(FontFamily.GenericSansSerif, 8.5f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            graphics.FillRectangle(backgroundBrush, x, y, width, height);
            graphics.DrawRectangle(borderPen, x, y, width - 1, height - 1);
            graphics.DrawString(text, font, accentBrush, x + (9 * scale), y + (9 * scale));
        }
    }
}
