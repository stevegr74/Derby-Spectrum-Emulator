using System.Numerics;

namespace Derby.Tape;

public sealed partial class MountedTape
{
    internal double? ScrubPosition { get; private set; }
    private int? scrubbedSignalBlock;

    internal bool Scrub(double position)
    {
        if (!double.IsFinite(position) || position < 0 || position > blocks.Count || blocks.Count == 0)
            return false;

        int index = Math.Min((int)position, blocks.Count - 1);
        double fraction = position - index;
        while (index < blocks.Count && !IsSeekableBlock(index))
        {
            index++;
            fraction = 0;
        }
        if (index == blocks.Count)
        {
            do { index--; } while (index >= 0 && !IsSeekableBlock(index));
            if (index < 0)
                return false;
            fraction = 1;
        }

        ResetAt(index, 0, FindBoundaryEarLevel(index));
        TapeBlock block = blocks[index];
        ulong length = GetScrubBlockDuration(block, index);
        ulong remaining = length == 0 ? 0 : Math.Min(length - 1, (ulong)(length * fraction));
        while (earPlaybackBlockIndex == index && earPlaybackState != EarPlaybackState.Idle &&
            remaining >= (ulong)earPulseLengthTStates)
        {
            if (SkipScrubPulses(block, ref remaining))
                continue;
            remaining -= (ulong)earPulseLengthTStates;
            AdvanceEarPulse();
        }

        earPlaybackStarted = true;
        pausedPulseElapsedTStates = remaining;
        playbackPaused = true;
        ScrubPosition = index + fraction;
        scrubbedSignalBlock = fraction > 0 ? index : null;
        return true;
    }

    internal short[] RenderPreview()
    {
        // Playback mutates cursor fields, not the shared tape blocks.
        var cursor = (MountedTape)MemberwiseClone();
        const int sampleRate = 44100;
        const int sampleCount = sampleRate * 4 / 5;
        var samples = new short[sampleCount];
        ulong start = (ulong)playbackClockHz;
        cursor.ResumePlayback(start);
        cursor.ReadEarBit(start);
        double previous = cursor.earLevel ? 1 : -1;
        double filtered = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            if (cursor.playbackPaused || cursor.earPlaybackState == EarPlaybackState.Idle)
                break;
            double level = cursor.ReadEarBit(start + (ulong)i * (ulong)playbackClockHz / sampleRate) ? 1 : -1;
            filtered = level - previous + 0.97 * filtered;
            previous = level;
            int edge = Math.Min(i, samples.Length - 1 - i);
            samples[i] = (short)(filtered * 5500 * Math.Min(1, edge / 220.0));
        }
        return samples;
    }

    private ulong GetScrubBlockDuration(TapeBlock block, int index)
    {
        ulong length = block.PauseAfterBlockMs == 0 ? 0 : (ulong)GetPauseLengthTStates(block, index + 1);
        switch (block.Kind)
        {
            case TapeBlockKind.Data:
                length += (ulong)ScaleBlockTiming(block.PilotPulseLength, block) * block.PilotPulseCount;
                if (block.SyncFirstPulseLength != 0)
                    length += (ulong)ScaleBlockTiming(block.SyncFirstPulseLength, block)
                        + (ulong)ScaleBlockTiming(block.SyncSecondPulseLength, block);
                ulong zero = (ulong)ScaleBlockTiming(block.ZeroBitPulseLength, block) * 2;
                ulong one = (ulong)ScaleBlockTiming(block.OneBitPulseLength, block) * 2;
                for (int i = 0; i < block.StreamByteCount; i++)
                    length += ScrubByteDuration(block, i, zero, one);
                break;
            case TapeBlockKind.PureTone:
                length += (ulong)ScaleBlockTiming(block.PureTonePulseLength, block) * block.PureTonePulseCount;
                break;
            case TapeBlockKind.PulseSequence:
                foreach (int pulse in block.PulseSequence!)
                    length += (ulong)ScaleBlockTiming(pulse, block);
                break;
            case TapeBlockKind.DirectRecording:
                ulong bits = (ulong)(block.DirectRecordingSamples!.Length - 1) * 8 + block.UsedBitsInLastByte;
                length += bits * (ulong)ScaleBlockTiming(block.DirectRecordingSampleTStates, block);
                break;
        }
        return length;
    }

    private static ulong ScrubByteDuration(TapeBlock block, int index, ulong zero, ulong one)
    {
        int bits = block.GetStreamByteBitCount(index);
        int ones = BitOperations.PopCount((uint)block.GetStreamByte(index) >> (8 - bits));
        return (ulong)(bits - ones) * zero + (ulong)ones * one;
    }

    private bool SkipScrubPulses(TapeBlock block, ref ulong remaining)
    {
        // Skip complete bytes where possible, but let the normal renderer handle block boundaries.
        if (earPlaybackState == EarPlaybackState.Data && earBitIndex == 0 && earPulseRepeatCount == 0 &&
            earStreamByteIndex < block.StreamByteCount - 1)
        {
            ulong zero = (ulong)ScaleBlockTiming(block.ZeroBitPulseLength, block) * 2;
            ulong one = (ulong)ScaleBlockTiming(block.OneBitPulseLength, block) * 2;
            int first = earStreamByteIndex;
            while (earStreamByteIndex < block.StreamByteCount - 1)
            {
                ulong duration = ScrubByteDuration(block, earStreamByteIndex, zero, one);
                if (remaining < duration)
                    break;
                remaining -= duration;
                earStreamByteIndex++;
            }
            if (earStreamByteIndex != first)
            {
                SyncRomByteStreamTrapToEarProgress();
                earPulseLengthTStates = GetCurrentBitPulseLengthTStates();
                return true;
            }
        }
        else if (earPlaybackState == EarPlaybackState.DirectRecording)
        {
            ulong current = (ulong)earStreamByteIndex * 8 + (uint)earBitIndex;
            ulong last = (ulong)(block.DirectRecordingSamples!.Length - 1) * 8 + block.UsedBitsInLastByte - 1;
            ulong length = (ulong)earPulseLengthTStates;
            ulong skip = Math.Min(last - current, remaining / length);
            if (skip > 0)
            {
                current += skip;
                remaining -= skip * length;
                earStreamByteIndex = (int)(current / 8);
                earBitIndex = (int)(current % 8);
                earLevel = GetCurrentDirectRecordingLevel(block);
                return true;
            }
        }
        return false;
    }
}
