using System.Globalization;

namespace Derby
{
    public enum DisassemblyAddressSpaceKind
    {
        Mapped,
        Rom,
        Ram
    }

    public readonly record struct DisassemblyAddressSpace(DisassemblyAddressSpaceKind Kind, int Bank = 0)
    {
        public static DisassemblyAddressSpace Mapped => new(DisassemblyAddressSpaceKind.Mapped);
        public static DisassemblyAddressSpace Rom(int bank) => new(DisassemblyAddressSpaceKind.Rom, bank);
        public static DisassemblyAddressSpace Ram(int bank) => new(DisassemblyAddressSpaceKind.Ram, bank);

        public int Length => Kind == DisassemblyAddressSpaceKind.Mapped ? 65536 : 16384;

        public string DisplayName => Kind switch
        {
            DisassemblyAddressSpaceKind.Rom => $"ROM {Bank}",
            DisassemblyAddressSpaceKind.Ram => $"RAM {Bank}",
            _ => "Mapped 64K"
        };

        public string FormatAddress(ushort address) => Kind == DisassemblyAddressSpaceKind.Mapped
            ? $"{address:X4}H"
            : $"{DisplayName}:{address:X4}H";

        public override string ToString() => DisplayName;
    }

    public sealed record DisassemblyCpuState(
        ushort AF,
        ushort BC,
        ushort DE,
        ushort HL,
        ushort AlternateAF,
        ushort AlternateBC,
        ushort AlternateDE,
        ushort AlternateHL,
        ushort IX,
        ushort IY,
        ushort SP,
        ushort PC,
        byte I,
        byte R,
        bool IFF1,
        bool IFF2,
        int InterruptMode,
        bool IsHalted,
        bool InterruptPending,
        ulong TStates,
        IReadOnlyList<ushort> StackWords,
        ushort Memptr = 0);

    public sealed class DisassemblySnapshot
    {
        private readonly byte[] memory;
        private readonly byte[][] romBanks;
        private readonly byte[][] ramBanks;

        public DisassemblySnapshot(
            byte[] memory,
            ushort programCounter,
            SpectrumMachineModel machineModel,
            int currentRomBank,
            int pagedRamBank,
            int screenBank,
            DateTime? capturedAtUtc = null,
            DisassemblyCpuState? cpuState = null,
            IReadOnlyList<byte[]>? romBanks = null,
            IReadOnlyList<byte[]>? ramBanks = null,
            string? analysisContext = null,
            SpectrumMemoryPagingModel memoryPagingModel = SpectrumMemoryPagingModel.Standard128K,
            IReadOnlyList<int>? mappedRamBanks = null)
        {
            ArgumentNullException.ThrowIfNull(memory);
            if (memory.Length != 65536)
                throw new ArgumentException("A disassembly snapshot must contain the full logical 64K address space.", nameof(memory));

            this.memory = (byte[])memory.Clone();
            if (romBanks != null && romBanks.Count is not (2 or 4))
                throw new ArgumentException("Expected a complete two-bank or four-bank ROM set.", nameof(romBanks));
            this.romBanks = CloneBanks(romBanks, romBanks?.Count ?? 0, nameof(romBanks));
            this.ramBanks = CloneBanks(ramBanks, 8, nameof(ramBanks));
            ProgramCounter = programCounter;
            MachineModel = machineModel;
            CurrentRomBank = currentRomBank;
            PagedRamBank = pagedRamBank;
            ScreenBank = screenBank;
            MemoryPagingModel = memoryPagingModel;
            MappedRamBanks = mappedRamBanks?.ToArray() ?? [-1, 5, 2, pagedRamBank];
            if (MappedRamBanks.Count != 4)
                throw new ArgumentException("A mapped capture must describe four 16K slots.", nameof(mappedRamBanks));
            AnalysisContext = string.IsNullOrWhiteSpace(analysisContext) ? "default" : analysisContext.Trim();
            CapturedAtUtc = capturedAtUtc ?? DateTime.UtcNow;
            CpuState = cpuState is null
                ? null
                : cpuState with { StackWords = Array.AsReadOnly(cpuState.StackWords.ToArray()) };
        }

