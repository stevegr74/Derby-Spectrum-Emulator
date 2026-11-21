namespace Derby;

[Flags]
public enum SpectrumMouseButtons : byte
{
    None = 0,
    Right = 1,
    Left = 2,
    Middle = 4
}

public enum SpectrumMouseType : byte
{
    Off,
    Amx,
    Kempston
}

public sealed partial class SpectrumMachine
{
    internal readonly record struct KempstonMouseState(bool Enabled, byte X, byte Y, SpectrumMouseButtons Buttons);
    private KempstonMouseState kempstonMouse;
    private readonly AmxMouse amxMouse = new();

    public SpectrumMouseType MouseType { get; private set; }
    public bool IsKempstonMouseEnabled => MouseType == SpectrumMouseType.Kempston;

    public void SetMouseType(SpectrumMouseType type)
    {
        if (MouseType == type)
            return;
        kempstonMouse = default;
        amxMouse.Restore(default);
        RestoreMouseType(type);
    }

    private void RestoreMouseType(SpectrumMouseType type)
    {
        MouseType = type;
        kempstonMouse = kempstonMouse with { Enabled = type == SpectrumMouseType.Kempston };
        cpu.HasPeripheralInterrupt = type == SpectrumMouseType.Amx ? () => amxMouse.HasInterrupt : null;
        cpu.AcknowledgePeripheralInterrupt = type == SpectrumMouseType.Amx ? amxMouse.Acknowledge : null;
        cpu.ReturnPeripheralInterrupt = type == SpectrumMouseType.Amx ? amxMouse.ReturnFromInterrupt : null;
    }

    public void ReleaseMouseInput()
    {
        kempstonMouse = kempstonMouse with { Buttons = SpectrumMouseButtons.None };
        amxMouse.ReleaseInput();
    }

    public void UpdateMouse(int deltaX, int deltaY, SpectrumMouseButtons buttons)
    {
        if (MouseType == SpectrumMouseType.Amx)
        {
            amxMouse.SetInput(deltaX, deltaY, buttons);
            return;
        }
        if (!IsKempstonMouseEnabled)
            return;
        kempstonMouse = kempstonMouse with
        {
            X = unchecked((byte)(kempstonMouse.X + deltaX)),
            Y = unchecked((byte)(kempstonMouse.Y + deltaY)),
            Buttons = buttons & (SpectrumMouseButtons.Left | SpectrumMouseButtons.Right)
        };
    }

    private bool TryReadMousePort(ushort port, out byte value)
    {
        if (MouseType == SpectrumMouseType.Amx)
            return amxMouse.TryRead(port, out value);
        if (IsKempstonMouseEnabled)
        {
            switch (port)
            {
                case 0xFBDF:
                    value = kempstonMouse.X;
                    return true;
                case 0xFFDF:
                    value = kempstonMouse.Y;
                    return true;
                case 0xFADF:
                    value = unchecked((byte)~(byte)kempstonMouse.Buttons);
                    return true;
            }
        }
        value = 0;
        return false;
    }
}
