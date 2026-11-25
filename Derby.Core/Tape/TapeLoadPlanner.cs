using System;
using System.Collections.Generic;

namespace Derby.Tape
{
    internal static class TapeLoadPlanner
    {
        public static TapeLoadPlan Create(SpectrumMachine machine, IReadOnlyList<TapeBlock> blocks)
        {
            ArgumentNullException.ThrowIfNull(machine);

            if (!machine.SupportsRomTapeAcceleration)
            {
                if (machine.RomProfile.HasNativeTapeMenu && TapLoader.TryGetLeadingStandardBasicPair(blocks, out _))
                {
                    return new TapeLoadPlan(
                        TapeLoadStrategy.RomBootstrapMounted,
                        TapeLoadPlanReason.NativeMenuWithoutAcceleration,
                        "The selected ROM starts this BASIC tape through its native Loader menu without acceleration.");
                }

                return new TapeLoadPlan(
                    TapeLoadStrategy.MountedRealtime,
                    TapeLoadPlanReason.IncompatibleRomProfile,
                    $"ROM profile '{machine.RomProfile.Name}' is not compatible with address-based tape acceleration.");
            }

            bool canStartNativeRom = !ReferenceEquals(machine.RomProfile, SpectrumRomProfile.SyntheticTest);
            if (canStartNativeRom && TapLoader.HasLeadingStandardCodePair(blocks))
            {
                return new TapeLoadPlan(
                    TapeLoadStrategy.RomBootstrapMounted,
                    TapeLoadPlanReason.LeadingCodeRequiresNativeLoad,
                    "Tape starts with a standard CODE file rather than an autorun BASIC program.");
            }

            if (TapLoader.HasInconsistentStandardBasicHeaderDataPair(blocks))
            {
                return new TapeLoadPlan(
                    TapeLoadStrategy.RomBootstrapMounted,
                    TapeLoadPlanReason.InconsistentStandardBasicPair,
                    "Tape contains an internally inconsistent BASIC header/data pair that must be interpreted by the real ROM loader.");
            }

            if (TapLoader.HasNonCanonicalLeadingBasicProgram(blocks, checkAutoStartLine: canStartNativeRom))
            {
                return new TapeLoadPlan(
                    TapeLoadStrategy.RomBootstrapMounted,
                    TapeLoadPlanReason.NonCanonicalBasicProgram,
                    "The leading BASIC program's line layout or autorun line requires the real ROM loader.");
            }

            if (TapLoader.CanLoadAllStandardTapeBlocks(blocks))
            {
                if (TapLoader.LeadingBasicLoadsCodeIntoRomWorkspace(blocks))
                {
                    return new TapeLoadPlan(
                        TapeLoadStrategy.RomBootstrapMounted,
                        TapeLoadPlanReason.CodeLoadReplacesRomWorkspace,
                        "BASIC loads CODE into the live ROM workspace, which requires native tape execution.");
                }

                bool use128kMode = TapLoader.Use128kTapeLoadMode(machine);
                if (TapLoader.RequiresRomDrivenBootstrapForStandardTape(machine, blocks, use128kMode))
                {
                    return new TapeLoadPlan(
                        TapeLoadStrategy.RomBootstrapMounted,
                        TapeLoadPlanReason.StandardBasicChainRequiresRom,
                        "Standard tape chains from an autorun BASIC stage into a second BASIC loader and should resume under ROM control.");
                }

                if (TapLoader.RequiresMountedLoadSemanticsForStandardTape(machine, blocks, use128kMode))
                {
                    if (canStartNativeRom && TapLoader.HasCodeOverlappingBootstrapStack(blocks))
                    {
                        return new TapeLoadPlan(
                            TapeLoadStrategy.RomBootstrapMounted,
                            TapeLoadPlanReason.CodeLoadOverlapsBootstrapStack,
                            "Mounted CODE crosses the bootstrap stack; run the BASIC loader and its stack setup in the native ROM.");
                    }

                    if (!TapLoader.CanExecuteLeadingBasicProgramWithBootstrap(machine, blocks, use128kMode))
                    {
                        return new TapeLoadPlan(
                            TapeLoadStrategy.RomBootstrapMounted,
                            TapeLoadPlanReason.UnsupportedStandardBootstrapControlFlow,
                            "Standard tape requires mounted LOAD semantics, but its autorun BASIC control flow must execute under ROM control.");
                    }

                    return new TapeLoadPlan(
                        TapeLoadStrategy.BootstrapHybrid,
                        TapeLoadPlanReason.StandardTapeRequiresMountedSemantics,
                        "Standard tape contains a protected or chained loader stage that requires mounted LOAD semantics.");
                }

                if (TapLoader.HasTrailingCodeOverlappingBootstrapEntry(machine, blocks, use128kMode))
                {
                    return new TapeLoadPlan(
                        TapeLoadStrategy.RomBootstrapMounted,
                        TapeLoadPlanReason.TrailingCodeOverlapsBootstrapEntry,
                        "Preloading trailing CODE records would overwrite the BASIC loader's machine-code entry point.");
                }

                return new TapeLoadPlan(
                    TapeLoadStrategy.FullFakeLoad,
                    TapeLoadPlanReason.StandardTapeFullyAccelerated,
                    "All blocks are standard header/data pairs and can be fully fake-loaded.");
            }

            if (TapLoader.HasTwoStageBasicPrefixBeforeElectricallyDecodedProtectedStream(
                    blocks, out bool chainsFromNumberedAutorunToLineZero))
            {
                if (canStartNativeRom && chainsFromNumberedAutorunToLineZero)
                {
                    return new TapeLoadPlan(
                        TapeLoadStrategy.RomBootstrapMounted,
                        TapeLoadPlanReason.LineZeroContinuationRequiresNativeLoad,
                        "A numbered BASIC autorun chains into a line-zero protected loader that needs native ROM LOAD state.");
                }

                return new TapeLoadPlan(
                    TapeLoadStrategy.RomBootstrapMounted,
                    TapeLoadPlanReason.ProtectedContinuationRequiresRom,
                    "Tape has a two-stage BASIC prefix whose protected continuation depends on real ROM loader state.");
            }

            if (TapLoader.RequiresLeadingStandardBasicChainFakeLoad(machine, blocks))
            {
                return new TapeLoadPlan(
                    TapeLoadStrategy.LeadingStandardChainFakeLoad,
                    TapeLoadPlanReason.SafeLeadingBasicChain,
                    "Tape begins with a chain of standard BASIC stages before a protected remainder and can fake-load that safe prefix.");
            }

            if (TapLoader.RequiresRomDrivenBootstrapForMixedTape(machine, blocks))
            {
                return new TapeLoadPlan(
                    TapeLoadStrategy.RomBootstrapMounted,
                    TapeLoadPlanReason.MixedBasicChainRequiresRom,
                    "Tape begins with an autorun BASIC stage that chains into a mounted BASIC loader before protected continuation.");
            }

            if (TapLoader.RequiresMountedRealtimeForMixedTape(machine, blocks))
            {
                bool use128kMode = TapLoader.Use128kTapeLoadMode(machine);
                if (!TapLoader.CanExecuteLeadingBasicProgramWithBootstrap(machine, blocks, use128kMode))
                {
                    if (canStartNativeRom && TapLoader.HasHeaderlessStandardDataAfterHeaderedPrefix(blocks))
                    {
                        return new TapeLoadPlan(
                            TapeLoadStrategy.RomBootstrapMounted,
                            TapeLoadPlanReason.HeaderlessContinuationRequiresNativeLoad,
                            "Unsupported BASIC bootstrap precedes headerless ROM-speed data; keep the complete tape under native ROM control.");
                    }

                    return new TapeLoadPlan(
                        TapeLoadStrategy.RomBootstrapMounted,
                        TapeLoadPlanReason.UnsupportedMixedBootstrapControlFlow,
                        "Mixed tape requires mounted playback, but its autorun BASIC control flow must execute under ROM control.");
                }

                if (TapLoader.FindLeadingStandardBasicChainEndIndex(blocks) == 0 &&
                    !TapLoader.CanSatisfyLeadingBasicLoadRequestsBeforeProtectedRemainder(machine, blocks, use128kMode))
                {
                    return new TapeLoadPlan(
                        TapeLoadStrategy.RomBootstrapMounted,
                        TapeLoadPlanReason.UnsatisfiedLeadingLoadRequests,
                        "Tape loader requests more standard LOAD stages than the safe bootstrap can satisfy before protected playback, so autorun must remain under ROM control.");
                }

                if (TapLoader.HasStandardHeaderDataPairAfterLeadingBasic(blocks) &&
                    !TapLoader.CanResolveMountedLoadUsrContinuation(machine, blocks))
                {
                    return new TapeLoadPlan(
                        TapeLoadStrategy.RomBootstrapMounted,
                        TapeLoadPlanReason.UnrepresentableMountedContinuation,
                        "Tape requires mounted continuation that cannot be represented safely by the bootstrap executor, so autorun must remain under ROM control.");
                }

                return new TapeLoadPlan(
                    TapeLoadStrategy.BootstrapHybrid,
                    TapeLoadPlanReason.MixedTapeBootstrap,
                    "Tape begins with a BASIC stage followed by raw standard byte streams and should enter the mounted remainder through the bootstrap loader path.");
            }

            if (TapLoader.CanBootstrapBasicProgramAndMountRemaining(blocks))
            {
                bool use128kMode = TapLoader.Use128kTapeLoadMode(machine);
                if (TapeExecutionEngine.ContainsElectricallyDecodedProtectedStream(blocks) &&
                    TapLoader.LeadingBasicProgramHasMachineCodeHandoff(blocks))
                {
                    return new TapeLoadPlan(
                        TapeLoadStrategy.RomBootstrapMounted,
                        TapeLoadPlanReason.ProtectedBasicRequiresRom,
                        "Tape begins with a BASIC loader whose machine-code handoff into protected playback requires authentic ROM state.");
                }

                if (TapLoader.StartsProtectedRemainderImmediatelyAfterLeadingBasic(blocks) &&
                    !TapLoader.CanExecuteLeadingBasicProgramWithBootstrap(machine, blocks, use128kMode))
                {
                    return new TapeLoadPlan(
                        TapeLoadStrategy.RomBootstrapMounted,
                        TapeLoadPlanReason.ProtectedBasicRequiresRom,
                        "Tape begins with a protected BASIC loader whose interpreter handoff must execute under ROM control.");
                }

                if (TapLoader.FindLeadingStandardBasicChainEndIndex(blocks) == 0 &&
                    !TapLoader.CanSatisfyLeadingBasicLoadRequestsBeforeProtectedRemainder(machine, blocks, use128kMode))
                {
                    return new TapeLoadPlan(
                        TapeLoadStrategy.RomBootstrapMounted,
                        TapeLoadPlanReason.UnsatisfiedLeadingLoadRequests,
                        "Tape loader cannot complete its LOAD sequence through the safe bootstrap path, so autorun must remain under ROM control.");
                }

                if (use128kMode &&
                    TapLoader.HasStandardHeaderDataPairAfterLeadingBasic(blocks) &&
                    TapLoader.LeadingBasicProgramHasMachineCodeHandoff(blocks))
                {
                    return new TapeLoadPlan(
                        TapeLoadStrategy.RomBootstrapMounted,
                        TapeLoadPlanReason.MachineCodeHandoffRequiresRom,
                        "Tape loads standard data before a machine-code handoff whose 128K paging state must be established by the ROM interpreter.");
                }

                return new TapeLoadPlan(
                    TapeLoadStrategy.BootstrapHybrid,
                    TapeLoadPlanReason.BasicBootstrapMounted,
                    "Tape begins with a standard BASIC loader and requires mounted continuation.");
            }

            return new TapeLoadPlan(
                TapeLoadStrategy.MountedRealtime,
                TapeLoadPlanReason.NoSafeAccelerationPath,
                "Tape does not have a safe fake-load bootstrap path.");
        }
    }
}
