namespace Derby;

internal static class FixedDialogWorkArea
{
    public static void Enable(Form form, Func<Rectangle>? getWorkArea = null) => form.Load += (_, _) =>
        Fit(form, getWorkArea?.Invoke() ?? Screen.FromControl(form.Owner ?? form).WorkingArea);

    private static void Fit(Form form, Rectangle workArea)
    {
        if (form.Width <= workArea.Width && form.Height <= workArea.Height)
            return;

        Size contents = form.ClientSize;
        Size chrome = form.Size - form.ClientSize;
        int maxWidth = Math.Max(1, workArea.Width - chrome.Width);
        int maxHeight = Math.Max(1, workArea.Height - chrome.Height);
        int barWidth = SystemInformation.VerticalScrollBarWidth;
        int barHeight = SystemInformation.HorizontalScrollBarHeight;
        int width = contents.Width + (contents.Height > maxHeight ? barWidth : 0);
        int height = contents.Height + (width > maxWidth ? barHeight : 0);
        var viewport = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            // Leave room for the other scrollbar at the end of each clipped axis.
            AutoScrollMinSize = new Size(contents.Width + (width > maxWidth ? 2 * barWidth : 0),
                contents.Height + (height > maxHeight ? 2 * barHeight : 0))
        };
        var surface = new Panel { Size = contents, Location = Point.Empty };
        Control[] controls = form.Controls.Cast<Control>().ToArray();
        form.SuspendLayout();
        surface.SuspendLayout();
        form.Controls.Clear();
        surface.Controls.AddRange(controls);
        viewport.Controls.Add(surface);
        form.Controls.Add(viewport);
        form.ClientSize = new Size(Math.Min(width, maxWidth), Math.Min(height, maxHeight));
        surface.ResumeLayout();
        form.ResumeLayout();
    }
}
