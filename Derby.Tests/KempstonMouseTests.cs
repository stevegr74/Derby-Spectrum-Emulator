using Derby.Input;
using Xunit;

namespace Derby.Tests;

public sealed class KempstonMouseTests
{
    [Theory]
    [InlineData(SpectrumMachineModel.Spectrum48K)]
    [InlineData(SpectrumMachineModel.Spectrum128K)]
    public void MousePortsAreOptInAndLeaveTheJoystickAddressSpaceAlone(SpectrumMachineModel model)
    {
        var machine = CreateMachine();
        machine.Reset(model);
        machine.SetJoystickState(SpectrumJoystickInterface.Kempston, SpectrumJoystickState.Right | SpectrumJoystickState.Fire);
        Assert.False(machine.IsKempstonMouseEnabled);
        Assert.Equal(0xFF, machine.DebugReadPort(0xFBDF));

        machine.SetMouseType(SpectrumMouseType.Kempston);
        machine.UpdateMouse(-1, 258, SpectrumMouseButtons.Left);
        Assert.Equal(0xFF, machine.DebugReadPort(0xFBDF));
        Assert.Equal(2, machine.DebugReadPort(0xFFDF));
        Assert.Equal(0xFD, machine.DebugReadPort(0xFADF));
        Assert.Equal(0x11, machine.DebugReadPort(0x001F));
        Assert.Equal(0x11, machine.DebugReadPort(0xFA1F));
        machine.UpdateMouse(2, -3, SpectrumMouseButtons.Right);
        Assert.Equal(1, machine.DebugReadPort(0xFBDF));
        Assert.Equal(0xFF, machine.DebugReadPort(0xFFDF));
        Assert.Equal(0xFE, machine.DebugReadPort(0xFADF));
        machine.UpdateMouse(0, 0, SpectrumMouseButtons.None);
        Assert.Equal(0xFF, machine.DebugReadPort(0xFADF));
        machine.SetMouseType(SpectrumMouseType.Off);
        Assert.Equal(0xFF, machine.DebugReadPort(0xFBDF));
        Assert.Equal(0x11, machine.DebugReadPort(0x001F));
    }

    [Fact]
    public void Z80TimedReadsSeeMouseAndJoystickOnSeparatePorts()
    {
        var machine = CreateMachine();
        machine.SetMouseType(SpectrumMouseType.Kempston);
        machine.UpdateMouse(47, 13, SpectrumMouseButtons.Left);
        machine.SetJoystickState(SpectrumJoystickInterface.Kempston, SpectrumJoystickState.Left | SpectrumJoystickState.Up);
        machine.PokeMemory(0x8000, 0xED);
        machine.PokeMemory(0x8001, 0x78); // IN A,(C)
        foreach ((ushort port, byte expected) in new (ushort, byte)[]
        {
            (0xFBDF, 47), (0xFFDF, 13), (0xFADF, 0xFD), (0x001F, 0x0A)
        })
        {
            machine.Cpu.Regs.PC = 0x8000;
            machine.Cpu.Regs.BC = port;
            machine.Cpu.Step();
            Assert.Equal(expected, machine.Cpu.Regs.A);
        }
    }

    [Fact]
    public void QuickStatesRestoreCountersAndOldPersistentPayloadsStillLoad()
    {
        var machine = CreateMachine();
        machine.SetMouseType(SpectrumMouseType.Kempston);
        machine.UpdateMouse(12, 34, SpectrumMouseButtons.Left);
        var quick = machine.CaptureQuickState();
        byte[] persistent = machine.CapturePersistentState();
        machine.SetMouseType(SpectrumMouseType.Off);
        machine.RestoreQuickState(quick);
        Assert.True(machine.IsKempstonMouseEnabled);
        Assert.Equal(12, machine.DebugReadPort(0xFBDF));
        Assert.Equal(0xFD, machine.DebugReadPort(0xFADF));

        machine.RestorePersistentState(persistent);
        Assert.True(machine.IsKempstonMouseEnabled);
        Assert.Equal(12, machine.DebugReadPort(0xFBDF));
        Assert.Equal(34, machine.DebugReadPort(0xFFDF));
        Assert.Equal(0xFF, machine.DebugReadPort(0xFADF));

        byte[] legacy = persistent[..^17];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(legacy.AsSpan(6, 4), 2);
        machine.RestorePersistentState(legacy);
        Assert.True(machine.IsKempstonMouseEnabled);
        Assert.Equal(12, machine.DebugReadPort(0xFBDF));

        legacy = legacy[..^3];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(legacy.AsSpan(6, 4), 1);
        machine.RestorePersistentState(legacy);
        Assert.False(machine.IsKempstonMouseEnabled);
    }

    private static SpectrumMachine CreateMachine() => new(Path.Combine(AppContext.BaseDirectory, "ROMs"));
}
