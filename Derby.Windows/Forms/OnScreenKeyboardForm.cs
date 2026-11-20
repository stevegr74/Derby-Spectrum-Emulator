using System.Text.Json;

namespace Derby;

internal sealed class OnScreenKeyboardForm : Form
{
    private readonly Panel keyPanel = new() { Dock = DockStyle.Fill, BackColor = EmulatorUiStyle.Panel };
    private readonly Button layout48K = new() { Text = "48K rubber-key", AutoSize = true };
    private readonly Button layout128K = new() { Text = "128K", AutoSize = true };
    private readonly List<List<KeyFace>> keyRows = [];
    private readonly Action<OnScreenKeyStroke, bool> sendStroke;
    private readonly Action cancelStrokes;
    private OnScreenShift armed;
    private KeyFace? capturedKey;
    private OnScreenKeyStroke? heldStroke;

    public OnScreenKeyboardForm(
        OnScreenKeyboardLayout layout, Action<OnScreenKeyStroke, bool> sendStroke, Action cancelStrokes)
    {
        this.sendStroke = sendStroke;
        this.cancelStrokes = cancelStrokes;
        SuspendLayout();
        Text = "DERBY On-Screen Keyboard";
        AccessibleName = Text;
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        MinimumSize = new Size(670, 460);
        ClientSize = new Size(770, 440);
        EmulatorUiStyle.ApplyForm(this);
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 10), RowCount = 3, ColumnCount = 1
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var header = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        var title = EmulatorUiStyle.CreateTitle("ON-SCREEN KEYBOARD");
        title.Margin = new Padding(2, 6, 16, 4);
        header.Controls.AddRange([title, layout48K, layout128K]);
        var hint = EmulatorUiStyle.CreateSubtitle(
            "Hold a key to keep it down. Click either Shift key to arm it." + Environment.NewLine +
            "Page Up: 48K layout   |   Page Down: 128K layout");
        hint.Margin = new Padding(2, 5, 0, 0);
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(keyPanel, 0, 1);
        root.Controls.Add(hint, 0, 2);
        Controls.Add(root);
        layout48K.Click += (_, _) => SelectLayout(OnScreenKeyboardLayout.Spectrum48K);
        layout128K.Click += (_, _) => SelectLayout(OnScreenKeyboardLayout.Spectrum128K);
        keyPanel.Resize += (_, _) => ArrangeKeys();
        SelectLayout(layout);
        AutoScaleDimensions = new SizeF(96, 96);
        ResumeLayout(true);
    }

    public OnScreenKeyboardLayout LayoutChoice { get; private set; }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Both keys are outside the Spectrum input map; navigation keys remain emulated.
        switch (keyData)
        {
            case Keys.PageUp:
                SelectLayout(OnScreenKeyboardLayout.Spectrum48K);
                layout48K.Focus();
                return true;
            case Keys.PageDown:
                SelectLayout(OnScreenKeyboardLayout.Spectrum128K);
                layout128K.Focus();
                return true;
            default:
                return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    internal void SelectLayout(OnScreenKeyboardLayout layout)
    {
        if (keyRows.Count != 0 && layout == LayoutChoice)
            return;
        if (keyRows.Count != 0)
            ClearInput();
        LayoutChoice = layout;
        EmulatorUiStyle.StyleButton(layout48K, layout == OnScreenKeyboardLayout.Spectrum48K);
        EmulatorUiStyle.StyleButton(layout128K, layout == OnScreenKeyboardLayout.Spectrum128K);
        keyPanel.SuspendLayout();
        foreach (KeyFace face in keyRows.SelectMany(row => row))
            face.Dispose();
        keyRows.Clear();
        foreach (OnScreenKeyboardRow row in OnScreenKeyboardLayouts.GetRows(layout))
        {
            var faces = new List<KeyFace>();
            foreach (OnScreenKey key in row.Keys)
            {
                var face = new KeyFace(key);
                face.MouseDown += (_, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                        Press(face);
                };
                face.MouseUp += (_, e) =>
                {
                    if (e.Button == MouseButtons.Left && capturedKey == face)
                        Release();
                };
                face.MouseCaptureChanged += (_, _) =>
                {
                    if (capturedKey == face && !face.Capture)
                        ClearInput();
                };
                faces.Add(face);
                keyPanel.Controls.Add(face);
            }
            keyRows.Add(faces);
        }
        keyPanel.ResumeLayout(false);
        ArrangeKeys();
    }

    internal void ClearInput()
    {
        KeyFace? previous = capturedKey;
        capturedKey = null;
        heldStroke = null;
        armed = OnScreenShift.None;
        if (previous != null)
            previous.Capture = false;
        cancelStrokes();
        UpdateKeyHighlights();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (!e.Cancel)
            ClearInput();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        ClearInput();
        base.OnDeactivate(e);
    }

    private void Press(KeyFace face)
    {
        if (capturedKey != null)
            return;
        OnScreenKey key = face.Key;
        if (key.Shift != OnScreenShift.None)
        {
            armed ^= key.Shift;
            // Both shift contacts together enter extended mode on the 48K keyboard.
            if (armed != (OnScreenShift.Caps | OnScreenShift.Symbol))
            {
                UpdateKeyHighlights();
                return;
            }
        }
        capturedKey = face;
        heldStroke = key.CreateStroke(armed);
        armed = OnScreenShift.None;
        face.Capture = true;
        if (!face.Capture)
        {
            ClearInput();
            return;
        }
        sendStroke(heldStroke, true);
        UpdateKeyHighlights();
    }

    private void Release()
    {
        KeyFace previous = capturedKey!;
        OnScreenKeyStroke stroke = heldStroke!;
        capturedKey = null;
        heldStroke = null;
        previous.Capture = false;
        sendStroke(stroke, false);
        UpdateKeyHighlights();
    }

    private void UpdateKeyHighlights()
    {
        foreach (KeyFace face in keyRows.SelectMany(row => row))
            face.SetPressed(face == capturedKey || (armed & face.Key.Shift) != 0);
    }

    private void ArrangeKeys()
    {
        if (keyRows.Count == 0)
            return;
        var rows = OnScreenKeyboardLayouts.GetRows(LayoutChoice);
        foreach ((int row, int column, Rectangle bounds) in GetKeyBounds(rows, keyPanel.ClientSize, DeviceDpi))
            keyRows[row][column].Bounds = bounds;
    }

    internal static IEnumerable<(int Row, int Column, Rectangle Bounds)> GetKeyBounds(
        IReadOnlyList<OnScreenKeyboardRow> rows, Size panel, int dpi)
    {
        float scale = dpi / 96f;
        int gap = (int)Math.Round(4 * scale);
        int pad = (int)Math.Round(7 * scale);
        float unit = Math.Max(1, panel.Width - 2 * pad) /
            rows.Max(row => row.Offset + row.Keys.Sum(key => key.Width));
        float height = (float)Math.Max(1, panel.Height - 2 * pad) / rows.Count;
        for (int i = 0; i < rows.Count; i++)
        {
            float x = rows[i].Offset;
            for (int j = 0; j < rows[i].Keys.Length; j++)
            {
                int left = pad + (int)Math.Round(x * unit);
                x += rows[i].Keys[j].Width;
                int top = pad + (int)Math.Round(i * height);
                yield return (i, j, new Rectangle(left, top,
                    Math.Max(1, pad + (int)Math.Round(x * unit) - left - gap),
                    Math.Max(1, pad + (int)Math.Round((i + 1) * height) - top - gap)));
            }
        }
    }

    private sealed class KeyFace : Control
    {
        private bool isPressed;
        public OnScreenKey Key { get; }

        public KeyFace(OnScreenKey key)
        {
            Key = key;
            AccessibleName = key.Legend.Replace('\n', ' ');
            AccessibleRole = AccessibleRole.PushButton;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.Selectable, false);
        }

        public void SetPressed(bool pressed)
        {
            if (isPressed == pressed)
                return;
            isPressed = pressed;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(isPressed ? EmulatorUiStyle.AccentSoft : EmulatorUiStyle.Field);
            ControlPaint.DrawBorder(e.Graphics, ClientRectangle,
                isPressed ? EmulatorUiStyle.Accent : EmulatorUiStyle.Border, ButtonBorderStyle.Solid);
            PaintLegends(e.Graphics, Key, ClientRectangle, DeviceDpi);
        }
    }

    internal readonly record struct LegendMetrics(float MainPt, float DetailPt, bool Fits);

    internal static LegendMetrics PaintLegends(Graphics graphics, OnScreenKey key, Rectangle bounds, int dpi)
    {
        const TextFormatFlags Measure = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        const TextFormatFlags Center = Measure | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
        int padding = (int)Math.Round(2 * dpi / 96f);
        Rectangle face = Rectangle.Inflate(bounds, -padding, -padding);
        string[] mainLines = key.Legend.Split('\n');
        if (face.Width <= 0 || face.Height <= 0)
            return default;

        // Reserve separate bands for the red symbol, main legend and BASIC keyword.
        for (float reduction = 0; reduction <= 4; reduction += .5f)
        {
            float mainPt = (key.Legend.Length == 1 ? 12f : 8.5f) - reduction;
            float detailPt = 7.5f - reduction;
            using var mainFont = new Font("Segoe UI", mainPt * dpi / 72f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var detailFont = new Font("Segoe UI", detailPt * dpi / 72f, FontStyle.Bold, GraphicsUnit.Pixel);
            Size[] mainSizes = mainLines.Select(line => MeasureLegend(line, mainFont)).ToArray();
            Size symbol = MeasureLegend(key.Symbol, detailFont);
            Size keyword = MeasureLegend(key.Keyword, detailFont);
            int height = mainSizes.Sum(size => size.Height);
            if (height + symbol.Height + keyword.Height > face.Height ||
                mainSizes.Any(size => size.Width > face.Width) || symbol.Width > face.Width || keyword.Width > face.Width)
                continue;

            if (symbol.Height != 0)
                TextRenderer.DrawText(graphics, key.Symbol, detailFont,
                    new Rectangle(face.X, face.Y, face.Width, symbol.Height),
                    Color.FromArgb(155, 54, 65), Measure | TextFormatFlags.Right);
            int y = face.Y + symbol.Height + (face.Height - symbol.Height - keyword.Height - height) / 2;
            for (int i = 0; i < mainLines.Length; i++)
            {
                TextRenderer.DrawText(graphics, mainLines[i], mainFont,
                    new Rectangle(face.X, y, face.Width, mainSizes[i].Height), EmulatorUiStyle.Ink, Center);
                y += mainSizes[i].Height;
            }
            if (keyword.Height != 0)
                TextRenderer.DrawText(graphics, key.Keyword, detailFont,
                    new Rectangle(face.X, face.Bottom - keyword.Height, face.Width, keyword.Height),
                    EmulatorUiStyle.Muted, Center);
            return new LegendMetrics(mainPt, detailPt, true);
        }
        return default;

        Size MeasureLegend(string text, Font font) => text.Length == 0
            ? Size.Empty
            : TextRenderer.MeasureText(graphics, text, font, new Size(int.MaxValue, int.MaxValue), Measure);
    }
}

