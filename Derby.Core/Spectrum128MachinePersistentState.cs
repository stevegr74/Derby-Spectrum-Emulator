using System.Text;
using Derby.Audio;
using Derby.Z80;

namespace Derby
{
    public sealed partial class Spectrum128Machine
    {
        private const int PersistentStateVersion = 1;
        private static readonly byte[] PersistentStateMagic = Encoding.ASCII.GetBytes("DRBQST");

        public byte[] CapturePersistentState()
        {
            QuickState state = CaptureQuickState();
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(PersistentStateMagic);
            writer.Write(PersistentStateVersion);
            WriteCpuState(writer, state.CpuState);
            writer.Write((int)state.Model);
            writer.Write(state.AyHardwareEnabled);
            writer.Write(state.PagedRamBank);
            writer.Write(state.CurrentRomBank);
            writer.Write(state.PagingLocked);
            writer.Write(state.ScreenBank);
            writer.Write(state.BorderColor);
            writer.Write(state.FrameCount);
            writer.Write(state.Last7ffdValue);
            writer.Write(state.Last1ffdValue);
            writer.Write(state.Uses48kMemoryMap);
            writer.Write(state.FrameTStates);
            writer.Write(state.LastAyRegister);
            writer.Write(state.SpeakerHigh);
            writer.Write(state.MicHigh);
            writer.Write(state.SpeakerEdge);
            writer.Write(state.FrameStartSpeakerHigh);
            writer.Write(state.FrameStartTStates);
            writer.Write(state.FrameStartBorderColor);
            writer.Write(state.CurrentFrameExecutedTStates);
            writer.Write(state.LastAudioFrameTStates);
            writer.Write(state.FloatingBusDisplayStartAdjustTStates);
            writer.Write(state.FloatingBusSampleAdjustTStates);
            writer.Write(state.TStatesUntilNextInterrupt);
            writer.Write(state.RealignInterruptPhaseAfterNextAccept);
            writer.Write(state.InterruptPulseEndTStates.HasValue);
            if (state.InterruptPulseEndTStates.HasValue)
                writer.Write(state.InterruptPulseEndTStates.Value);

            byte[] ayRegisters = state.AyState.GetRegistersCopy();
            writer.Write(state.AyState.SelectedRegister);
            writer.Write(ayRegisters);
            for (int bank = 0; bank < state.RamBanks.Length; bank++)
                writer.Write(state.RamBanks[bank]);
            writer.Flush();
            return stream.ToArray();
        }

        public void RestorePersistentState(byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);
            PersistentStateData restored = ReadPersistentState(data);

            Reset(restored.Model);
            QuickState state = CaptureQuickState();
            state.CpuState = restored.CpuState;
            state.RamBanks = restored.RamBanks;
            state.Model = restored.Model;
            state.AyHardwareEnabled = restored.AyHardwareEnabled;
            state.PagedRamBank = restored.PagedRamBank;
            state.CurrentRomBank = restored.CurrentRomBank;
            state.PagingLocked = restored.PagingLocked;
            state.ScreenBank = restored.ScreenBank;
            state.BorderColor = restored.BorderColor;
            state.FrameCount = restored.FrameCount;
            state.Last7ffdValue = restored.Last7ffdValue;
            state.Last1ffdValue = restored.Last1ffdValue;
            state.Uses48kMemoryMap = restored.Uses48kMemoryMap;
            state.FrameTStates = restored.FrameTStates;
            state.LastAyRegister = restored.LastAyRegister;
            state.SpeakerHigh = restored.SpeakerHigh;
            state.MicHigh = restored.MicHigh;
            state.SpeakerEdge = restored.SpeakerEdge;
            state.FrameStartSpeakerHigh = restored.FrameStartSpeakerHigh;
            state.FrameStartTStates = restored.FrameStartTStates;
            state.FrameStartBorderColor = restored.FrameStartBorderColor;
            state.CurrentFrameExecutedTStates = restored.CurrentFrameExecutedTStates;
            state.LastAudioFrameTStates = restored.LastAudioFrameTStates;
            state.FloatingBusDisplayStartAdjustTStates = restored.FloatingBusDisplayStartAdjustTStates;
            state.FloatingBusSampleAdjustTStates = restored.FloatingBusSampleAdjustTStates;
            state.TStatesUntilNextInterrupt = restored.TStatesUntilNextInterrupt;
            state.RealignInterruptPhaseAfterNextAccept = restored.RealignInterruptPhaseAfterNextAccept;
            state.InterruptPulseEndTStates = restored.InterruptPulseEndTStates;
            state.AyState = new Ay8912.QuickState(restored.AyRegisters, restored.AySelectedRegister);
            RestoreQuickState(state);
        }

