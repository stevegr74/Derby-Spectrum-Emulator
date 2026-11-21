namespace Derby;

internal sealed class AmxMouse
{
    internal record struct Channel(byte Vector, byte Mode, bool InterruptEnabled,
        byte FollowupWords, int Pending, byte Direction, bool InService);
    internal record struct State(Channel X, Channel Y, SpectrumMouseButtons Buttons);

    private Channel x;
    private Channel y;
    private SpectrumMouseButtons buttons;

    public State Capture() => new(x, y, buttons);

    public void Restore(State state)
    {
        x = state.X;
        y = state.Y;
        buttons = state.Buttons;
    }

    public void SetInput(int dx, int dy, SpectrumMouseButtons newButtons)
    {
        QueueMotion(ref x, dx);
        QueueMotion(ref y, dy);
        buttons = newButtons;
    }

    public void ReleaseInput()
    {
        x.Pending = y.Pending = 0;
        buttons = SpectrumMouseButtons.None;
    }

    private static void QueueMotion(ref Channel channel, int delta)
    {
        if (channel.Mode == 1 && channel.InterruptEnabled)
            channel.Pending = (int)Math.Clamp((long)channel.Pending + delta, -256, 256);
    }

    public bool HasInterrupt => !x.InService && (x.Pending != 0 || (!y.InService && y.Pending != 0));

    public byte? Acknowledge()
    {
        if (!HasInterrupt)
            return null;
        if (x.Pending != 0)
            return Acknowledge(ref x, reverseDirection: true);
        return Acknowledge(ref y, reverseDirection: false);
    }

    private static byte Acknowledge(ref Channel channel, bool reverseDirection)
    {
        bool positive = channel.Pending > 0;
        channel.Direction = (byte)(positive != reverseDirection ? 1 : 0);
        channel.Pending -= positive ? 1 : -1;
        channel.InService = true;
        return channel.Vector;
    }

    public void ReturnFromInterrupt()
    {
        if (x.InService)
            x.InService = false;
        else
            y.InService = false;
    }

    public bool TryRead(ushort port, out byte value)
    {
        switch (port & 0xFF)
        {
            case 0x1F: value = x.Direction; return true;
            case 0x3F: value = y.Direction; return true;
            case 0xDF:
                value = (byte)(0xFF
                    ^ (buttons.HasFlag(SpectrumMouseButtons.Left) ? 0x80 : 0)
                    ^ (buttons.HasFlag(SpectrumMouseButtons.Middle) ? 0x40 : 0)
                    ^ (buttons.HasFlag(SpectrumMouseButtons.Right) ? 0x20 : 0));
                return true;
            default: value = 0; return false;
        }
    }

    public void WriteControl(ushort port, byte value)
    {
        switch (port & 0xFF)
        {
            case 0x5F: WriteControl(ref x, value); break;
            case 0x7F: WriteControl(ref y, value); break;
        }
    }

    private static void WriteControl(ref Channel channel, byte value)
    {
        if (channel.FollowupWords > 0)
        {
            channel.FollowupWords--;
            return;
        }
        if ((value & 1) == 0)
        {
            channel.Vector = value;
        }
        else if ((value & 0x0F) == 0x0F)
        {
            channel.Mode = (byte)(value >> 6);
            channel.FollowupWords = channel.Mode == 3 ? (byte)1 : (byte)0;
            channel.Pending = 0;
        }
        else if ((value & 0x0F) is 0x03 or 0x07)
        {
            channel.InterruptEnabled = (value & 0x80) != 0;
            if (!channel.InterruptEnabled)
                channel.Pending = 0;
            if (channel.Mode == 3 && (value & 0x1F) == 0x17)
                channel.FollowupWords = 1;
        }
    }
}
