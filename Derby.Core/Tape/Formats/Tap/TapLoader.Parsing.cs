using System.Collections.Generic;
namespace Derby.Tape
{
    public static partial class TapLoader
    {
        internal static IReadOnlyList<TapeBlock> ParseBlocks(byte[] fileData)
            => TapFileParser.Parse(fileData);
    }
}
