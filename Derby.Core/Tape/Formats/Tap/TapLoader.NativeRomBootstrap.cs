using System;
using System.Collections.Generic;
using static Derby.Tape.SpectrumRomWorkspace;

namespace Derby.Tape
{
    public static partial class TapLoader
    {
        private const int NativeTapeMenuStartupFrames = 260;
        private const int NativeTapeMenuEnterFrames = 20;

        internal static bool HasLeadingStandardCodePair(IReadOnlyList<TapeBlock> blocks)
            => TryGetLeadingStandardPair(blocks, CodeType, out _);

        internal static bool TryGetLeadingStandardBasicPair(
            IReadOnlyList<TapeBlock> blocks, out string? fileName)
            => TryGetLeadingStandardPair(blocks, ProgramType, out fileName);

        private static bool TryGetLeadingStandardPair(
            IReadOnlyList<TapeBlock> blocks, byte type, out string? fileName)
        {
            fileName = null;
            int index = 0;
            while (index < blocks.Count && blocks[index].Kind == TapeBlockKind.Metadata)
                index++;

            if (index + 1 >= blocks.Count || !IsStandardHeaderBlock(blocks[index]))
                return false;

            TapHeaderInfo header = ParseHeaderInfo(blocks[index]);
            TapeBlock data = blocks[index + 1];
            if (header.Type != type || !blocks[index].IsLoadableRomBlock || data.Kind != TapeBlockKind.Data ||
                data.Flag != DataFlag || !data.IsLoadableRomBlock || data.Payload?.Length != header.DataLength)
                return false;

            fileName = header.FileName;
            return true;
        }

        internal static TapBootstrapResult StartNativeMenuTapeLoader(
            SpectrumMachine machine,
            string displayName,
            IReadOnlyList<TapeBlock> blocks,
            bool initialEarLevelHigh)
        {
            machine.Reset(SpectrumMachineModel.Spectrum128K);

            // Let the bundled ROM finish startup. Its default menu entry loads tape.
            // No ROM workspace addresses or fast-load traps are used on these models.
            ExecuteRomInputFrames(machine, NativeTapeMenuStartupFrames);
            machine.SetKey(6, 0, true); // Enter.
            try
            {
                ExecuteRomInputFrames(machine, NativeTapeMenuEnterFrames);
                machine.MountTape(new MountedTape(displayName, blocks,
                    skipCustomHeaderForEarPlayback: false,
                    initialEarLevelHigh: initialEarLevelHigh,
                    allowRomLoadTraps: false));
            }
            finally
            {
                machine.ClearKeyboard();
            }

            TryGetLeadingStandardBasicPair(blocks, out string? fileName);
            return new TapBootstrapResult(blocks.Count, 0, displayName, fileName);
        }

        internal static bool LeadingBasicLoadsCodeAcrossRamTop(IReadOnlyList<TapeBlock> blocks)
        {
            int index = 0;
            while (index < blocks.Count && blocks[index].Kind == TapeBlockKind.Metadata)
                index++;
            if (index + 1 >= blocks.Count || !IsStandardHeaderBlock(blocks[index]) ||
                blocks[index + 1].Payload is not byte[] program)
                return false;

            TapHeaderInfo basic = ParseHeaderInfo(blocks[index]);
            if (basic.Type != ProgramType || basic.AutoStartLine >= 32768 ||
                !BasicBootstrapExecutor.TryGetRamTopBeforeHeaderAddressCodeLoad(
                    program, basic.ProgramLength, basic.AutoStartLine, out int ramTop))
                return false;

            for (index += 2; index + 1 < blocks.Count; index++)
            {
                if (!IsStandardHeaderBlock(blocks[index]))
                    continue;
                TapHeaderInfo header = ParseHeaderInfo(blocks[index]);
                if (header.Type == CodeType)
                    return header.StartAddress <= ramTop && header.StartAddress + header.DataLength > ramTop;
            }

            return false;
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

        internal static bool LeadingBasicOverridesScreenHeaderAddress(IReadOnlyList<TapeBlock> blocks)
        {
            int index = 0;
            while (index < blocks.Count && blocks[index].Kind == TapeBlockKind.Metadata)
                index++;
            if (index + 1 >= blocks.Count || !IsStandardHeaderBlock(blocks[index]) ||
                blocks[index + 1].Payload is not byte[] program)
                return false;

            TapHeaderInfo basic = ParseHeaderInfo(blocks[index]);
            if (basic.Type != ProgramType || basic.AutoStartLine >= 32768)
                return false;

            index += 2;
            while (index < blocks.Count && blocks[index].Kind == TapeBlockKind.Metadata)
                index++;
            if (index >= blocks.Count || !IsStandardHeaderBlock(blocks[index]))
                return false;

            TapHeaderInfo screen = ParseHeaderInfo(blocks[index]);
            return screen.Type == CodeType && screen.StartAddress != 0x4000 &&
                   BasicBootstrapExecutor.FirstLoadUsesScreenDestination(program, basic.ProgramLength, basic.AutoStartLine);
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

        internal static bool HasToneDelimitedProtectedCodeHandoff(IReadOnlyList<TapeBlock> blocks)
        {
            int index = 0;

            TapeBlock? NextSignal()
            {
                while (index < blocks.Count && blocks[index].Kind == TapeBlockKind.Metadata)
                    index++;
                return index < blocks.Count ? blocks[index++] : null;
            }

            foreach (byte type in new[] { ProgramType, CodeType })
            {
                TapeBlock? headerBlock = NextSignal();
                if (headerBlock is null || !IsStandardHeaderBlock(headerBlock) || !headerBlock.CanUseRomLoadTrap)
                    return false;

                TapHeaderInfo header = ParseHeaderInfo(headerBlock);
                TapeBlock? data = NextSignal();
                if (header.Type != type || data is not { CanUseRomLoadTrap: true, Flag: DataFlag } ||
                    data.Payload?.Length != header.DataLength)
                    return false;
            }

            return NextSignal() is { Kind: TapeBlockKind.PureTone } &&
                   NextSignal() is { Kind: TapeBlockKind.Pause, PauseAfterBlockMs: > 0 } &&
                   NextSignal() is { Kind: TapeBlockKind.Data, CanUseRomLoadTrap: false };
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
