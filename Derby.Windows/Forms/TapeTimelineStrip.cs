namespace Derby;

internal enum TapeMarkKind { Section, Bookmark, Stop }
internal readonly record struct TapeMark(int BlockIndex, TapeMarkKind Kind);

internal sealed class TapeTimelineStrip : Control
{
    private int count;
    private int position;
    private TapeMark[] marks = [];
    public event Action<TapeMark>? PositionSelected;

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
    }

    public void UpdatePositions(int total, int current)
    {
        if (count == total && position == current)
            return;
        (count, position) = (total, current);
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
            PositionSelected?.Invoke(selected);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        int Scale(int value) => LogicalToDeviceUnits(value);
        int left = Scale(6);
        int width = Math.Max(1, ClientSize.Width - left * 2);
        int y = Scale(13);
        int height = Scale(9);
        int X(int index) => left + (int)((long)width * index / Math.Max(1, count));
        using var track = new SolidBrush(EmulatorUiStyle.Border);
        using var elapsed = new SolidBrush(EmulatorUiStyle.AccentSoft);
        using var accent = new SolidBrush(EmulatorUiStyle.Accent);
        using var ink = new Pen(EmulatorUiStyle.Ink);
        using var marker = new Pen(EmulatorUiStyle.Warning);
        e.Graphics.FillRectangle(track, left, y, width, height);
        e.Graphics.FillRectangle(elapsed, left, y, X(position) - left, height);
        if (count != 0)
            e.Graphics.FillRectangle(accent, X(position) - Scale(1), y - Scale(2), Scale(3), height + Scale(4));
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
