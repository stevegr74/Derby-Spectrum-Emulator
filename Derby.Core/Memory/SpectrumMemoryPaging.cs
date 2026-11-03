namespace Derby
{
    internal sealed class SpectrumMemoryPaging
    {
        internal int PagedRamBank { get; set; }
        internal int CurrentRomBank { get; set; }
        internal bool PagingLocked { get; set; }
        internal int ScreenBank { get; set; } = 5;
        internal bool Uses48KMemoryMap { get; set; }
        internal byte Last7ffdValue { get; set; } = 0xFF;
        internal byte Last1ffdValue { get; set; }

        internal bool IsSpecialPagingEnabled(SpectrumMemoryPagingModel pagingModel)
        {
            return pagingModel == SpectrumMemoryPagingModel.Plus2APlus3 &&
                (Last1ffdValue & 0x01) != 0;
        }

        internal int GetMappedRamBankForSlot(int slot, SpectrumMemoryPagingModel pagingModel)
        {
            if ((uint)slot >= 4)
                throw new ArgumentOutOfRangeException(nameof(slot));

            if (!IsSpecialPagingEnabled(pagingModel))
            {
                return slot switch
                {
                    1 => 5,
                    2 => 2,
                    3 => PagedRamBank,
                    _ => throw new InvalidOperationException("Slot 0 currently contains ROM.")
                };
            }

            int configuration = (Last1ffdValue >> 1) & 0x03;
            return configuration switch
            {
                0 => slot,
                1 => slot + 4,
                2 => slot == 3 ? 3 : slot + 4,
                3 => slot switch { 0 => 4, 1 => 7, 2 => 6, _ => 3 },
                _ => throw new InvalidOperationException()
            };
        }

        internal bool IsMappedSlotRom(int slot, SpectrumMemoryPagingModel pagingModel)
        {
            if ((uint)slot >= 4)
                throw new ArgumentOutOfRangeException(nameof(slot));

            return slot == 0 && !IsSpecialPagingEnabled(pagingModel);
        }

        internal void UpdateCurrentRomBank(SpectrumMemoryPagingModel pagingModel)
        {
            int lowBit = (Last7ffdValue >> 4) & 0x01;
            int highBit = pagingModel == SpectrumMemoryPagingModel.Plus2APlus3
                ? (Last1ffdValue >> 1) & 0x02
                : 0;
            CurrentRomBank = highBit | lowBit;
        }
    }
}
