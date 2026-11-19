using Derby.Disk;

namespace Derby;

public sealed partial class SpectrumMachine
{
    private readonly Plus3DiskController diskController = new();
    public bool HasPlus3DiskDrive => RomSet.HasPlus3DiskDrive && MachineModel == SpectrumMachineModel.Spectrum128K;
    public string? MountedDiskName { get; private set; }
    public bool IsDiskAccessActive =>
        HasPlus3DiskDrive && diskController.LastAccess is ulong last &&
        Cpu.TStates >= last && Cpu.TStates - last < (ulong)(CurrentCpuClockHz / 3);

    public void InsertDisk(DskImage image, string displayName)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (!HasPlus3DiskDrive)
            throw new InvalidOperationException("A Spectrum +3 disk drive is required.");
        diskController.SetDisk(image);
        MountedDiskName = displayName;
    }

    public void EjectDisk()
    {
        diskController.SetDisk(null);
        MountedDiskName = null;
    }

    private bool TryReadDiskPort(ushort port, ulong now, out byte value)
    {
        value = 0xFF;
        if (!HasPlus3DiskDrive)
            return false;
        switch (port & 0xF002)
        {
            case 0x2000:
                value = diskController.Status;
                return true;
            case 0x3000:
                value = diskController.ReadData(now);
                return true;
            default:
                return false;
        }
    }
}
