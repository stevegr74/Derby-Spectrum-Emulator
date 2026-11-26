using Derby.Disk;

namespace Derby;

public sealed partial class SpectrumMachine
{
    private PlusDInterface? plusD;
    public bool HasPlusD => plusD != null;
    public string? MountedPlusDDiskName => plusD?.DiskName;
    private int PlusDByteTime => CurrentCpuClockHz / 31_250;
    private bool IsPlusDOverlay(ushort address) => address < 0x4000 && plusD?.PagedIn == true;

    public void AttachPlusD(ReadOnlySpan<byte> firmware)
    {
        if (MemoryPagingModel == SpectrumMemoryPagingModel.Plus2APlus3)
            throw new InvalidOperationException("+D requires a 48K, original 128K, or grey +2 profile.");
        if (firmware.Length != 0x2000)
            throw new InvalidDataException("The +D ROM must be exactly 8 KB.");
        plusD = new PlusDInterface(firmware);
        cpu.BeforeOpcodeFetch = plusD.ObserveOpcodeFetch;
    }

    public void InsertPlusDDisk(MgtImage image, string displayName)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (plusD == null)
            throw new InvalidOperationException("A +D interface is required.");
        plusD.Insert(image, displayName);
    }

    public void EjectPlusDDisk() => plusD?.Insert(null, null);

    private bool TryReadPlusDPort(ushort port, ulong time, out byte value)
    {
        value = 0xFF;
        return plusD?.TryReadPort(port, time, PlusDByteTime, out value) == true;
    }
}