internal sealed record OnScreenKeyboardSettings(OnScreenKeyboardLayout Layout, Rectangle? Bounds)
{
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Derby", "keyboard-settings.json");

    public static OnScreenKeyboardSettings? Load(string path)
    {
        try
        {
            var settings = JsonSerializer.Deserialize<OnScreenKeyboardSettings>(File.ReadAllText(path));
            return settings != null && Enum.IsDefined(settings.Layout) ? settings : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public void Save(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, JsonSerializer.Serialize(this));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Keyboard access is still useful when preferences cannot be written.
        }
    }

    public static Rectangle Place(Rectangle? saved, Size defaultSize, Size minimumSize, Rectangle owner, Rectangle area)
    {
        Size requested = saved?.Size ?? defaultSize;
        var size = new Size(Math.Min(area.Width, Math.Max(requested.Width, minimumSize.Width)),
            Math.Min(area.Height, Math.Max(requested.Height, minimumSize.Height)));
        const int Gap = 8;
        Point[] candidates =
        [
            new(owner.Left, owner.Bottom + Gap), new(owner.Left, owner.Top - size.Height - Gap),
            new(owner.Right + Gap, owner.Top), new(owner.Left - size.Width - Gap, owner.Top)
        ];
        if (saved is Rectangle remembered && area.Contains(new Rectangle(remembered.Location, size)))
            return new Rectangle(remembered.Location, size);
        foreach (Point point in candidates)
        {
            Rectangle bounds = Clamp(point, size, area);
            if (!bounds.IntersectsWith(owner))
                return bounds;
        }
        return Clamp(candidates[0], size, area);
    }

    private static Rectangle Clamp(Point point, Size size, Rectangle area) => new(
        Math.Clamp(point.X, area.Left, area.Right - size.Width),
        Math.Clamp(point.Y, area.Top, area.Bottom - size.Height), size.Width, size.Height);
}
