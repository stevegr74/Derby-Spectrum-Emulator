using System;
using System.Collections.Generic;
namespace Derby.Tape
{
    public static partial class TapLoader
    {
        internal static ulong EstimateTapeBlockDurationTStates(TapeBlock block)
        {
            if (block == null)
                throw new ArgumentNullException(nameof(block));

            return block.Kind switch
            {
                TapeBlockKind.Data => EstimateDataBlockDurationTStates(block),
                TapeBlockKind.PureTone => (ulong)block.PureTonePulseLength * block.PureTonePulseCount,
                TapeBlockKind.PulseSequence => EstimatePulseSequenceDurationTStates(block.PulseSequence),
                TapeBlockKind.DirectRecording => EstimateDirectRecordingDurationTStates(block),
                TapeBlockKind.Pause => (ulong)block.PauseAfterBlockMs * TStatesPerMillisecond48k,
                _ => 0UL
            };
        }

        internal static ulong EstimateTapeBlockDurationBeforeTrailingPauseTStates(TapeBlock block)
        {
            if (block == null)
                throw new ArgumentNullException(nameof(block));

            return block.Kind switch
            {
                TapeBlockKind.Data => EstimateDataBlockDurationTStates(block, includeTrailingPause: false),
                TapeBlockKind.Pause => 0UL,
                _ => EstimateTapeBlockDurationTStates(block)
            };
        }

        private static ulong EstimateDataBlockDurationTStates(TapeBlock block)
            => EstimateDataBlockDurationTStates(block, includeTrailingPause: true);

        private static ulong EstimateDataBlockDurationTStates(TapeBlock block, bool includeTrailingPause)
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
                    bool oneBit = (value & 0x80) != 0;
                    ushort pulseLength = oneBit ? block.OneBitPulseLength : block.ZeroBitPulseLength;
                    total += (ulong)pulseLength * 2UL;
                    value <<= 1;
                }
            }

            if (includeTrailingPause)
                total += (ulong)block.PauseAfterBlockMs * TStatesPerMillisecond48k;
            return total;
        }

        private static ulong EstimatePulseSequenceDurationTStates(int[]? pulseSequence)
        {
            if (pulseSequence == null)
                return 0;

            ulong total = 0;
            for (int i = 0; i < pulseSequence.Length; i++)
                total += (ulong)pulseSequence[i];

            return total;
        }

        private static ulong EstimateDirectRecordingDurationTStates(TapeBlock block)
        {
            if (block.DirectRecordingSamples == null)
                return 0;

            ulong totalBits = 0;
            for (int i = 0; i < block.DirectRecordingSamples.Length; i++)
                totalBits += i == block.DirectRecordingSamples.Length - 1 ? block.UsedBitsInLastByte : 8UL;

            return (totalBits * block.DirectRecordingSampleTStates)
                + ((ulong)block.PauseAfterBlockMs * TStatesPerMillisecond48k);
        }

        internal static IReadOnlyList<TapeBlock> ParseBlocks(byte[] fileData)
            => TapFileParser.Parse(fileData);

    }
}
