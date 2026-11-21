namespace Derby;

internal sealed class AmxMouse
{
    internal record struct Channel(byte Vector, byte Mode, bool InterruptEnabled,
        byte FollowupWords, int Pending, byte Direction, bool InService);
    internal record struct State(Channel X, Channel Y, SpectrumMouseButtons Buttons);

    // A reset Z80 PIO starts in Mode 1 (input), with interrupts disabled.
    private static readonly Channel ResetChannel = new() { Mode = 1 };
    private Channel x = ResetChannel;
    private Channel y = ResetChannel;
    private SpectrumMouseButtons buttons;

    public void Reset() => Restore(new(ResetChannel, ResetChannel, SpectrumMouseButtons.None));

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
            return Acknowledge(ref x);
        return Acknowledge(ref y);
    }

    private static byte Acknowledge(ref Channel channel)
    {
        bool positive = channel.Pending > 0;
        channel.Direction = positive ? (byte)0 : (byte)1;
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
