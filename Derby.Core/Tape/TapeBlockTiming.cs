namespace Derby.Tape
{
    internal static class TapeBlockTiming
    {
        private const ulong TStatesPerMillisecond48K = SpectrumMachine.CpuClockHz48 / 1000;

        internal static ulong EstimateDuration(TapeBlock block)
        {
            ArgumentNullException.ThrowIfNull(block);

            return block.Kind switch
            {
                TapeBlockKind.Data => EstimateDataDuration(block, includeTrailingPause: true),
                TapeBlockKind.PureTone => (ulong)block.PureTonePulseLength * block.PureTonePulseCount,
                TapeBlockKind.PulseSequence => EstimatePulseSequenceDuration(block.PulseSequence),
                TapeBlockKind.DirectRecording => EstimateDirectRecordingDuration(block),
                TapeBlockKind.Pause => (ulong)block.PauseAfterBlockMs * TStatesPerMillisecond48K,
                _ => 0UL
            };
        }

        internal static ulong EstimateDurationBeforeTrailingPause(TapeBlock block)
        {
            ArgumentNullException.ThrowIfNull(block);

            return block.Kind switch
            {
                TapeBlockKind.Data => EstimateDataDuration(block, includeTrailingPause: false),
                TapeBlockKind.Pause => 0UL,
                _ => EstimateDuration(block)
            };
        }

        private static ulong EstimateDataDuration(TapeBlock block, bool includeTrailingPause)
        {
            ulong total = (ulong)block.PilotPulseLength * block.PilotPulseCount;
            total += block.SyncFirstPulseLength;
            total += block.SyncSecondPulseLength;

            for (int i = 0; i < block.StreamByteCount; i++)
            {
                byte value = block.GetStreamByte(i);
                int bitCount = block.GetStreamByteBitCount(i);
                for (int bit = 0; bit < bitCount; bit++)
                {
                    ushort pulseLength = (value & 0x80) != 0
                        ? block.OneBitPulseLength
                        : block.ZeroBitPulseLength;
                    total += (ulong)pulseLength * 2UL;
                    value <<= 1;
                }
            }

            if (includeTrailingPause)
                total += (ulong)block.PauseAfterBlockMs * TStatesPerMillisecond48K;

            return total;
        }

        private static ulong EstimatePulseSequenceDuration(int[]? pulseSequence)
        {
            if (pulseSequence == null)
                return 0;

            ulong total = 0;
            foreach (int pulseLength in pulseSequence)
                total += (ulong)pulseLength;

            return total;
        }

        private static ulong EstimateDirectRecordingDuration(TapeBlock block)
        {
            if (block.DirectRecordingSamples == null)
                return 0;

            ulong totalBits = 0;
            for (int i = 0; i < block.DirectRecordingSamples.Length; i++)
                totalBits += i == block.DirectRecordingSamples.Length - 1 ? block.UsedBitsInLastByte : 8UL;

            return (totalBits * block.DirectRecordingSampleTStates)
                + ((ulong)block.PauseAfterBlockMs * TStatesPerMillisecond48K);
        }
    }
}
