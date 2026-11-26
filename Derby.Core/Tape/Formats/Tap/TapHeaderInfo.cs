using System.Text.Json.Serialization;

namespace Derby.Tape
{
    internal sealed class TapHeaderInfo
    {
        [JsonConstructor]
        internal TapHeaderInfo(
            byte type,
            string fileName,
            ushort dataLength,
            ushort autoStartLine,
            ushort programLength)
        {
            Type = type;
            FileName = string.IsNullOrWhiteSpace(fileName) ? "unnamed" : fileName;
            DataLength = dataLength;
            AutoStartLine = autoStartLine;
            ProgramLength = programLength;
            StartAddress = autoStartLine;
        }

        [JsonInclude] internal byte Type { get; }
        [JsonInclude] internal string FileName { get; }
        [JsonInclude] internal ushort DataLength { get; }
        [JsonInclude] internal ushort AutoStartLine { get; }
        [JsonInclude] internal ushort ProgramLength { get; }
        internal ushort StartAddress { get; }
    }
}
