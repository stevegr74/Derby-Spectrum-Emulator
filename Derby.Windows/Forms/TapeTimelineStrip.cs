namespace Derby;

internal enum TapeMarkKind { Section, Bookmark, Stop }
internal readonly record struct TapeMark(int BlockIndex, TapeMarkKind Kind);

internal sealed class TapeTimelineStrip : Control
{
    private int count;
    private double position;
    private double? dragPosition;
    private bool canScrub;
    private TapeMark[] marks = [];
    public event Action<TapeMark>? PositionSelected;
    public event Action<double>? ScrubRequested;

    public void UpdateMarks(TapeMark[] positions)
    {
        marks = positions;
        Invalidate();
    }

    public TapeTimelineStrip()
    {
        Height = 48;
        Dock = DockStyle.Fill;
        TabStop = false;
        Font = EmulatorUiStyle.UiFont(8F);
        DoubleBuffered = true;
        AccessibleName = "Tape progress";
        AccessibleDescription = "While stopped, use Left or Right to seek, or Home and End.";
    }

    public void UpdatePositions(int total, double current, bool scrub)
    {
        if (count == total && position == current && canScrub == scrub)
            return;
        (count, position, canScrub) = (total, current, scrub);
        TabStop = scrub;
        if (!scrub && dragPosition != null)
            Capture = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || count == 0)
            return;
        int left = LogicalToDeviceUnits(6);
        int width = Math.Max(1, ClientSize.Width - left * 2);
        TapeMark? nearest = null;
        int distance = LogicalToDeviceUnits(7);
        foreach (TapeMark mark in marks)
        {
            int x = left + (int)((long)width * mark.BlockIndex / count);
            int y = LogicalToDeviceUnits(MarkHeight(mark.Kind));
            int candidate = Math.Max(Math.Abs(e.X - x), Math.Abs(e.Y - y));
            if (candidate < distance)
                (nearest, distance) = (mark, candidate);
        }
        if (nearest is { } selected)
        {
            PositionSelected?.Invoke(selected);
            return;
        }
        if (canScrub && e.Y >= LogicalToDeviceUnits(12) && e.Y <= LogicalToDeviceUnits(23))
        {
            Focus();
            Capture = true;
            dragPosition = PositionAt(e.X);
            Invalidate();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = canScrub && (dragPosition != null ||
            e.Y >= LogicalToDeviceUnits(12) && e.Y <= LogicalToDeviceUnits(23)) ? Cursors.Hand : Cursors.Default;
        if (dragPosition != null)
        {
            dragPosition = PositionAt(e.X);
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || dragPosition == null)
            return;
        Capture = false;
        if (canScrub)
            ScrubRequested?.Invoke(PositionAt(e.X));
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture)
        {
            dragPosition = null;
            Invalidate();
        }
    }

    protected override bool IsInputKey(Keys keyData) => canScrub &&
        keyData is Keys.Left or Keys.Right or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        double step = count * 0.01;
        double? target = e.KeyCode switch
        {
            Keys.Left => position - step, Keys.Right => position + step,
            Keys.Home => 0, Keys.End => count, _ => null
        };
        if (canScrub && target is double value && e.Modifiers == Keys.None)
        {
            ScrubRequested?.Invoke(Math.Clamp(value, 0, count));
            e.Handled = true;
        }
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    private double PositionAt(int x)
    {
        int left = LogicalToDeviceUnits(6);
        return count * Math.Clamp((double)(x - left) / Math.Max(1, ClientSize.Width - left * 2), 0, 1);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        int Scale(int value) => LogicalToDeviceUnits(value);
        int left = Scale(6);
        int width = Math.Max(1, ClientSize.Width - left * 2);
        int y = Scale(13);
        int height = Scale(9);
        int X(double index) => left + (int)(width * index / Math.Max(1, count));
        int head = X(dragPosition ?? position);
        using var track = new SolidBrush(EmulatorUiStyle.Border);
        using var elapsed = new SolidBrush(EmulatorUiStyle.AccentSoft);
        using var accent = new SolidBrush(EmulatorUiStyle.Accent);
        using var ink = new Pen(EmulatorUiStyle.Ink);
        using var marker = new Pen(EmulatorUiStyle.Warning);
        e.Graphics.FillRectangle(track, left, y, width, height);
        e.Graphics.FillRectangle(elapsed, left, y, head - left, height);
        if (count != 0)
            e.Graphics.FillRectangle(accent, head - Scale(1), y - Scale(2), Scale(3), height + Scale(4));
        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(e.Graphics, new(left - Scale(3), y - Scale(3), width + Scale(6), height + Scale(6)));
        foreach (TapeMark mark in marks)
        {
            int x = X(mark.BlockIndex);
            int markY = Scale(MarkHeight(mark.Kind));
            switch (mark.Kind)
            {
                case TapeMarkKind.Bookmark:
                    e.Graphics.FillEllipse(accent, x - Scale(3), markY - Scale(3), Scale(6), Scale(6));
                    break;
                case TapeMarkKind.Section:
                    e.Graphics.DrawLine(ink, x, markY - Scale(3), x, markY + Scale(3));
                    break;
                case TapeMarkKind.Stop:
                    e.Graphics.DrawLine(marker, x, markY - Scale(2), x, markY + Scale(2));
                    break;
            }
        }

        var captions = new Rectangle(left, y + height + Scale(6), width, Scale(18));
        TextRenderer.DrawText(e.Graphics, "Start", Font, captions, EmulatorUiStyle.Muted, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(e.Graphics, "End", Font, captions, EmulatorUiStyle.Muted,
            TextFormatFlags.NoPadding | TextFormatFlags.Right);
        if (marks.Length > 0 && width >= Scale(325))
            TextRenderer.DrawText(e.Graphics, "Ticks: sections  |  Dots: bookmarks  |  Amber: last stop", Font, captions,
                EmulatorUiStyle.Muted, TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter);
    }

    private static int MarkHeight(TapeMarkKind kind) => kind switch
    {
        TapeMarkKind.Bookmark => 8,
        TapeMarkKind.Stop => 26,
        _ => 17
    };
}
