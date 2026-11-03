namespace Derby.Tape
{
    internal sealed class TapHeaderInfo
    {
        internal TapHeaderInfo(
            byte type,
            string fileName,
            ushort dataLength,
            ushort parameter1,
            ushort parameter2)
        {
            Type = type;
            FileName = string.IsNullOrWhiteSpace(fileName) ? "unnamed" : fileName;
            DataLength = dataLength;
            AutoStartLine = parameter1;
            ProgramLength = parameter2;
            StartAddress = parameter1;
        }

        internal byte Type { get; }
        internal string FileName { get; }
        internal ushort DataLength { get; }
        internal ushort AutoStartLine { get; }
        internal ushort ProgramLength { get; }
        internal ushort StartAddress { get; }
    }
}
