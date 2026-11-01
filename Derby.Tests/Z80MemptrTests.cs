using Derby.Z80;
using Xunit;

namespace Derby.Tests
{
    public class Z80MemptrTests
    {
        [Fact]
        public void ResetAndQuickState_PreserveExpectedMemptrLifecycle()
        {
            Z80Cpu cpu = CreateCpu(0x00);
            SetMemptr(cpu, 0xCAFE);
            Z80Cpu.QuickState state = cpu.CaptureQuickState();

            cpu.Reset();
            Assert.Equal((ushort)0, cpu.Memptr);

            cpu.RestoreQuickState(state);
            Assert.Equal((ushort)0xCAFE, cpu.Memptr);

            cpu.ClearSnapshotExecutionState();
            Assert.Equal((ushort)0, cpu.Memptr);
        }

        [Theory]
        [InlineData(0x02, 0x1234, 0xA5, 0xA535)] // LD (BC),A
        [InlineData(0x0A, 0x1234, 0xA5, 0x1235)] // LD A,(BC)
        [InlineData(0x12, 0x5678, 0xA5, 0xA579)] // LD (DE),A
        [InlineData(0x1A, 0x5678, 0xA5, 0x5679)] // LD A,(DE)
        public void IndirectAccumulatorLoads_SetMemptr(int opcode, int address, int accumulator, int expected)
        {
            Z80Cpu cpu = CreateCpu((byte)opcode);
            cpu.Regs.BC = (ushort)address;
            cpu.Regs.DE = (ushort)address;
            cpu.Regs.A = (byte)accumulator;

            cpu.Step();

            Assert.Equal((ushort)expected, cpu.Memptr);
        }

        [Theory]
        [InlineData(0x09)]
        [InlineData(0x19)]
        [InlineData(0x29)]
        [InlineData(0x39)]
        public void AddHl_SetMemptrFromOriginalHl(int opcode)
        {
            Z80Cpu cpu = CreateCpu((byte)opcode);
            cpu.Regs.HL = 0x7FFF;
            cpu.Regs.BC = 1;
            cpu.Regs.DE = 1;
            cpu.Regs.SP = 1;

            cpu.Step();

            Assert.Equal((ushort)0x8000, cpu.Memptr);
        }

        [Theory]
        [InlineData(0x22)] // LD (nn),HL
        [InlineData(0x2A)] // LD HL,(nn)
        public void AbsoluteHlLoads_SetMemptrToAddressPlusOne(int opcode)
        {
            byte[] memory = new byte[65536];
            memory[0] = (byte)opcode;
            memory[1] = 0xFF;
            memory[2] = 0x7F;
            Z80Cpu cpu = CreateCpuWithMemory(memory);
            cpu.Regs.HL = 0x1234;

            cpu.Step();

            Assert.Equal((ushort)0x8000, cpu.Memptr);
        }

        [Fact]
        public void LdAbsoluteA_UsesAddressAndAccumulatorSpecificMemptrRules()
        {
            byte[] memory = new byte[65536];
            memory[0] = 0x32; // LD (7FFF),A
            memory[1] = 0xFF;
            memory[2] = 0x7F;
            memory[3] = 0x3A; // LD A,(4567)
            memory[4] = 0x67;
            memory[5] = 0x45;
            memory[0x4567] = 0x11;
            Z80Cpu cpu = CreateCpuWithMemory(memory);
            cpu.Regs.A = 0xA5;

            cpu.Step();
            Assert.Equal((ushort)0xA500, cpu.Memptr);

            cpu.Step();
            Assert.Equal((ushort)0x4568, cpu.Memptr);
        }

        [Fact]
        public void ConditionalJpAndCall_LoadMemptrEvenWhenNotTaken()
        {
            byte[] memory = new byte[]
            {
                0xCA, 0x34, 0x12,       // JP Z,1234
                0xCC, 0x78, 0x56        // CALL Z,5678
            };
            Z80Cpu cpu = CreateCpuWithMemory(memory);
            cpu.Regs.F = 0;

            cpu.Step();
            Assert.Equal((ushort)0x1234, cpu.Memptr);
            Assert.Equal((ushort)3, cpu.Regs.PC);

            cpu.Step();
            Assert.Equal((ushort)0x5678, cpu.Memptr);
            Assert.Equal((ushort)6, cpu.Regs.PC);
        }

