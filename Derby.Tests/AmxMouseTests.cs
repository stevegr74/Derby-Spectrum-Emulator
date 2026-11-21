using Derby.Input;
using Xunit;

namespace Derby.Tests;

public sealed class AmxMouseTests
{
    [Fact]
    public void AmxTakesTheJoystickPortOnlyWhileSelected()
    {
        var machine = CreateMachine();
        machine.SetJoystickState(SpectrumJoystickInterface.Kempston, SpectrumJoystickState.Fire);
        Assert.Equal(0x10, machine.DebugReadPort(0xAB1F));

        machine.SetMouseType(SpectrumMouseType.Amx);
        machine.UpdateMouse(0, 0, SpectrumMouseButtons.Left | SpectrumMouseButtons.Middle);
        Assert.Equal(0, machine.DebugReadPort(0xAB1F));
        Assert.Equal(0x3F, machine.DebugReadPort(0x00DF));
        machine.ReleaseMouseInput();
        Assert.Equal(0xFF, machine.DebugReadPort(0x00DF));

        machine.SetMouseType(SpectrumMouseType.Kempston);
        Assert.Equal(0x10, machine.DebugReadPort(0xAB1F));
        Assert.Equal(0, machine.DebugReadPort(0xFBDF));
        machine.SetMouseType(SpectrumMouseType.Off);
        Assert.Equal(0x10, machine.DebugReadPort(0xAB1F));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PioVectorsTakePriorityWithoutLosingAnActiveUlaRequest(bool persistent)
    {
        var machine = CreateMachine();
        machine.SetMouseType(SpectrumMouseType.Amx);
        ConfigureAxis(machine, 0x5F, 0x40);
        ConfigureAxis(machine, 0x7F, 0x42);
        machine.UpdateMouse(1, 1, SpectrumMouseButtons.None);
        if (persistent)
        {
            byte[] saved = machine.CapturePersistentState();
            machine.SetMouseType(SpectrumMouseType.Off);
            machine.RestorePersistentState(saved);
        }
        else
        {
            var saved = machine.CaptureQuickState();
            machine.SetMouseType(SpectrumMouseType.Off);
            machine.RestoreQuickState(saved);
        }

        machine.Cpu.Regs.I = 0x90;
        machine.Cpu.Regs.SP = 0xB000;
        machine.Cpu.Regs.PC = 0x8000;
        SetVector(machine, 0x9040, 0xA100);
        SetVector(machine, 0x9042, 0xA200);
        SetVector(machine, 0x90FF, 0xA300);
        machine.Cpu.RestoreInterruptState(true, true, 2);
        machine.Cpu.InterruptPending = true;

        machine.Cpu.ExecuteCycles(1);
        Assert.Equal(0xA100, machine.Cpu.Regs.PC);
        Assert.Equal(0, machine.DebugReadPort(0x001F) & 1);
        Assert.True(machine.Cpu.InterruptPending);
        ReturnWithReti(machine);

        machine.Cpu.ExecuteCycles(1);
        Assert.Equal(0xA200, machine.Cpu.Regs.PC);
        Assert.Equal(0, machine.DebugReadPort(0x003F) & 1);
        ReturnWithReti(machine);

        machine.Cpu.ExecuteCycles(1);
        Assert.Equal(0xA300, machine.Cpu.Regs.PC);
        Assert.False(machine.Cpu.InterruptPending);
    }

    [Fact]
    public void ResetPioAcceptsBothAxesWithoutAModeWord()
    {
        var machine = CreateMachine();
        for (int pass = 0; pass < 2; pass++)
        {
            machine.SetMouseType(SpectrumMouseType.Amx);
            machine.UpdateMouse(1, 1, SpectrumMouseButtons.None);
            Assert.False(machine.Cpu.HasPeripheralInterrupt!.Invoke());

            machine.DebugWritePort(0x045F, 0x04);
            machine.DebugWritePort(0xD25F, 0xD2);
            machine.DebugWritePort(0x875F, 0x87);
            machine.DebugWritePort(0x047F, 0x04);
            machine.DebugWritePort(0xD07F, 0xD0);
            machine.DebugWritePort(0x877F, 0x87);
            machine.UpdateMouse(1, 1, SpectrumMouseButtons.None);

            Assert.Equal((byte)0xD2, machine.Cpu.AcknowledgePeripheralInterrupt!.Invoke());
            machine.Cpu.ReturnPeripheralInterrupt!.Invoke();
            Assert.Equal((byte)0xD0, machine.Cpu.AcknowledgePeripheralInterrupt!.Invoke());
            machine.Cpu.ReturnPeripheralInterrupt!.Invoke();
            Assert.False(machine.Cpu.HasPeripheralInterrupt!.Invoke());
            machine.SetMouseType(SpectrumMouseType.Off);
        }
    }

    [Fact]
    public void MotionDirectionBitsFollowTheSpectrumPointerAxes()
    {
        var machine = CreateMachine();
        machine.SetMouseType(SpectrumMouseType.Amx);
        ConfigureAxis(machine, 0x5F, 0x40);
        ConfigureAxis(machine, 0x7F, 0x42);

        foreach (int direction in new[] { 1, -1 })
        {
            int expected = direction > 0 ? 0 : 1;
            machine.UpdateMouse(direction, direction, SpectrumMouseButtons.None);
            Assert.Equal((byte)0x40, machine.Cpu.AcknowledgePeripheralInterrupt!.Invoke());
            Assert.Equal(expected, machine.DebugReadPort(0x001F) & 1);
            machine.Cpu.ReturnPeripheralInterrupt!.Invoke();
            Assert.Equal((byte)0x42, machine.Cpu.AcknowledgePeripheralInterrupt!.Invoke());
            Assert.Equal(expected, machine.DebugReadPort(0x003F) & 1);
            machine.Cpu.ReturnPeripheralInterrupt!.Invoke();
        }
    }

    [Fact]
    public void DisabledPioDoesNotInterruptAndReleaseDropsUnsentMotion()
    {
        var machine = CreateMachine();
        machine.SetMouseType(SpectrumMouseType.Amx);
        machine.Cpu.Regs.PC = 0x8000;
        machine.Cpu.RestoreInterruptState(true, true, 2);
        machine.UpdateMouse(5, 0, SpectrumMouseButtons.None);
        Assert.False(machine.Cpu.HasPeripheralInterrupt!.Invoke());

        ConfigureAxis(machine, 0x5F, 0x40);
        machine.UpdateMouse(-5, 0, SpectrumMouseButtons.Right);
        Assert.True(machine.Cpu.HasPeripheralInterrupt!.Invoke());
        machine.ReleaseMouseInput();
        Assert.False(machine.Cpu.HasPeripheralInterrupt!.Invoke());
        Assert.Equal(0xFF, machine.DebugReadPort(0x00DF));
    }

    private static SpectrumMachine CreateMachine() => new(Path.Combine(AppContext.BaseDirectory, "ROMs"));

    private static void ConfigureAxis(SpectrumMachine machine, ushort port, byte vector)
    {
        machine.DebugWritePort(port, 0x4F);
        machine.DebugWritePort(port, vector);
        machine.DebugWritePort(port, 0x87);
    }

    private static void SetVector(SpectrumMachine machine, ushort address, ushort target)
    {
        machine.PokeMemory(address, (byte)target);
        machine.PokeMemory((ushort)(address + 1), (byte)(target >> 8));
    }

    private static void ReturnWithReti(SpectrumMachine machine)
    {
        ushort pc = machine.Cpu.Regs.PC;
        machine.PokeMemory(pc, 0xFB); // EI
        machine.PokeMemory((ushort)(pc + 1), 0xED);
        machine.PokeMemory((ushort)(pc + 2), 0x4D); // RETI
        machine.Cpu.Step();
        machine.Cpu.Step();
    }
}
