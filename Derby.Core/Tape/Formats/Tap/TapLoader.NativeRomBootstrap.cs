using System;
using System.Collections.Generic;
using static Derby.Tape.SpectrumRomWorkspace;

namespace Derby.Tape
{
    public static partial class TapLoader
    {
        internal static bool HasLeadingStandardCodePair(IReadOnlyList<TapeBlock> blocks)
        {
            int index = 0;
            while (index < blocks.Count && blocks[index].Kind == TapeBlockKind.Metadata)
                index++;

            if (index + 1 >= blocks.Count || !IsStandardHeaderBlock(blocks[index]))
                return false;

            TapHeaderInfo header = ParseHeaderInfo(blocks[index]);
            TapeBlock data = blocks[index + 1];
            return header.Type == CodeType && blocks[index].IsLoadableRomBlock && data.Kind == TapeBlockKind.Data &&
                data.Flag == DataFlag && data.IsLoadableRomBlock && data.Payload?.Length == header.DataLength;
        }

        internal static bool HasCodeOverlappingBootstrapStack(IReadOnlyList<TapeBlock> blocks)
        {
            foreach (TapeBlock block in blocks)
            {
                if (!IsStandardHeaderBlock(block))
                    continue;

                TapHeaderInfo header = ParseHeaderInfo(block);
                if (header.Type == CodeType &&
                    header.StartAddress <= DefaultStackPointer &&
                    header.StartAddress + header.DataLength > DefaultStackPointer)
                    return true;
            }

            return false;
        }

        internal static bool HasHeaderlessStandardDataAfterHeaderedPrefix(IReadOnlyList<TapeBlock> blocks)
        {
            int pairs = 0;
            for (int index = 0; index < blocks.Count; index++)
            {
                TapeBlock block = blocks[index];
                if (block.Kind == TapeBlockKind.Metadata)
                    continue;

                if (index + 1 < blocks.Count && IsStandardHeaderBlock(block) &&
                    blocks[index + 1].Flag == DataFlag && blocks[index + 1].IsLoadableRomBlock)
                {
                    pairs++;
                    index++;
                    continue;
                }

                return pairs >= 2 && block.IsLoadableRomBlock && block.Flag == DataFlag;
            }

            return false;
        }

        internal static TapBootstrapResult StartNativeRomTapeLoader(
            SpectrumMachine machine,
            string displayName,
            IReadOnlyList<TapeBlock> blocks,
            int nonRomTimingDivisor,
            int loadableTimingDivisor,
            bool initialEarLevelHigh,
            bool use48Basic = false,
            bool loadCode = false)
        {
            bool use128kMode = Use128kTapeLoadMode(machine);
            // Some tapes restore a 48 BASIC workspace without requiring 48K hardware.
            return use128kMode && !use48Basic && !loadCode
                ? StartNative128kRomTapeLoader(
                    machine,
                    displayName,
                    blocks,
                    nonRomTimingDivisor,
                    loadableTimingDivisor,
                    initialEarLevelHigh)
                : StartNative48BasicTapeLoader(
                    machine,
                    displayName,
                    blocks,
                    nonRomTimingDivisor,
                    loadableTimingDivisor,
                    initialEarLevelHigh,
                    use128kMode,
                    loadCode);
        }

        private static TapBootstrapResult StartNative48BasicTapeLoader(
            SpectrumMachine machine,
            string displayName,
            IReadOnlyList<TapeBlock> blocks,
            int nonRomTimingDivisor,
            int loadableTimingDivisor,
            bool initialEarLevelHigh,
            bool use128kMode,
            bool loadCode)
        {
            InitializeMachineForRomDrivenTapeLoad(machine, use128kMode);

            PressRomKey(machine, row: 6, bit: 3); // J: LOAD keyword in K mode.
            PressRomKeyChord(machine, (7, 1), (5, 0)); // Symbol Shift+P: quote.
            PressRomKeyChord(machine, (7, 1), (5, 0));

            if (loadCode)
            {
                PressRomKeyChord(machine, (0, 0), (7, 1)); // Both shifts: extended mode.
                PressRomKey(machine, row: 5, bit: 2); // I: CODE in extended mode.
            }

            var tape = new MountedTape(
                displayName,
                blocks,
                initialBlockIndex: 0,
                skipCustomHeaderForEarPlayback: false,
                nonRomTimingDivisor: nonRomTimingDivisor,
                loadableTimingDivisor: loadableTimingDivisor,
                initialEarLevelHigh: initialEarLevelHigh,
                allowRomLoadTraps: false);
            machine.MountTape(tape);

            PressRomKey(machine, row: 6, bit: 0); // Enter.

            bool enteredTapeLoader = false;
            for (int frame = 0; frame < 100; frame++)
            {
                machine.ExecuteTimeSlice(machine.FrameTStates);
                ushort programCounter = machine.Cpu.Regs.PC;
                if ((machine.PeekMemory(FlagsSystemVariableAddress) & 0x10) != 0 ||
                    programCounter is >= 0x0556 and <= 0x0605)
                {
                    enteredTapeLoader = true;
                    break;
                }
            }

            machine.ClearKeyboard();
            if (!enteredTapeLoader)
                throw new InvalidOperationException("The Spectrum 48 ROM did not enter its tape loader.");

            string? autoStartFileName = null;
            foreach (TapeBlock block in blocks)
            {
                if (!IsStandardHeaderBlock(block))
                    continue;

                TapHeaderInfo header = ParseHeaderInfo(block);
                if (header.Type == ProgramType)
                    autoStartFileName = header.FileName;
                break;
            }

            return new TapBootstrapResult(blocks.Count, 0, displayName, autoStartFileName);
        }

        private static void PressRomKey(SpectrumMachine machine, int row, int bit)
        {
            machine.SetKey(row, bit, true);
            ExecuteRomInputFrames(machine, 4);
            machine.SetKey(row, bit, false);
            ExecuteRomInputFrames(machine, 4);
        }

        private static void PressRomKeyChord(
            SpectrumMachine machine,
            (int Row, int Bit) first,
            (int Row, int Bit) second)
        {
            machine.SetKey(first.Row, first.Bit, true);
            machine.SetKey(second.Row, second.Bit, true);
            ExecuteRomInputFrames(machine, 4);
            machine.SetKey(second.Row, second.Bit, false);
            machine.SetKey(first.Row, first.Bit, false);
            ExecuteRomInputFrames(machine, 4);
        }

        private static void ExecuteRomInputFrames(SpectrumMachine machine, int frameCount)
        {
            for (int frame = 0; frame < frameCount; frame++)
                machine.ExecuteTimeSlice(machine.FrameTStates);
        }
    }
}
