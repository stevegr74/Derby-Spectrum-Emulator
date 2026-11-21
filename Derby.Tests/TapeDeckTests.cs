using Derby.Tape;
using Xunit;

namespace Derby.Tests;

public sealed class TapeDeckTests
{
    private static SpectrumMachine CreateMachine() => new(Path.Combine(AppContext.BaseDirectory, "ROMs"));

    [Fact]
    public void CueRestoresRemainingPulseAndEarAfterSideSwapWithoutRestoringMachine()
    {
        var machine = CreateMachine();
        TapeBlock[] sideA = [TapeBlock.CreatePureTone(100, 10), TapeBlock.CreatePureTone(210, 14)];
        var tape = new MountedTape("side A.tzx", sideA, initialEarLevelHigh: true);
        machine.MountTape(tape);
        machine.IdentifyTapeImage([1, 2]);
        ulong start = machine.Cpu.TStates + 100;
        tape.ReadEarBit(start);
        machine.Cpu.AddTStates(235);
        machine.ToggleTapeTransport();
        TapeDeckView original = machine.CaptureTapeDeck()!;
        TapeCue cue = Assert.IsType<TapeCue>(original.Markers.Single().Cue);
        bool level = tape.ReadEarBit(machine.Cpu.TStates);
        Assert.Equal(0, cue.BlockIndex);

        machine.MountTape(new MountedTape("side B.tzx", sideA));
        machine.IdentifyTapeImage([3, 4]);
        machine.ToggleTapeTransport();
        Assert.False(machine.TrySeekTape(machine.CaptureTapeDeck()!.Timeline, 0, cue));

        machine.MountTape(new MountedTape("renamed side A.tzx", sideA, initialEarLevelHigh: false));
        machine.IdentifyTapeImage([1, 2]);
        Assert.Equal(original.Timeline.Identity, machine.CaptureTapeDeck()!.Timeline.Identity);
        Assert.False(machine.TrySeekTape(machine.CaptureTapeDeck()!.Timeline, 0, cue));
        machine.ToggleTapeTransport();
        machine.Cpu.AddTStates(500);
        machine.Cpu.Regs.PC = 0x8000;
        machine.PokeMemory(0x8123, 0xA5);
        Assert.True(machine.TrySeekTape(machine.CaptureTapeDeck()!.Timeline, 0, cue));
        Assert.Equal(TapeTransportTransition.ManualSeek, machine.LastTapeTransportTransition);
        Assert.Equal(TapeTransportState.Stopped, machine.TapeTransportState);
        Assert.Equal(0x8000, machine.Cpu.Regs.PC);
        Assert.Equal(0xA5, machine.PeekMemory(0x8123));
        MountedTape restored = machine.MountedTape!;
        Assert.Equal(level, restored.ReadEarBit(machine.Cpu.TStates));
        ulong resumed = machine.Cpu.TStates;
        machine.ToggleTapeTransport();
        int pulse = cue.State.EarPulseLengthTStates;
        ulong remaining = (ulong)pulse - cue.State.PausedPulseElapsedTStates;
        Assert.Equal(level, restored.ReadEarBit(resumed + remaining - 1));
        Assert.NotEqual(level, restored.ReadEarBit(resumed + remaining));
    }

    [Fact]
    public void EndedDeckAllowsRewindWithoutReattachingLiveTapeUntilSeek()
    {
        var machine = CreateMachine();
        machine.MountTape(new MountedTape("ended.tap", [TapeBlock.CreatePureTone(100, 3)]));
        machine.ExecuteFrame();
        machine.ExecuteFrame();
        TapeDeckView ended = machine.CaptureTapeDeck()!;
        Assert.Equal(TapeTransportState.Ended, ended.State);
        Assert.False(machine.HasMountedTape);
        Assert.True(machine.HasTapeInDeck);
        Assert.Equal(1, Assert.Single(ended.Markers).BlockIndex);
        Assert.True(machine.TrySeekTape(ended.Timeline, 0));
        machine.ExecuteFrame();
        Assert.Equal(TapeTransportState.Stopped, machine.TapeTransportState);
        Assert.Equal(0, machine.CaptureTapeDeck()!.BlockIndex);
        Assert.True(machine.HasMountedTape);
    }

    [Fact]
    public void ConversionCreditsDoNotBecomeNamedTapeSections()
    {
        var machine = CreateMachine();
        machine.MountTape(new MountedTape("sections.tzx",
            [TapeBlock.CreateMetadata("Created with a tape conversion tool"), TapeBlock.CreatePureTone(100, 3),
                TapeBlock.CreateMetadata("Second stage"), TapeBlock.CreateMetadata("Converted by a volunteer"),
                TapeBlock.CreatePureTone(200, 3)]));

        Assert.Equal(new TapeSection(4, "Second stage"), Assert.Single(machine.CaptureTapeDeck()!.Timeline.Sections));
    }

    [Fact]
    public void BlockSeekRejectsMetadataAndPreservesExplicitStopCue()
    {
        var machine = CreateMachine();
        machine.MountTape(new MountedTape("markers.tzx",
            [TapeBlock.CreatePureTone(100, 3), TapeBlock.CreateStopTape(),
                TapeBlock.CreateMetadata("Next level"), TapeBlock.CreatePureTone(250, 4)]));
        machine.ExecuteFrame();
        machine.ExecuteFrame();
        TapeDeckView view = machine.CaptureTapeDeck()!;
        Assert.Equal(new TapeSection(3, "Next level"), Assert.Single(view.Timeline.Sections));
        Assert.False(machine.TrySeekTape(view.Timeline, 2));
        TapeCue cue = Assert.IsType<TapeCue>(Assert.Single(view.Markers).Cue);
        Assert.True(machine.TrySeekTape(view.Timeline, 3));
        Assert.False(machine.IsTapeStoppedAtMarker);
        Assert.Equal(3, machine.CaptureTapeDeck()!.BlockIndex);
        Assert.True(machine.TrySeekTape(view.Timeline, 1, cue));
        Assert.True(machine.IsTapeStoppedAtMarker);
        machine.ToggleTapeTransport();
        Assert.Equal(3, machine.CaptureTapeDeck()!.BlockIndex);
    }
}
