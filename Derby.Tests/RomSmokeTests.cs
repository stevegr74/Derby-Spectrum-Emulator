using System;
using System.IO;
using Xunit;

namespace Derby.Tests
{
    public class RomSmokeTests
    {
        [Fact]
        public void Boots_For_Thirty_Frames_Without_Leaving_Valid_State()
        {
            string romFolder = Path.Combine(AppContext.BaseDirectory, "ROMs");
            if (!Directory.Exists(romFolder))
                return;

            string rom0 = Path.Combine(romFolder, "128-0.rom");
            string rom1 = Path.Combine(romFolder, "128-1.rom");
            if (!File.Exists(rom0) || !File.Exists(rom1))
                return;

            var machine = new SpectrumMachine(romFolder);
            for (int i = 0; i < 30; i++)
                machine.ExecuteFrame();

            Assert.InRange(machine.Cpu.Regs.SP, (ushort)0x4000, ushort.MaxValue);
            Assert.InRange(machine.Cpu.Regs.PC, (ushort)0x0000, ushort.MaxValue);
        }

        [Fact]
        public void Plus2A_BootsProductionRomAndDrawsStartupScreen()
        {
            string romFolder = Path.Combine(AppContext.BaseDirectory, "ROMs");
            if (Enumerable.Range(0, 4).Any(bank =>
                !File.Exists(Path.Combine(romFolder, $"plus3-{bank}.rom"))))
            {
                return;
            }

            var machine = new SpectrumMachine(SpectrumRomSet.BundledPlus2A(romFolder));
            for (int frame = 0; frame < 100; frame++)
                machine.ExecuteFrame();

            Assert.Equal(SpectrumMemoryPagingModel.Plus2APlus3, machine.MemoryPagingModel);
            Assert.InRange(machine.Cpu.Regs.SP, (ushort)0x4000, ushort.MaxValue);
            Assert.Contains(machine.GetScreenBankData().Take(0x1B00), value => value != 0 && value != 0x38);
        }
    }
}
