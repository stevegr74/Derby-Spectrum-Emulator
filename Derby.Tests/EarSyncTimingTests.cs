using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Derby.Tape;
using Xunit;

namespace Derby.Tests
{
    public class EarSyncTimingTests
    {
        private const ushort Entry = 0xC000;
        private const ushort Results = 0x8000;

        // Measure the number of polls between successive EAR transitions. All
        // results are stored in fixed, uncontended RAM regardless of the code bank.
        private static readonly byte[] Poller =
        {
            0x3E, 0x7F,       // LD A,$7F
            0xDB, 0xFE,       // IN A,($FE)
            0xE6, 0x40,       // AND $40
            0x4F,             // LD C,A
            0x06, 0x00,       // $C007: LD B,0
            0x04,             // $C009: INC B
            0x3E, 0x7F,       // LD A,$7F
            0xDB, 0xFE,       // IN A,($FE)
            0xE6, 0x40,       // AND $40
            0xB9,             // CP C
            0xCA, 0x09, 0xC0, // JP Z,$C009
            0x4F,             // LD C,A
            0x70,             // LD (HL),B
            0x2C,             // INC L
            0x15,             // DEC D
            0xC2, 0x07, 0xC0, // JP NZ,$C007
            0x76              // HALT
        };

        // Expectations use the 128K bus-cycle schedule and scaled pulse lengths,
        // not a capture of this program running on physical hardware.
        [Theory]
        [InlineData(0, 10000, 10017, "060A050B040B", "10272 10738 10989 11498 11706 12215")]
        [InlineData(3, 10000, 10017, "060A050B040B", "10272 10738 10989 11498 11706 12215")]
        [InlineData(0, 14361, 14378, "060A040A050A", "14646 15126 15346 15842 16105 16588")]
        [InlineData(3, 14361, 14402, "040703070307", "14654 15158 15402 15879 16114 16592")]
        [InlineData(0, 14364, 14386, "0609050A040A", "14654 15102 15362 15858 16078 16569")]
        [InlineData(3, 14364, 14402, "040703070307", "14654 15158 15402 15879 16114 16592")]
        public void Original128k_PollingLoopMeasuresSyntheticEarEdges(
            byte bank, int phase, int firstSample, string counts, string samplesAtEdges)
        {
            string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                File.WriteAllBytes(Path.Combine(folder, "128-0.rom"), new byte[16384]);
                File.WriteAllBytes(Path.Combine(folder, "128-1.rom"), new byte[16384]);
                var machine = new SpectrumMachine(folder);
                machine.ConfigureFor128kSnapshot(last7ffdValue: bank, borderColor: 0);
                machine.SetDebugEventCaptureEnabled(true);
                for (int i = 0; i < Poller.Length; i++)
                    machine.PokeMemory((ushort)(Entry + i), Poller[i]);
                machine.Cpu.Regs.PC = Entry;
                machine.Cpu.Regs.HL = Results;
                machine.Cpu.Regs.D = 6;
                machine.SetSnapshotResumeFramePhase(phase);

                var tape = new MountedTape("synthetic-ear-pulses", new[]
                {
                    TapeBlock.CreatePulseSequence(new[] { 241, 481, 240, 480, 240, 480, 5000 })
                }, initialEarLevelHigh: false, allowRomLoadTraps: false);
                machine.MountTape(tape);
                Assert.False(tape.ReadEarBit(machine.Cpu.TStates)); // Anchor the first pulse at the entry phase.
                machine.ExecuteTimeSlice(3000);

                Assert.True(machine.Cpu.IsHalted);
                Assert.Equal((ushort)(Results + 6), machine.Cpu.Regs.HL);
                var measuredCounts = Enumerable.Range(0, 6).Select(i => machine.PeekMemory((ushort)(Results + i)));
                Assert.Equal(counts, Convert.ToHexString(measuredCounts.ToArray()));

                var reads = Regex.Matches(machine.BuildDebugDump(), @"T=\s*(\d+) IN  7FFE=([0-9A-F]{2})")
                    .Select(m => (Time: int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                        Level: (Convert.ToByte(m.Groups[2].Value, 16) & 0x40) != 0))
                    .ToArray();
                Assert.NotEmpty(reads);
                Assert.Equal((firstSample, false), reads[0]);
                var changes = reads.Skip(1).Where((read, i) => read.Level != reads[i].Level).Select(read => read.Time);
                Assert.Equal(samplesAtEdges, string.Join(" ", changes));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
