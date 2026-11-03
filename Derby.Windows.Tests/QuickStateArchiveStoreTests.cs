using System.Drawing;
using Derby.Pokes;
using Xunit;

namespace Derby.Tests
{
    public sealed class QuickStateArchiveStoreTests
    {
        [Fact]
        public void Archive_RoundTripsEightSlotMetadataThumbnailAndPersistentState()
        {
            string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"derby-quick-state-{Guid.NewGuid():N}");
            string path = System.IO.Path.Combine(directory, "quick-states.dat");
            try
            {
                SpectrumRomSet romSet = SpectrumRomSet.Bundled128K(System.IO.Path.Combine(AppContext.BaseDirectory, "ROMs"));
                var machine = new SpectrumMachine(romSet);
                machine.Cpu.Regs.PC = 0x8123;
                machine.PokeRamBank(7, 0x1234, 0xA5);
                machine.ForceApply7ffdValue(0x0F);
                machine.Cpu.WritePort(0xFFFD, 6);
                machine.Cpu.WritePort(0xBFFD, 0x3C);
                machine.Cpu.WritePort(0x00FE, 0x05);
                var thumbnail = new Bitmap(160, 120);
                using (Graphics graphics = Graphics.FromImage(thumbnail))
                    graphics.Clear(Color.CadetBlue);
                var slots = new QuickStateSlot?[8];
                slots[6] = new QuickStateSlot(
                    machine.CaptureQuickState(),
                    machine.CapturePersistentState(),
                    romSet,
                    "Archive Test.tzx",
                    new PokeMediaIdentity("Archive Test.tzx", new string('A', 64)),
                    thumbnail);
                var store = new QuickStateArchiveStore(path);

                store.Save(slots);
                QuickStateSlot?[] loaded = store.Load(
                    8,
                    name => name == romSet.Name ? romSet : null);

                Assert.True(File.Exists(path));
                Assert.Equal(8, loaded.Length);
                Assert.Null(loaded[0]);
                QuickStateSlot restoredSlot = Assert.IsType<QuickStateSlot>(loaded[6]);
                Assert.Null(restoredSlot.State);
                Assert.Equal("Archive Test.tzx", restoredSlot.MediaDisplayName);
                Assert.Equal(romSet.Name, restoredSlot.RomSet.Name);
                Assert.Equal(new Size(160, 120), restoredSlot.Thumbnail.Size);
                var restoredMachine = new SpectrumMachine(romSet);
                restoredMachine.RestorePersistentState(restoredSlot.PersistentState);
                Assert.Equal((ushort)0x8123, restoredMachine.Cpu.Regs.PC);
                Assert.Equal(0xA5, restoredMachine.PeekRamBank(7, 0x1234));
                Assert.Equal(7, restoredMachine.PagedRamBank);
                Assert.Equal(7, restoredMachine.ScreenBank);
                Assert.Equal(5, restoredMachine.BorderColor);
                restoredMachine.Cpu.WritePort(0xFFFD, 6);
                Assert.Equal(0x3C, restoredMachine.DebugReadPort(0xFFFD));

                restoredSlot.Dispose();
                loaded[6] = null;
                store.Save(loaded);
                QuickStateSlot?[] afterDelete = store.Load(8, name => name == romSet.Name ? romSet : null);
                Assert.All(afterDelete, Assert.Null);

                foreach (QuickStateSlot? slot in slots)
                    slot?.Dispose();
                foreach (QuickStateSlot? slot in loaded)
                    slot?.Dispose();
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void Archive_CorruptFileReturnsEmptySlots()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"derby-quick-state-{Guid.NewGuid():N}.dat");
            try
            {
                File.WriteAllText(path, "not an archive");
                var store = new QuickStateArchiveStore(path);
                QuickStateSlot?[] loaded = store.Load(8, _ => null);
                Assert.Equal(8, loaded.Length);
                Assert.All(loaded, Assert.Null);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
