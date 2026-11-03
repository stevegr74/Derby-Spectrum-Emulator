using System;
using System.Collections.Generic;

namespace Derby.Tape
{
    internal static class TapFileParser
    {
        private const ushort StandardPauseAfterBlockMs = 1000;
        private const ushort HeaderPilotPulseCount = 8063;
        private const ushort DataPilotPulseCount = 3223;
        private const ushort PilotPulseLengthTStates = 2168;
        private const ushort SyncFirstPulseLengthTStates = 667;
        private const ushort SyncSecondPulseLengthTStates = 735;
        private const ushort ZeroBitPulseLengthTStates = 855;
        private const ushort OneBitPulseLengthTStates = 1710;

        internal static IReadOnlyList<TapeBlock> Parse(byte[] fileData)
        {
            if (fileData == null)
                throw new ArgumentNullException(nameof(fileData));

            var blocks = new List<TapeBlock>();
            int offset = 0;

            while (offset < fileData.Length)
            {
                if (offset + 2 > fileData.Length)
                    throw new InvalidOperationException("The .tap file ends inside a block length field.");

                int blockLength = ReadWord(fileData, offset);
                offset += 2;

                if (blockLength < 2)
                {
                    throw new InvalidOperationException(
                        $"Invalid tape block length {blockLength}. Each block must contain at least a flag byte and checksum byte.");
                }

                if (offset + blockLength > fileData.Length)
                    throw new InvalidOperationException("The .tap file ends inside a tape block.");

                ValidateChecksum(fileData, offset, blockLength);
                byte[] streamData = new byte[blockLength];
                Buffer.BlockCopy(fileData, offset, streamData, 0, blockLength);
                blocks.Add(TapeBlock.CreateData(
                    streamData,
                    PilotPulseLengthTStates,
                    streamData[0] < 0x80 ? HeaderPilotPulseCount : DataPilotPulseCount,
                    SyncFirstPulseLengthTStates,
                    SyncSecondPulseLengthTStates,
                    ZeroBitPulseLengthTStates,
                    OneBitPulseLengthTStates,
                    usedBitsInLastByte: 8,
                    pauseAfterBlockMs: StandardPauseAfterBlockMs));
                offset += blockLength;
            }

            return blocks;
        }

        private static void ValidateChecksum(byte[] data, int offset, int length)
        {
            byte checksum = 0;
            for (int i = 0; i < length; i++)
                checksum ^= data[offset + i];

            if (checksum != 0)
                throw new InvalidOperationException("Tape block checksum validation failed.");
        }

        private static ushort ReadWord(byte[] data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }
    }
}
