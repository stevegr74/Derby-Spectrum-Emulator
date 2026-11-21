using System.Drawing;

namespace Derby;

internal sealed class CapturedPointerMotion
{
    private enum Source { Undecided, Raw, WindowsPointer }

    private Source source;
    private Point previous;
    private Point remainder;

    public void Reset(Point position)
    {
        source = Source.Undecided;
        previous = position;
        remainder = Point.Empty;
    }

    public void Rebase(Point position) => previous = position;

    public bool TryUseRawMotion()
    {
        if (source == Source.WindowsPointer)
            return false;
        source = Source.Raw;
        return true;
    }

    public Point UseWindowsPointer(Point position, int displayScale)
    {
        if (source == Source.Raw || position == previous)
            return Point.Empty;
        source = Source.WindowsPointer;
        var delta = new Point(position.X - previous.X + remainder.X, position.Y - previous.Y + remainder.Y);
        previous = position;
        // Match guest pixels rather than scaled window pixels, retaining subpixel motion.
        remainder = new Point(delta.X % displayScale, delta.Y % displayScale);
        return new Point(delta.X / displayScale, delta.Y / displayScale);
    }

    public bool UsesWindowsPointer => source == Source.WindowsPointer;
}
