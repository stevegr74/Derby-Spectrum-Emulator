using System.Collections.Generic;

namespace Derby.Tape
{
    internal static class TapeExecutionEngine
    {
        private const int ProtectedLiveTapeTimingDivisor = 8;
        private const int ProtectedLiveChainTapeTimingDivisor = 64;
        private const int PreloadedLoadableReplayTimingDivisor = 1;

        internal static TapeExecutionResult Execute(
            SpectrumMachine machine,
            string displayName,
            IReadOnlyList<TapeBlock> blocks,
            TapeLoadPlan plan,
            bool initialEarLevelHigh = true)
        {
            int nonRomTimingDivisor = GetProtectedLiveTapeTimingDivisor(plan.Strategy, blocks);
            int loadableTimingDivisor = GetPreloadedLoadableReplayTimingDivisor(plan.Strategy);
            switch (plan.Strategy)
            {
                case TapeLoadStrategy.FullFakeLoad:
                {
                    TapBootstrapResult result = TapLoader.LoadAllStandardTapeBlocksAndAutoStart(
                        machine,
                        displayName,
                        blocks,
                        skipCustomHeaderForEarPlayback: false,
                        remountPlaybackRemainder: TapLoader.FindFirstCustomHeaderBlockIndex(blocks) >= 0,
                        stopBeforeFirstCustomHeader: false,
                        loadableTimingDivisor: loadableTimingDivisor,
                        initialEarLevelHigh: initialEarLevelHigh);
                    return CreateResult(plan, result);
                }

                case TapeLoadStrategy.LeadingStandardChainFakeLoad:
                {
                    TapBootstrapResult result = TapLoader.LoadLeadingStandardBasicChainAndMountRemaining(
                        machine,
                        displayName,
                        blocks,
                        skipCustomHeaderForEarPlayback: false,
                        nonRomTimingDivisor: nonRomTimingDivisor,
                        loadableTimingDivisor: loadableTimingDivisor,
                        initialEarLevelHigh: initialEarLevelHigh);
                    return CreateResult(plan, result);
                }

                case TapeLoadStrategy.RomBootstrapMounted:
                {
                    bool requiresNativeRomLoader =
                        plan.ReasonCode is TapeLoadPlanReason.InconsistentStandardBasicPair or
                            TapeLoadPlanReason.NonCanonicalBasicProgram;
                    bool requiresNative128Basic =
                        machine.MachineModel == SpectrumMachineModel.Spectrum128K &&
                        !ReferenceEquals(machine.RomProfile, SpectrumRomProfile.SyntheticTest) &&
                        (plan.ReasonCode == TapeLoadPlanReason.UnsupportedStandardBootstrapControlFlow ||
                         (plan.ReasonCode == TapeLoadPlanReason.UnsupportedMixedBootstrapControlFlow &&
                          TapLoader.Requires128kTapeLoadModeForStandardTape(machine, blocks)));
                    TapBootstrapResult result = requiresNativeRomLoader
                        ? TapLoader.StartNativeRomTapeLoader(
                            machine,
                            displayName,
                            blocks,
                            nonRomTimingDivisor,
                            loadableTimingDivisor,
                            initialEarLevelHigh)
                        : requiresNative128Basic
                        ? TapLoader.StartNative128kRomTapeLoader(
                            machine,
                            displayName,
                            blocks,
                            nonRomTimingDivisor,
                            loadableTimingDivisor,
                            initialEarLevelHigh)
                        : TapLoader.LoadLeadingBasicProgramAndMountRemainingForRomAutoStart(
                            machine,
                            displayName,
                            blocks,
                            skipCustomHeaderForEarPlayback: false,
                            nonRomTimingDivisor: nonRomTimingDivisor,
                            loadableTimingDivisor: loadableTimingDivisor,
                            initialEarLevelHigh: initialEarLevelHigh,
                            allowRomLoadTraps: plan.ReasonCode != TapeLoadPlanReason.UnrepresentableMountedContinuation);
                    return CreateResult(plan, result);
                }

                case TapeLoadStrategy.BootstrapHybrid:
                {
                    TapBootstrapResult result = TapLoader.BootstrapTapeBlocksAndMountRemaining(
                        machine,
                        displayName,
                        blocks,
                        skipCustomHeaderForEarPlayback: false,
                        nonRomTimingDivisor: nonRomTimingDivisor,
                        loadableTimingDivisor: loadableTimingDivisor,
                        initialEarLevelHigh: initialEarLevelHigh);
                    return CreateResult(plan, result);
                }

                default:
                {
                    bool use128kMode = TapLoader.Use128kTapeLoadMode(machine);
                    TapLoader.InitializeMachineForFakeTapeLoad(machine, use128kMode);
                    var tape = new MountedTape(
                        displayName,
                        blocks,
                        nonRomTimingDivisor: nonRomTimingDivisor,
                        loadableTimingDivisor: loadableTimingDivisor,
                        initialEarLevelHigh: initialEarLevelHigh);
                    machine.MountTape(tape);
                    TapLoader.LogMountedTape(tape, blocks);
                    return new TapeExecutionResult(
                        TapeLoadStrategy.MountedRealtime,
                        plan.ReasonCode,
                        plan.Reason,
                        blocks.Count,
                        0,
                        displayName,
                        null);
                }
            }
        }

        internal static int GetProtectedLiveTapeTimingDivisor(
            TapeLoadStrategy strategy,
            IReadOnlyList<TapeBlock> blocks)
        {
            if (strategy == TapeLoadStrategy.FullFakeLoad || blocks.Count == 0)
                return 1;

            if (ContainsElectricallyDecodedProtectedStream(blocks))
                return 1;

            int protectedTimingDivisor = strategy == TapeLoadStrategy.LeadingStandardChainFakeLoad
                ? ProtectedLiveChainTapeTimingDivisor
                : ProtectedLiveTapeTimingDivisor;

            foreach (TapeBlock block in blocks)
            {
                if (!block.CanUseRomLoadTrap && block.Kind != TapeBlockKind.Metadata)
                    return protectedTimingDivisor;
            }

            return 1;
        }

        private static TapeExecutionResult CreateResult(TapeLoadPlan plan, TapBootstrapResult result)
        {
            return new TapeExecutionResult(
                plan.Strategy,
                plan.ReasonCode,
                plan.Reason,
                result.TotalBlockCount,
                result.ConsumedBlockCount,
                result.DisplayName,
                result.AutoStartFileName);
        }

        private static int GetPreloadedLoadableReplayTimingDivisor(TapeLoadStrategy strategy)
        {
            return strategy switch
            {
                TapeLoadStrategy.FullFakeLoad => PreloadedLoadableReplayTimingDivisor,
                TapeLoadStrategy.LeadingStandardChainFakeLoad => PreloadedLoadableReplayTimingDivisor,
                _ => 1
            };
        }

        internal static bool ContainsElectricallyDecodedProtectedStream(IReadOnlyList<TapeBlock> blocks)
        {
            foreach (TapeBlock block in blocks)
            {
                if (block.CanUseRomLoadTrap)
                    continue;

                if (block.Kind == TapeBlockKind.Data || block.Kind == TapeBlockKind.DirectRecording)
                    return true;
            }

            return false;
        }
    }
}