        public ushort ProgramCounter { get; }
        public SpectrumMachineModel MachineModel { get; }
        public int CurrentRomBank { get; }
        public int PagedRamBank { get; }
        public int ScreenBank { get; }
        public SpectrumMemoryPagingModel MemoryPagingModel { get; }
        public IReadOnlyList<int> MappedRamBanks { get; }
        public string AnalysisContext { get; }
        public DateTime CapturedAtUtc { get; }
        public DisassemblyCpuState? CpuState { get; }

        public IReadOnlyList<DisassemblyAddressSpace> AvailableAddressSpaces
        {
            get
            {
                var spaces = new List<DisassemblyAddressSpace> { DisassemblyAddressSpace.Mapped };
                for (int bank = 0; bank < romBanks.Length; bank++)
                    spaces.Add(DisassemblyAddressSpace.Rom(bank));
                for (int bank = 0; bank < ramBanks.Length; bank++)
                    spaces.Add(DisassemblyAddressSpace.Ram(bank));
                return spaces;
            }
        }

        public byte ReadMemory(ushort address) => memory[address];

        public byte ReadMemory(DisassemblyAddressSpace addressSpace, ushort address)
        {
            if (addressSpace.Kind == DisassemblyAddressSpaceKind.Mapped)
                return ReadMemory(address);

            byte[][] banks = addressSpace.Kind switch
            {
                DisassemblyAddressSpaceKind.Rom when (uint)addressSpace.Bank < romBanks.Length => romBanks,
                DisassemblyAddressSpaceKind.Ram when (uint)addressSpace.Bank < ramBanks.Length => ramBanks,
                _ => throw new ArgumentOutOfRangeException(nameof(addressSpace))
            };
            return address < 0x4000 ? banks[addressSpace.Bank][address] : (byte)0xFF;
        }

        public string MappingText
        {
            get
            {
                string model = MemoryPagingModel == SpectrumMemoryPagingModel.Plus2APlus3
                    ? "+2A"
                    : MachineModel == SpectrumMachineModel.Spectrum48K ? "48K" : "128K";
                string[] slots = new string[4];
                for (int slot = 0; slot < slots.Length; slot++)
                {
                    int start = slot * 0x4000;
                    int end = start + 0x3FFF;
                    slots[slot] = MappedRamBanks[slot] < 0
                        ? $"{start:X4}-{end:X4}: ROM {CurrentRomBank}"
                        : $"{start:X4}-{end:X4}: RAM {MappedRamBanks[slot]}";
                }
                return $"{model} | {string.Join(" | ", slots)} | Screen: RAM {ScreenBank}";
            }
        }

        private static byte[][] CloneBanks(IReadOnlyList<byte[]>? source, int expectedCount, string parameterName)
        {
            if (source == null)
                return Array.Empty<byte[]>();
            if (source.Count != expectedCount)
                throw new ArgumentException($"Expected {expectedCount} banks.", parameterName);

            var clone = new byte[expectedCount][];
            for (int bank = 0; bank < expectedCount; bank++)
            {
                if (source[bank] == null || source[bank].Length != 0x4000)
                    throw new ArgumentException("Every physical bank must contain exactly 16K.", parameterName);
                clone[bank] = (byte[])source[bank].Clone();
            }
            return clone;
        }
    }

    public static class DisassemblyAddressParser
    {
        public static bool TryParse(string? text, out ushort address)
        {
            address = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string value = text.Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                value = value[2..];
            else if (value.StartsWith('$'))
                value = value[1..];
            else if (value.EndsWith('H') || value.EndsWith('h'))
                value = value[..^1];

            return value.Length is > 0 and <= 4 &&
                ushort.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out address);
        }
    }
}