        private static PersistentStateData ReadPersistentState(byte[] data)
        {
            try
            {
                using var stream = new MemoryStream(data, writable: false);
                using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
                if (!reader.ReadBytes(PersistentStateMagic.Length).SequenceEqual(PersistentStateMagic))
                    throw new InvalidDataException("The Quick State payload has an invalid signature.");
                int version = reader.ReadInt32();
                if (version != PersistentStateVersion)
                    throw new NotSupportedException($"Quick State payload version {version} is not supported.");

                var restored = new PersistentStateData
                {
                    CpuState = ReadCpuState(reader),
                    Model = ReadModel(reader),
                    AyHardwareEnabled = reader.ReadBoolean(),
                    PagedRamBank = reader.ReadInt32(),
                    CurrentRomBank = reader.ReadInt32(),
                    PagingLocked = reader.ReadBoolean(),
                    ScreenBank = reader.ReadInt32(),
                    BorderColor = reader.ReadInt32(),
                    FrameCount = reader.ReadInt32(),
                    Last7ffdValue = reader.ReadByte(),
                    Last1ffdValue = reader.ReadByte(),
                    Uses48kMemoryMap = reader.ReadBoolean(),
                    FrameTStates = reader.ReadInt32(),
                    LastAyRegister = reader.ReadByte(),
                    SpeakerHigh = reader.ReadBoolean(),
                    MicHigh = reader.ReadBoolean(),
                    SpeakerEdge = reader.ReadBoolean(),
                    FrameStartSpeakerHigh = reader.ReadBoolean(),
                    FrameStartTStates = reader.ReadUInt64(),
                    FrameStartBorderColor = reader.ReadInt32(),
                    CurrentFrameExecutedTStates = reader.ReadInt32(),
                    LastAudioFrameTStates = reader.ReadInt32(),
                    FloatingBusDisplayStartAdjustTStates = reader.ReadInt32(),
                    FloatingBusSampleAdjustTStates = reader.ReadInt32(),
                    TStatesUntilNextInterrupt = reader.ReadInt32(),
                    RealignInterruptPhaseAfterNextAccept = reader.ReadBoolean(),
                    InterruptPulseEndTStates = reader.ReadBoolean() ? reader.ReadUInt64() : null,
                    AySelectedRegister = reader.ReadInt32(),
                    AyRegisters = ReadExact(reader, 16),
                    RamBanks = Enumerable.Range(0, 8).Select(_ => ReadExact(reader, 16384)).ToArray()
                };
                if (stream.Position != stream.Length)
                    throw new InvalidDataException("The Quick State payload contains unexpected trailing data.");
                ValidatePersistentState(restored);
                return restored;
            }
            catch (EndOfStreamException exception)
            {
                throw new InvalidDataException("The Quick State payload is truncated.", exception);
            }
        }

        private static void ValidatePersistentState(PersistentStateData state)
        {
            if ((uint)state.PagedRamBank >= 8 || (uint)state.ScreenBank >= 8)
                throw new InvalidDataException("The Quick State paging data is invalid.");
            if (state.CurrentRomBank < 0 || state.CurrentRomBank > 3)
                throw new InvalidDataException("The Quick State ROM selection is invalid.");
            if ((uint)state.BorderColor >= 8 || (uint)state.FrameStartBorderColor >= 8)
                throw new InvalidDataException("The Quick State border colour is invalid.");
            if ((uint)state.AySelectedRegister >= 16)
                throw new InvalidDataException("The Quick State AY selection is invalid.");
            if (state.FrameTStates <= 0 || state.CurrentFrameExecutedTStates < 0)
                throw new InvalidDataException("The Quick State timing data is invalid.");
        }

