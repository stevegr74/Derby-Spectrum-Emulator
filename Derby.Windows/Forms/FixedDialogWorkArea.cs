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
        var viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, AutoScrollMinSize = contents };
        var surface = new Panel { Size = contents, Location = Point.Empty };
        Control[] controls = form.Controls.Cast<Control>().ToArray();
        form.SuspendLayout();
        surface.SuspendLayout();
        form.Controls.Clear();
        surface.Controls.AddRange(controls);
        viewport.Controls.Add(surface);
        form.Controls.Add(viewport);
        int maxWidth = Math.Max(1, workArea.Width - chrome.Width);
        int maxHeight = Math.Max(1, workArea.Height - chrome.Height);
        int width = contents.Width + (contents.Height > maxHeight ? SystemInformation.VerticalScrollBarWidth : 0);
        int height = contents.Height + (width > maxWidth ? SystemInformation.HorizontalScrollBarHeight : 0);
        form.ClientSize = new Size(Math.Min(width, maxWidth), Math.Min(height, maxHeight));
        surface.ResumeLayout();
        form.ResumeLayout();
    }
}
