namespace Derby.Tape
{
    internal static class SpectrumRomWorkspace
    {
        internal const ushort BasicProgramStart = 23755;
        internal const ushort DefaultStackPointer = 0xFF58;
        internal const ushort RomSystemVariablesBase = 0x5C3A;
        internal const ushort ErrorNumberSystemVariableAddress = RomSystemVariablesBase;
        internal const ushort NewPpcAddress = 23618;
        internal const ushort NspPcAddress = 23620;
        internal const ushort PpcAddress = 23621;
        internal const ushort SubPpcAddress = 23623;
        internal const ushort ErrorStackPointerAddress = 23613;
        internal const ushort FlagsSystemVariableAddress = 23611;
        internal const ushort TvFlagSystemVariableAddress = 23612;
        internal const ushort BorderSystemVariableAddress = 23624;
        internal const ushort StreamsAddress = 23568;
        internal const ushort VarsAddress = 23627;
        internal const ushort ChansAddress = 23631;
        internal const ushort CurChlAddress = 23633;
        internal const ushort ProgAddress = 23635;
        internal const ushort NextLineAddress = 23637;
        internal const ushort DataAddress = 23639;
        internal const ushort EditLineAddress = 23641;
        internal const ushort KCurAddress = 23643;
        internal const ushort ChAddAddress = 23645;
        internal const ushort XPtrAddress = 23647;
        internal const ushort WorkspaceAddress = 23649;
        internal const ushort StackBottomAddress = 23651;
        internal const ushort StackEndAddress = 23653;
        internal const ushort RamTopAddress = 23730;
        internal const ushort PhysicalRamTopAddress = 23732;
        internal const ushort Spectrum128TapeLoadBankSelectAddress = 23388;
        internal const ushort Spectrum128RamRstAddress = 23389;
        internal const byte Spectrum128RamRstOpcode = 0xCF;
        internal const ushort InitialChannelsAreaAddress = BasicProgramStart - 21;
        internal const ushort KeyboardChannelDescriptorAddress = InitialChannelsAreaAddress;
        internal const ushort ScreenChannelDescriptorAddress = InitialChannelsAreaAddress + 5;
    }
}
