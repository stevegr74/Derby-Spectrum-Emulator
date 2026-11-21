using System.Runtime.InteropServices;

namespace Derby;

internal static class WindowsMouseInput
{
    private const uint ReadInput = 0x10000003;
    private const uint RemoveDevice = 1;

    public static bool SetRegistered(IntPtr target, bool enabled)
    {
        var device = new RawInputDevice
        {
            UsagePage = 1,
            Usage = 2,
            Flags = enabled ? 0 : RemoveDevice,
            Target = enabled ? target : IntPtr.Zero
        };
        return RegisterRawInputDevices([device], 1, (uint)Marshal.SizeOf<RawInputDevice>());
    }

    public static bool TryRead(IntPtr handle, out RawMouse mouse)
    {
        uint size = (uint)Marshal.SizeOf<RawInput>();
        uint read = GetRawInputData(handle, ReadInput, out RawInput input, ref size, (uint)Marshal.SizeOf<RawInputHeader>());
        mouse = input.Mouse;
        return read != uint.MaxValue && read >= (uint)Marshal.SizeOf<RawInput>() && input.Header.Type == 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr Parameter;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    internal struct RawMouse
    {
        [FieldOffset(0)] public ushort Flags;
        [FieldOffset(4)] public ushort ButtonFlags;
        [FieldOffset(12)] public int X;
        [FieldOffset(16)] public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInput
    {
        public RawInputHeader Header;
        public RawMouse Mouse;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(RawInputDevice[] devices, uint count, uint size);

    [DllImport("user32.dll")]
    private static extern uint GetRawInputData(IntPtr input, uint command, out RawInput data, ref uint size, uint headerSize);
}
