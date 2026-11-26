namespace Derby.Disk;

internal sealed class PlusDInterface(ReadOnlySpan<byte> firmware)
{
    private readonly byte[] rom = firmware.ToArray();
    private readonly byte[] ram = new byte[0x2000];
    private readonly PlusDController controller = new();
    public bool PagedIn { get; private set; }
    public string? DiskName { get; private set; }

    public void Reset()
    {
        PagedIn = false;
        Array.Clear(ram);
        controller.Reset();
    }

    public void Insert(MgtImage? disk, string? name)
    {
        controller.SetDisk(disk);
        DiskName = name;
    }

    public void ObserveOpcodeFetch(ushort address)
    {
        if (address is 0x0008 or 0x003A or 0x0066)
            PagedIn = true;
    }

    public byte ReadMemory(ushort address) => address < 0x2000
        ? rom[address]
        : ram[address - 0x2000];

    public void WriteMemory(ushort address, byte value)
    {
        if (address >= 0x2000)
            ram[address - 0x2000] = value;
    }

    public bool TryReadPort(ushort port, ulong now, int byteTime, out byte value)
    {
        value = 0xFF;
        switch (port & 0x7E)
        {
            case 0x62 or 0x6A or 0x72 or 0x7A:
                value = controller.ReadRegister((port >> 3) & 3, now, byteTime);
                return true;
            case 0x66:
                PagedIn = true;
                return true;
            default:
                return false;
        }
    }

    public bool TryWritePort(ushort port, byte value, ulong now, int byteTime)
    {
        switch (port & 0x7E)
        {
            case 0x62 or 0x6A or 0x72 or 0x7A:
                controller.WriteRegister((port >> 3) & 3, value, now, byteTime);
                return true;
            case 0x66:
                PagedIn = false;
                return true;
            case 0x6E:
                controller.SelectDrive(value);
                return true;
            default:
                return false;
        }
    }
}