        private static SpectrumMachineModel ReadModel(BinaryReader reader)
        {
            int value = reader.ReadInt32();
            if (!Enum.IsDefined(typeof(SpectrumMachineModel), value))
                throw new InvalidDataException("The Quick State machine model is invalid.");
            return (SpectrumMachineModel)value;
        }

        private static byte[] ReadExact(BinaryReader reader, int length)
        {
            byte[] result = reader.ReadBytes(length);
            if (result.Length != length)
                throw new EndOfStreamException();
            return result;
        }

        private static void WriteCpuState(BinaryWriter writer, Z80Cpu.QuickState state)
        {
            writer.Write(state.AF); writer.Write(state.BC); writer.Write(state.DE); writer.Write(state.HL);
            writer.Write(state.A_); writer.Write(state.F_); writer.Write(state.B_); writer.Write(state.C_);
            writer.Write(state.D_); writer.Write(state.E_); writer.Write(state.H_); writer.Write(state.L_);
            writer.Write(state.IX); writer.Write(state.IY); writer.Write(state.SP); writer.Write(state.PC);
            writer.Write(state.I); writer.Write(state.R); writer.Write(state.TStates); writer.Write(state.InstructionFetchCount);
            writer.Write(state.Halted); writer.Write(state.InterruptPending); writer.Write(state.IFF1); writer.Write(state.IFF2);
            writer.Write(state.InterruptMode); writer.Write(state.EiDelay); writer.Write(state.SuppressInterruptsForNextInstruction);
            writer.Write(state.QFlags); writer.Write(state.Memptr); writer.Write(state.LastInterruptProgressTStates);
        }

        private static Z80Cpu.QuickState ReadCpuState(BinaryReader reader) => new()
        {
            AF = reader.ReadUInt16(), BC = reader.ReadUInt16(), DE = reader.ReadUInt16(), HL = reader.ReadUInt16(),
            A_ = reader.ReadByte(), F_ = reader.ReadByte(), B_ = reader.ReadByte(), C_ = reader.ReadByte(),
            D_ = reader.ReadByte(), E_ = reader.ReadByte(), H_ = reader.ReadByte(), L_ = reader.ReadByte(),
            IX = reader.ReadUInt16(), IY = reader.ReadUInt16(), SP = reader.ReadUInt16(), PC = reader.ReadUInt16(),
            I = reader.ReadByte(), R = reader.ReadByte(), TStates = reader.ReadUInt64(), InstructionFetchCount = reader.ReadUInt64(),
            Halted = reader.ReadBoolean(), InterruptPending = reader.ReadBoolean(), IFF1 = reader.ReadBoolean(), IFF2 = reader.ReadBoolean(),
            InterruptMode = reader.ReadInt32(), EiDelay = reader.ReadInt32(), SuppressInterruptsForNextInstruction = reader.ReadBoolean(),
            QFlags = reader.ReadByte(), Memptr = reader.ReadUInt16(), LastInterruptProgressTStates = reader.ReadUInt64()
        };

        private sealed class PersistentStateData
        {
            public Z80Cpu.QuickState CpuState = null!;
            public SpectrumMachineModel Model;
            public bool AyHardwareEnabled;
            public int PagedRamBank;
            public int CurrentRomBank;
            public bool PagingLocked;
            public int ScreenBank;
            public int BorderColor;
            public int FrameCount;
            public byte Last7ffdValue;
            public byte Last1ffdValue;
            public bool Uses48kMemoryMap;
            public int FrameTStates;
            public byte LastAyRegister;
            public bool SpeakerHigh;
            public bool MicHigh;
            public bool SpeakerEdge;
            public bool FrameStartSpeakerHigh;
            public ulong FrameStartTStates;
            public int FrameStartBorderColor;
            public int CurrentFrameExecutedTStates;
            public int LastAudioFrameTStates;
            public int FloatingBusDisplayStartAdjustTStates;
            public int FloatingBusSampleAdjustTStates;
            public int TStatesUntilNextInterrupt;
            public bool RealignInterruptPhaseAfterNextAccept;
            public ulong? InterruptPulseEndTStates;
            public int AySelectedRegister;
            public byte[] AyRegisters = null!;
            public byte[][] RamBanks = null!;
        }
    }
}
