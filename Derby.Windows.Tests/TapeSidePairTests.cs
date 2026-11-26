using Derby.Tape;
using Xunit;

namespace Derby.Tests;

public sealed class TapeSidePairTests
{
    [Fact]
    public void PairSurvivesRestartAndOnlyTheExactOtherImageSwapsWhileStopped()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("derby-side-pair-");
        try
        {
            string path = Path.Combine(directory.FullName, "pairs.json");
            string firstPath = WriteTap("Side 1.tap", 0x12);
            string secondPath = WriteTap("Side 2.tap", 0x34);
            TapeSideImage first = TapeSideImage.Read(firstPath);
            TapeSideImage second = TapeSideImage.Read(secondPath);
            new TapeSidePairStore(path).Add(first.Side, second.Side);

            var afterRestart = new TapeSidePairStore(path);
            Assert.Equal(second.Side, afterRestart.FindOther(first.Side.Identity));
            Assert.Equal(first.Side, afterRestart.FindOther(second.Side.Identity));
            Assert.Throws<InvalidOperationException>(() => afterRestart.Add(first.Side, second.Side));

            string moved = Path.Combine(directory.FullName, "Renamed.tap");
            File.Move(secondPath, moved);
            TapeSideImage relocated = TapeSideImage.Read(moved);
            TapeSideImage wrong = TapeSideImage.Read(WriteTap("Side 2.tap", 0x56));
            Assert.Equal(second.Side.Identity, relocated.Side.Identity);
            Assert.NotEqual(second.Side.Identity, wrong.Side.Identity);

            var machine = new SpectrumMachine(Path.Combine(AppContext.BaseDirectory, "ROMs"));
            TapLoader.Mount(machine, firstPath, startStopped: true);
            TapeTimeline before = machine.CaptureTapeDeck()!.Timeline;
            machine.Cpu.Regs.PC = 0x8000;
            machine.PokeMemory(0x8100, 0xA5);
            Assert.False(wrong.TryMount(machine, before, second.Side.Identity));
            Assert.Same(before, machine.CaptureTapeDeck()!.Timeline);
            machine.ToggleTapeTransport();
            Assert.False(relocated.TryMount(machine, before, second.Side.Identity));
            Assert.Same(before, machine.CaptureTapeDeck()!.Timeline);
            machine.ToggleTapeTransport();
            Assert.True(relocated.TryMount(machine, before, second.Side.Identity));
            Assert.Equal(TapeTransportState.Stopped, machine.TapeTransportState);
            Assert.Equal(0, machine.CaptureTapeDeck()!.BlockIndex);
            Assert.Equal(0x8000, machine.Cpu.Regs.PC);
            Assert.Equal(0xA5, machine.PeekMemory(0x8100));
            Assert.False(first.TryMount(machine, before, first.Side.Identity));
            Assert.True(first.TryMount(machine, machine.CaptureTapeDeck()!.Timeline, first.Side.Identity));
            Assert.Equal(TapeTransportState.Stopped, machine.TapeTransportState);

            afterRestart.Relocate(relocated.Side);
            Assert.Equal(relocated.Side, new TapeSidePairStore(path).FindOther(first.Side.Identity));
            afterRestart.Remove(second.Side.Identity);
            Assert.Null(new TapeSidePairStore(path).FindOther(first.Side.Identity));

            string WriteTap(string name, byte value)
            {
                string file = Path.Combine(directory.FullName, name);
                File.WriteAllBytes(file, [3, 0, 0xFF, value, (byte)(0xFF ^ value)]);
                return file;
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
