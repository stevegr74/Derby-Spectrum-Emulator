using Derby.Disk;
using Xunit;

namespace Derby.Tests;

public sealed class PlusDTests
{
    private static string RomFolder => Path.Combine(AppContext.BaseDirectory, "ROMs");

    [Fact]
    public void MgtUsesAlternatingSidesAndOwnsItsReadOnlySectorBytes()
    {
        byte[] bytes = new byte[MgtImage.ImageSize];
        bytes[0] = 0x9A;
        bytes[10 * 512] = 0x34;
        bytes[20 * 512] = 0x56;
        bytes[^1] = 0x78;

        var image = MgtImage.Parse(bytes);
        bytes[0] = 0;
        Assert.Equal(0x9A, image.GetSector(0, 0, 1)[0]);
        Assert.Equal(0x34, image.GetSector(0, 1, 1)[0]);
        Assert.Equal(0x56, image.GetSector(1, 0, 1)[0]);
        Assert.Equal(0x78, image.GetSector(79, 1, 10)[^1]);
        Assert.True(image.GetSector(80, 0, 1).IsEmpty);
        Assert.True(image.GetSector(0, 0, 0).IsEmpty);
        Assert.Throws<InvalidDataException>(() => MgtImage.Parse(bytes.AsSpan(1)));
    }

    [Fact]
    public void PlusDOverlaysRomAndLocalRamOnFetchButNotOnInspection()
    {
        var machine = new SpectrumMachine(RomFolder);
        byte original = machine.PeekMemory(8);
        byte[] rom = new byte[8192];
        rom[8] = 0x3E; // LD A,$42
        rom[9] = 0x42;
        machine.AttachPlusD(rom);

        Assert.Equal(original, machine.PeekMemory(8));
        machine.Cpu.Regs.PC = 8;
        machine.Cpu.ExecuteCycles(7);
        Assert.Equal(0x42, machine.Cpu.Regs.A);
        machine.PokeMemory(0x2000, 0xA5);
        machine.PokeMemory(8, 0);
        Assert.Equal(0x3E, machine.PeekMemory(8));
        machine.DebugWritePort(0x00E7, 0);
        Assert.Equal(original, machine.PeekMemory(8));
        machine.DebugReadPort(0x12E7);
        Assert.Equal(0xA5, machine.PeekMemory(0x2000));

        Assert.Throws<NotSupportedException>(() => machine.CaptureQuickState());
        Assert.False(machine.HasPlus3DiskDrive);
        var plus3 = new SpectrumMachine(SpectrumRomSet.BundledPlus3(RomFolder));
        Assert.Throws<InvalidOperationException>(() => plus3.AttachPlusD(rom));
        Assert.True(plus3.HasPlus3DiskDrive);
    }

    [Fact]
    public void PlusDReadOnlyDriveProvidesIndexAndPacedSectorReads()
    {
        var machine = new SpectrumMachine(RomFolder);
        machine.AttachPlusD(new byte[8192]);
        byte[] bytes = new byte[MgtImage.ImageSize];
        bytes.AsSpan(10 * MgtImage.SectorSize, MgtImage.SectorSize).Fill(0xA5);
        var image = MgtImage.Parse(bytes);
        machine.InsertPlusDDisk(image, "side-test.mgt");
        machine.DebugWritePort(0x00EF, 0x81); // Drive 1, side 1.
        machine.DebugWritePort(0x00E3, 0xD0);
        Assert.Equal(0x06, machine.DebugReadPort(0x00E3) & 0x06); // Track zero and index.
        machine.Cpu.AddTStates(20_000);
        Assert.Equal(0x04, machine.DebugReadPort(0x00E3) & 0x06);

        machine.DebugWritePort(0x00F3, 1);
        machine.DebugWritePort(0x00E3, 0x80);
        Assert.Equal(1, machine.DebugReadPort(0x00E3) & 3); // Busy before DRQ.
        ulong byteTime = (ulong)(machine.CurrentCpuClockHz / 31_250);
        for (int i = 0; i < MgtImage.SectorSize; i++)
        {
            machine.Cpu.AddTStates(byteTime);
            Assert.Equal(3, machine.DebugReadPort(0x00E3) & 3);
            Assert.Equal(0xA5, machine.DebugReadPort(0x00FB));
        }
        Assert.Equal(0, machine.DebugReadPort(0x00E3) & 3);

        machine.DebugWritePort(0x00E3, 0xA0);
        Assert.Equal(0x40, machine.DebugReadPort(0x00E3) & 0x40);
        machine.DebugWritePort(0x00FB, 0);
        Assert.Equal(0xA5, image.GetSector(0, 1, 1)[0]);
        machine.EjectPlusDDisk();
        Assert.Null(machine.MountedPlusDDiskName);
        machine.DebugWritePort(0x00E3, 0x80);
        Assert.Equal(0x10, machine.DebugReadPort(0x00E3) & 0x10);
    }
}
