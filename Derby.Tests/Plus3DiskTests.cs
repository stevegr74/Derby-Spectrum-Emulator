using Derby.Disk;
using Xunit;

namespace Derby.Tests;

public sealed class Plus3DiskTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DskParserReadsSectorAndRejectsTruncatedTrack(bool extended)
    {
        byte[] bytes = BuildImage(extended);
        DskImage image = DskImage.Parse(bytes);
        DskImage.Sector sector = Assert.Single(image.GetTrack(0, 0));
        Assert.Equal(1, sector.R);
        Assert.All(sector.Data, value => Assert.Equal((byte)0xA5, value));
        Assert.Throws<InvalidDataException>(() => DskImage.Parse(bytes.AsSpan(0, bytes.Length - 1)));
    }

    [Fact]
    public void ExtendedImagePreservesSparseTracksAndIdOnlySectors()
    {
        byte[] bytes = BuildImage(true);
        bytes[48] = 2;
        bytes[52] = 0;
        bytes[53] = 2;
        bytes[256 + 24 + 3] = 0x0F;
        bytes[256 + 24 + 6] = 0;

        DskImage image = DskImage.Parse(bytes);
        Assert.Empty(image.GetTrack(0, 0));
        DskImage.Sector sector = Assert.Single(image.GetTrack(1, 0));
        Assert.Equal(0x0F, sector.N);
        Assert.Empty(sector.Data);
    }

    [Fact]
    public void Plus3ReadsSectorsRejectsWritesAndSwapsWithoutReset()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "ROMs");
        var machine = new SpectrumMachine(SpectrumRomSet.BundledPlus3(folder));
        var plus2A = new SpectrumMachine(SpectrumRomSet.BundledPlus2A(folder));
        DskImage disk = DskImage.Parse(BuildImage(true));
        Assert.Throws<InvalidOperationException>(() => plus2A.InsertDisk(disk, "same-roms.dsk"));
        Assert.False(plus2A.HasPlus3DiskDrive);

        machine.PokeMemory(0x8000, 0x42);
        machine.Cpu.Regs.PC = 0x8100;
        machine.InsertDisk(disk, "test.dsk");
        machine.DebugWritePort(0x7FFD, 0x20);
        machine.DebugWritePort(0x1FFD, 8);
        Assert.True(machine.PagingLocked);
        Assert.Equal(0x80, machine.DebugReadPort(0x2FFD));
        WriteCommand(machine, [0x46, 0, 0, 0, 1, 0, 1, 0x2A, 128]);
        Assert.Equal(0xF0, machine.DebugReadPort(0x2FFD));
        for (int i = 0; i < 128; i++)
            Assert.Equal(0xA5, machine.DebugReadPort(0x3FFD));
        Assert.Equal(0xD0, machine.DebugReadPort(0x2FFD));
        Assert.Equal(new byte[] { 0x40, 0x80, 0, 0, 0, 1, 0 }, ReadResult(machine));
        Assert.True(machine.IsDiskAccessActive);

        WriteCommand(machine, [0x45, 0, 0, 0, 1, 0, 1, 0x2A, 128]);
        Assert.Equal(0x02, ReadResult(machine)[1]);
        machine.InsertDisk(disk, "replacement.dsk");
        Assert.Equal("replacement.dsk", machine.MountedDiskName);
        machine.EjectDisk();
        Assert.Null(machine.MountedDiskName);
        Assert.False(machine.IsDiskAccessActive);
        Assert.Equal(0x8100, machine.Cpu.Regs.PC);
        Assert.Equal(0x42, machine.PeekMemory(0x8000));
    }

    private static void WriteCommand(SpectrumMachine machine, byte[] command)
    {
        foreach (byte value in command)
            machine.DebugWritePort(0x3FFD, value);
    }

    private static byte[] ReadResult(SpectrumMachine machine) =>
        Enumerable.Range(0, 7).Select(_ => machine.DebugReadPort(0x3FFD)).ToArray();

    private static byte[] BuildImage(bool extended)
    {
        byte[] data = new byte[768];
        if (extended)
            "EXTENDED CPC DSK File\r\nDisk-Info\r\n"u8.CopyTo(data);
        else
            "MV - CPCEMU Disk-File\r\nDisk-Info\r\n"u8.CopyTo(data);
        data[48] = 1;
        data[49] = 1;
        data[51] = 2;
        data[52] = 2;
        "Track-Info\r\n"u8.CopyTo(data.AsSpan(256));
        data[256 + 21] = 1;
        data[256 + 24 + 2] = 1;
        data[256 + 24 + 6] = 128;
        data.AsSpan(512, 128).Fill(0xA5);
        return data;
    }
}