        [Fact]
        public void RelativeBranches_ChangeMemptrOnlyWhenTaken()
        {
            Z80Cpu cpu = CreateCpu(0x28, 0x02); // JR Z,+2
            cpu.Regs.F = 0;
            SetMemptr(cpu, 0xCAFE);

            cpu.Step();
            Assert.Equal((ushort)0xCAFE, cpu.Memptr);

            cpu = CreateCpu(0x28, 0x02);
            cpu.Regs.F = 0x40;
            cpu.Step();
            Assert.Equal((ushort)4, cpu.Memptr);
        }

        [Theory]
        [InlineData(0xC9)] // RET
        [InlineData(0xC0)] // RET NZ
        public void TakenReturns_SetMemptrToDestination(int opcode)
        {
            byte[] memory = new byte[65536];
            memory[0] = (byte)opcode;
            memory[0x8000] = 0x34;
            memory[0x8001] = 0x12;
            Z80Cpu cpu = CreateCpuWithMemory(memory);
            cpu.Regs.SP = 0x8000;
            cpu.Regs.F = 0;

            cpu.Step();

            Assert.Equal((ushort)0x1234, cpu.Memptr);
        }

        [Fact]
        public void ImmediateAndCRegisterIo_SetMemptrFromPort()
        {
            Z80Cpu cpu = CreateCpu(0xDB, 0xFE); // IN A,(FE)
            cpu.Regs.A = 0x12;
            cpu.ReadPort = _ => 0x34;
            cpu.Step();
            Assert.Equal((ushort)0x12FF, cpu.Memptr);

            cpu = CreateCpu(0xED, 0x41); // OUT (C),B
            cpu.Regs.BC = 0x7FFF;
            cpu.Step();
            Assert.Equal((ushort)0x8000, cpu.Memptr);
        }

        [Theory]
        [InlineData(0x42)] // SBC HL,BC
        [InlineData(0x4A)] // ADC HL,BC
        public void EdSixteenBitArithmetic_UsesOriginalHl(int opcode)
        {
            Z80Cpu cpu = CreateCpu(0xED, (byte)opcode);
            cpu.Regs.HL = 0x2FFF;
            cpu.Regs.BC = 1;

            cpu.Step();

            Assert.Equal((ushort)0x3000, cpu.Memptr);
        }

        [Theory]
        [InlineData(0x45)]
        [InlineData(0x4D)]
        [InlineData(0x55)]
        [InlineData(0x5D)]
        [InlineData(0x65)]
        [InlineData(0x6D)]
        [InlineData(0x75)]
        [InlineData(0x7D)]
        public void ReturnFromInterruptAliases_RestorePcAndMemptr(int opcode)
        {
            byte[] memory = new byte[65536];
            memory[0] = 0xED;
            memory[1] = (byte)opcode;
            memory[0x8000] = 0x34;
            memory[0x8001] = 0x12;
            Z80Cpu cpu = CreateCpuWithMemory(memory);
            cpu.Regs.SP = 0x8000;
            cpu.RestoreInterruptState(false, true, 1);

            cpu.Step();

            Assert.Equal((ushort)0x1234, cpu.Regs.PC);
            Assert.Equal((ushort)0x1234, cpu.Memptr);
            Assert.True(cpu.IFF1);
        }

