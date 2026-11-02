using Xunit;

namespace Derby.Tests
{
    public sealed class Plus2APagingTests
    {
        [Fact]
        public void NormalPaging_SelectsAllFourRomBanks()
        {
            using var fixture = new Plus2ARomFixture();
            Spectrum128Machine machine = fixture.CreateMachine();

            Assert.Equal((byte)0x10, machine.PeekMemory(0x0000));
            machine.DebugWritePort(0x7FFD, 0x10);
            Assert.Equal((byte)0x11, machine.PeekMemory(0x0000));
            machine.DebugWritePort(0x1FFD, 0x04);
            Assert.Equal((byte)0x13, machine.PeekMemory(0x0000));
            machine.DebugWritePort(0x7FFD, 0x00);
            Assert.Equal((byte)0x12, machine.PeekMemory(0x0000));
        }

        [Fact]
        public void ProductionRomSet_IsRecognisedButKeepsAddressTrapsDisabled()
        {
            string romFolder = Path.Combine(AppContext.BaseDirectory, "ROMs");
            if (!File.Exists(Path.Combine(romFolder, "plus3-3.rom")))
                return;

            var machine = new Spectrum128Machine(SpectrumRomSet.BundledPlus2A(romFolder));

            Assert.Equal("Sinclair ZX Spectrum +2A/+3", machine.RomProfile.Name);
            Assert.False(machine.SupportsRomTapeAcceleration);
        }

        [Theory]
        [InlineData(0x01, 0, 1, 2, 3)]
        [InlineData(0x03, 4, 5, 6, 7)]
        [InlineData(0x05, 4, 5, 6, 3)]
        [InlineData(0x07, 4, 7, 6, 3)]
        public void SpecialPaging_MapsAllDocumentedRamConfigurations(
            byte portValue,
            int slot0,
            int slot1,
            int slot2,
            int slot3)
        {
            using var fixture = new Plus2ARomFixture();
            Spectrum128Machine machine = fixture.CreateMachine();

            machine.DebugWritePort(0x1FFD, portValue);

            Assert.True(machine.SpecialPagingEnabled);
            Assert.Equal([slot0, slot1, slot2, slot3],
                Enumerable.Range(0, 4).Select(machine.GetMappedRamBankForSlot));
            for (int slot = 0; slot < 4; slot++)
            {
                ushort address = (ushort)(slot * 0x4000);
                machine.PokeMemory(address, (byte)(0x80 + slot));
                Assert.Equal((byte)(0x80 + slot), machine.PeekRamBank(
                    machine.GetMappedRamBankForSlot(slot),
                    0));
            }
        }

        [Fact]
        public void RevisedPartialPortDecoding_AcceptsAliasesAndRejectsStandard128Aliases()
        {
            using var fixture = new Plus2ARomFixture();
            Spectrum128Machine plus2A = fixture.CreateMachine();

            plus2A.DebugWritePort(0x5FFD, 0x17);
            Assert.Equal(7, plus2A.PagedRamBank);
            Assert.Equal(1, plus2A.CurrentRomBank);
            plus2A.DebugWritePort(0x1FFD, 0x04);
            Assert.Equal(3, plus2A.CurrentRomBank);

            Spectrum128Machine standard128 = fixture.CreateStandard128Machine();
            standard128.DebugWritePort(0x1FFD, 0x05);
            Assert.False(standard128.SpecialPagingEnabled);
            Assert.Equal(0, standard128.CurrentRomBank);
        }

        [Fact]
        public void PagingLock_BlocksBothPagingRegisters()
        {
            using var fixture = new Plus2ARomFixture();
            Spectrum128Machine machine = fixture.CreateMachine();

            machine.DebugWritePort(0x7FFD, 0x23);
            machine.DebugWritePort(0x1FFD, 0x07);
            machine.DebugWritePort(0x7FFD, 0x10);

            Assert.True(machine.PagingLocked);
            Assert.False(machine.SpecialPagingEnabled);
            Assert.Equal(3, machine.PagedRamBank);
            Assert.Equal(0, machine.CurrentRomBank);
        }

        [Fact]
        public void QuickState_RestoresSpecialPagingMapAndRam()
        {
            using var fixture = new Plus2ARomFixture();
            Spectrum128Machine machine = fixture.CreateMachine();
            machine.DebugWritePort(0x1FFD, 0x07);
            machine.PokeMemory(0x4000, 0xA7);
            Spectrum128Machine.QuickState state = machine.CaptureQuickState();

            machine.Reset();
            machine.RestoreQuickState(state);

            Assert.True(machine.SpecialPagingEnabled);
            Assert.Equal([4, 7, 6, 3],
                Enumerable.Range(0, 4).Select(machine.GetMappedRamBankForSlot));
            Assert.Equal((byte)0xA7, machine.PeekRamBank(7, 0));
        }

        private sealed class Plus2ARomFixture : IDisposable
        {
            public Plus2ARomFixture()
            {
                Folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Folder);
                for (int bank = 0; bank < 4; bank++)
                {
                    File.WriteAllBytes(
                        Path.Combine(Folder, $"plus3-{bank}.rom"),
                        Enumerable.Repeat((byte)(0x10 + bank), 0x4000).ToArray());
                }
                File.WriteAllBytes(Path.Combine(Folder, "128-0.rom"), new byte[0x4000]);
                File.WriteAllBytes(Path.Combine(Folder, "128-1.rom"), new byte[0x4000]);
            }

            public string Folder { get; }

            public Spectrum128Machine CreateMachine() =>
                new(SpectrumRomSet.BundledPlus2A(Folder));

            public Spectrum128Machine CreateStandard128Machine() =>
                new(SpectrumRomSet.Bundled128K(Folder));

            public void Dispose() => Directory.Delete(Folder, recursive: true);
        }
    }
}
