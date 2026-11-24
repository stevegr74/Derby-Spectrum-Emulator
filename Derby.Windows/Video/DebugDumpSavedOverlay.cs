using System.Diagnostics;

namespace Derby;

internal sealed class DebugDumpSavedOverlay
{
    private long expiresAtTicks;

    internal void Show(long nowTicks) => expiresAtTicks = nowTicks + Stopwatch.Frequency * 5 / 2;

    internal bool IsVisible(long nowTicks) => nowTicks < expiresAtTicks;

    internal static Rectangle GetBounds(Size image, int scale, bool mouseCaptured)
    {
        int width = 120 * scale;
        int height = 30 * scale;
        return new Rectangle(image.Width - width - 8 * scale,
            image.Height - height - (mouseCaptured ? 28 : 8) * scale, width, height);
    }

    internal void Draw(Bitmap bitmap, int scale, long nowTicks, bool mouseCaptured)
    {
        if (!IsVisible(nowTicks))
            return;

        Rectangle bounds = GetBounds(bitmap.Size, scale, mouseCaptured);
        using Graphics graphics = Graphics.FromImage(bitmap);
        using var surface = new SolidBrush(Color.FromArgb(210, 8, 12, 14));
        using var ink = new SolidBrush(Color.FromArgb(60, 220, 190));
        using var border = new Pen(Color.FromArgb(235, 60, 220, 190), scale);
        using var title = new Font(FontFamily.GenericSansSerif, 8.5f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
        using var hint = new Font(FontFamily.GenericSansSerif, 8f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        graphics.FillRectangle(surface, bounds);
        graphics.DrawRectangle(border, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        graphics.DrawString("DEBUG DUMP SAVED", title, ink, bounds.X + 7 * scale, bounds.Y + 5 * scale);
        graphics.DrawString("Shift+F12 to view dumps", hint, ink, bounds.X + 7 * scale, bounds.Y + 16 * scale);
    }
}
