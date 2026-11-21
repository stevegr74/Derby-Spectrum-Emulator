using System.Drawing;
using Xunit;

namespace Derby.Tests;

public class CapturedPointerMotionTests
{
    [Fact]
    public void TouchpadReportsImmediatelyAndKeepsSubpixelMotion()
    {
        var motion = new CapturedPointerMotion();
        motion.Reset(new Point(100, 100));
        Assert.Equal(Point.Empty, motion.UseWindowsPointer(new Point(101, 99), 3));
        Assert.Equal(Point.Empty, motion.UseWindowsPointer(new Point(102, 98), 3));
        Assert.Equal(new Point(1, -1), motion.UseWindowsPointer(new Point(103, 97), 3));
        Assert.Equal(new Point(2, -1), motion.UseWindowsPointer(new Point(109, 94), 3));
        Assert.False(motion.TryUseRawMotion());
    }

    [Fact]
    public void RawDeviceDoesNotAlsoForwardWindowsCursorMovement()
    {
        var motion = new CapturedPointerMotion();
        motion.Reset(Point.Empty);
        Assert.True(motion.TryUseRawMotion());
        Assert.Equal(Point.Empty, motion.UseWindowsPointer(new Point(5, 4), 1));
        Assert.Equal(Point.Empty, motion.UseWindowsPointer(new Point(8, 9), 1));
        Assert.True(motion.TryUseRawMotion());
        Assert.False(motion.UsesWindowsPointer);
    }

    [Fact]
    public void RecenterDoesNotAddMovementOrLoseSubpixelMotion()
    {
        var motion = new CapturedPointerMotion();
        motion.Reset(new Point(100, 100));
        Assert.Equal(new Point(3, 0), motion.UseWindowsPointer(new Point(110, 101), 3));
        motion.Rebase(new Point(200, 200));
        Assert.Equal(Point.Empty, motion.UseWindowsPointer(new Point(200, 200), 3));
        Assert.Equal(new Point(-1, 1), motion.UseWindowsPointer(new Point(196, 202), 3));
    }

    [Fact]
    public void NewCaptureCanChooseADifferentSource()
    {
        var motion = new CapturedPointerMotion();
        motion.Reset(Point.Empty);
        Assert.True(motion.TryUseRawMotion());
        motion.Reset(new Point(200, 200));
        Assert.Equal(new Point(1, 0), motion.UseWindowsPointer(new Point(201, 200), 1));
        Assert.False(motion.TryUseRawMotion());
    }
}