        [Fact]
        public void IndexedMemoryAndIndexedBit_SetMemptrToEffectiveAddress()
        {
            byte[] memory = new byte[65536];
            memory[0] = 0xDD;
            memory[1] = 0x7E; // LD A,(IX-2)
            memory[2] = 0xFE;
            memory[3] = 0xFD;
            memory[4] = 0xCB; // BIT 0,(IY+5)
            memory[5] = 0x05;
            memory[6] = 0x46;
            Z80Cpu cpu = CreateCpuWithMemory(memory);
            cpu.Regs.IX = 0x4000;
            cpu.Regs.IY = 0x27FC;

            cpu.Step();
            Assert.Equal((ushort)0x3FFE, cpu.Memptr);

            cpu.Step();
            Assert.Equal((ushort)0x2801, cpu.Memptr);
            Assert.Equal(0x28, cpu.Regs.F & 0x28);
        }

        [Fact]
        public void BitAtHl_TakesUndocumentedFlagsFromMemptrHighByte()
        {
            byte[] memory = new byte[65536];
            memory[0] = 0xCB;
            memory[1] = 0x46; // BIT 0,(HL)
            Z80Cpu cpu = CreateCpuWithMemory(memory);
            cpu.Regs.HL = 0x4000;
            SetMemptr(cpu, 0x2800);

            cpu.Step();

            Assert.Equal(0x28, cpu.Regs.F & 0x28);
        }

        [Fact]
        public void BlockInstructions_FollowRepeatAndPortRules()
        {
            byte[] memory = new byte[65536];
            memory[0] = 0xED;
            memory[1] = 0xB0; // LDIR
            memory[0x4000] = 0xAA;
            Z80Cpu cpu = CreateCpuWithMemory(memory);
            cpu.Regs.HL = 0x4000;
            cpu.Regs.DE = 0x5000;
            cpu.Regs.BC = 2;

            cpu.Step();
            Assert.Equal((ushort)1, cpu.Memptr);

            cpu = CreateCpu(0xED, 0xA2); // INI
            cpu.Regs.BC = 0x12FE;
            cpu.Regs.HL = 0x4000;
            cpu.ReadPort = _ => 0;
            cpu.Step();
            Assert.Equal((ushort)0x12FF, cpu.Memptr);

            cpu = CreateCpu(0xED, 0xA3); // OUTI
            cpu.Regs.BC = 0x12FE;
            cpu.Regs.HL = 0x4000;
            cpu.Step();
            Assert.Equal((ushort)0x11FF, cpu.Memptr);

            cpu = CreateCpu(0xED, 0xB2); // INIR
            cpu.Regs.BC = 0x12FE;
            cpu.Regs.HL = 0x4000;
            cpu.ReadPort = _ => 0;
            cpu.Step();
            Assert.Equal((ushort)1, cpu.Memptr);

            cpu = CreateCpu(0xED, 0xB3); // OTIR
            cpu.Regs.BC = 0x12FE;
            cpu.Regs.HL = 0x4000;
            cpu.Step();
            Assert.Equal((ushort)1, cpu.Memptr);
        }

        [Theory]
        [InlineData(1, 0x0038)]
        [InlineData(2, 0x5678)]
        public void MaskableInterrupt_SetsMemptrToVectorDestination(int mode, int expected)
        {
            byte[] memory = new byte[65536];
            memory[0x12FF] = 0x78;
            memory[0x1300] = 0x56;
            Z80Cpu cpu = CreateCpuWithMemory(memory);
            cpu.Regs.I = 0x12;
            cpu.RestoreInterruptState(true, true, mode);
            cpu.InterruptPending = true;

            cpu.ExecuteInstructionFetches(1);

            Assert.Equal((ushort)expected, cpu.Memptr);
        }

        private static Z80Cpu CreateCpu(params byte[] program)
        {
            byte[] memory = new byte[65536];
            program.CopyTo(memory, 0);
            return CreateCpuWithMemory(memory);
        }

        private static Z80Cpu CreateCpuWithMemory(byte[] memory) => new Z80Cpu
        {
            ReadMemory = address => memory[address],
            WriteMemory = (address, value) => memory[address] = value
        };

        private static void SetMemptr(Z80Cpu cpu, ushort value) =>
            cpu.RestoreSnapshotExecutionState(false, false, false, false, value);
    }
}
