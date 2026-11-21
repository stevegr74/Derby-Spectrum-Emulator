using System.Security.Cryptography;

namespace Derby.Tape;

public sealed record TapeSection(int BlockIndex, string Name)
{
    public override string ToString() => Name;
}

public sealed record TapeTimeline(string Identity, string FileName, int BlockCount, IReadOnlyList<TapeSection> Sections);

internal sealed record TapeCueProfile(bool SkipCustomHeader, int NonRomDivisor, int RomDivisor,
    bool InitialEarHigh, bool AllowRomTraps, int ClockHz);

// A tape cursor, not a machine snapshot. Only the core can consume its exact state.
public sealed class TapeCue
{
    internal TapeCue(string sequence, string? media, int blockIndex, TapeCueProfile profile, MountedTape.QuickState state)
        => (Sequence, Media, BlockIndex, Profile, State) = (sequence, media, blockIndex, profile, state);

    internal string Sequence { get; }
    internal string? Media { get; }
    internal TapeCueProfile Profile { get; }
    internal MountedTape.QuickState State { get; }
    public int BlockIndex { get; }
}

public sealed record TapeTimelineMarker(int BlockIndex, string Label, TapeCue? Cue);

public sealed record TapeDeckView(TapeTimeline Timeline, TapeTransportState State, int BlockIndex,
    bool CanSeek, IReadOnlyList<TapeTimelineMarker> Markers);

public sealed partial class MountedTape
{
    private TapeTimeline? timeline;
    private string? mediaIdentity;
    private string? sequenceIdentity;

    internal TapeTimeline Timeline => timeline ??= CreateTimeline();
    internal int TimelinePosition => playbackCompleted ? blocks.Count : Math.Clamp(earPlaybackBlockIndex, 0, blocks.Count);

    internal void SetMediaIdentity(string identity)
    {
        mediaIdentity = identity;
        if (timeline != null)
            timeline = timeline with { Identity = identity };
    }

    private TapeCueProfile CueProfile => new(skipCustomHeaderForEarPlayback, nonRomTimingDivisor,
        loadableTimingDivisor, initialEarLevelHigh, allowRomLoadTraps, playbackClockHz);

    private string SequenceIdentity
    {
        get { _ = Timeline; return sequenceIdentity!; }
    }

    internal TapeCue? CaptureCue() => playbackPaused && !playbackCompleted
        ? new(SequenceIdentity, mediaIdentity, TimelinePosition, CueProfile, CaptureQuickState())
        : null;

    internal MountedTape? RestoreCue(TapeCue cue)
    {
        TapeCueProfile profile = cue.Profile;
        if (cue.Sequence != SequenceIdentity || profile.ClockHz != playbackClockHz ||
            (cue.Media != null && cue.Media != mediaIdentity))
            return null;
        MountedTape tape = this;
        if (profile != CueProfile)
        {
            tape = new(DisplayName, blocks, skipCustomHeaderForEarPlayback: profile.SkipCustomHeader,
                nonRomTimingDivisor: profile.NonRomDivisor, loadableTimingDivisor: profile.RomDivisor,
                initialEarLevelHigh: profile.InitialEarHigh, allowRomLoadTraps: profile.AllowRomTraps);
            tape.ConfigurePlaybackClock(playbackClockHz);
            tape.mediaIdentity = mediaIdentity;
        }
        tape.RestoreQuickState(cue.State);
        return tape;
    }

    internal bool SeekToBlock(int blockIndex)
    {
        if (blockIndex < 0 || blockIndex >= blocks.Count ||
            (blockIndex != 0 && !IsSeekableBlock(blockIndex)))
            return false;
        ResetAt(blockIndex, 0);
        playbackPaused = true;
        return true;
    }

    private bool IsSeekableBlock(int index) =>
        blocks[index].Kind is not (TapeBlockKind.Metadata or TapeBlockKind.SetSignalLevel or TapeBlockKind.StopTape)
        && GetEarPlaybackStartBlockIndex(index) == index;

    private TapeTimeline CreateTimeline()
    {
        using var sha = SHA256.Create();
        using var hashed = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write);
        using var writer = new BinaryWriter(hashed);
        writer.Write(blocks.Count);
        var sections = new List<TapeSection>();
        string? sectionLabel = null;
        for (int i = 0; i < blocks.Count; i++)
        {
            TapeBlock block = blocks[i];
            writer.Write((int)block.Kind);
            writer.Write(block.TimelineLabel ?? "");
            writer.Write(block.IsLoadableRomBlock);
            writer.Write(block.CanUseRomLoadTrap);
            writer.Write(block.RequiresExactPulseTiming);
            writer.Write(block.PilotPulseLength);
            writer.Write(block.PilotPulseCount);
            writer.Write(block.SyncFirstPulseLength);
            writer.Write(block.SyncSecondPulseLength);
            writer.Write(block.ZeroBitPulseLength);
            writer.Write(block.OneBitPulseLength);
            writer.Write(block.UsedBitsInLastByte);
            writer.Write(block.PauseAfterBlockMs);
            writer.Write(block.PureTonePulseLength);
            writer.Write(block.PureTonePulseCount);
            writer.Write(block.DirectRecordingSampleTStates);
            writer.Write(block.SignalLevel is null ? 0 : block.SignalLevel.Value ? 1 : 2);
            WriteBytes(block.StreamData);
            WriteBytes(block.DirectRecordingSamples);
            writer.Write(block.PulseSequence?.Length ?? -1);
            if (block.PulseSequence is { } pulses)
                foreach (int pulse in pulses)
                    writer.Write(pulse);

            if (block.Kind == TapeBlockKind.StopTape)
                sectionLabel = null;
            if (block.TimelineLabel is { } annotation && IsSectionAnnotation(annotation))
                sectionLabel = annotation;
            if (block.Kind is not (TapeBlockKind.Data or TapeBlockKind.PureTone or TapeBlockKind.PulseSequence or TapeBlockKind.DirectRecording)
                || !IsSeekableBlock(i))
                continue;

            string? label = sectionLabel;
            sectionLabel = null;
            if (label == null && IsHeaderBlock(block))
            {
                TapHeaderInfo header = TapLoader.ParseHeaderInfo(block);
                if (header.Type <= 3 && header.FileName != "unnamed")
                    label = header.FileName;
            }

            if (!string.IsNullOrWhiteSpace(label))
                sections.Add(new(i, label.Trim()));
        }

        writer.Flush();
        hashed.FlushFinalBlock();
        sequenceIdentity = Convert.ToHexString(sha.Hash!);
        return new(mediaIdentity ?? sequenceIdentity, DisplayName, blocks.Count, sections.AsReadOnly());

        void WriteBytes(byte[]? bytes)
        {
            writer.Write(bytes?.Length ?? -1);
            if (bytes != null)
                writer.Write(bytes);
        }
    }

    private static readonly string[] TapeCreditPrefixes =
    [
        "created with ", "created by ", "converted with ", "converted by ", "made with ",
        "generated with ", "generated by ", "recorded with ", "copyright ", "http://", "https://"
    ];

    private static bool IsSectionAnnotation(string text) =>
        !TapeCreditPrefixes.Any(prefix => text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
