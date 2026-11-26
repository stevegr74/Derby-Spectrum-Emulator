using System.Text.Json.Serialization;

namespace Derby.Tape
{
    public sealed partial class MountedTape
    {
        public sealed class QuickState
        {
            [JsonInclude, JsonRequired] internal int NextBlockIndex;
            [JsonInclude, JsonRequired] internal int EarPlaybackBlockIndex;
            [JsonInclude, JsonRequired] internal int EarStreamByteIndex;
            [JsonInclude, JsonRequired] internal int EarBitIndex;
            [JsonInclude, JsonRequired] internal int EarPulseRepeatCount;
            [JsonInclude, JsonRequired] internal int EarPilotPulsesRemaining;
            [JsonInclude, JsonRequired] internal int EarPulseLengthTStates;
            [JsonInclude, JsonRequired] internal int EarPulseSequenceIndex;
            [JsonInclude, JsonRequired] internal int EarNextBlockIndexAfterPause;
            [JsonInclude, JsonRequired] internal int EarPauseLowTailTStates;
            [JsonInclude, JsonRequired] internal int PendingPrePlaybackPauseTStates;
            [JsonInclude, JsonRequired] internal int RomStreamTrapBlockIndex;
            [JsonInclude, JsonRequired] internal int RomStreamTrapByteIndex;
            [JsonInclude, JsonRequired] internal int CompletedRomLoadableBlockIndex;
            [JsonInclude, JsonRequired] internal ulong LastEarSampleTStates;
            [JsonInclude, JsonRequired] internal bool EarLevel;
            [JsonInclude, JsonRequired] internal bool EarPlaybackStarted;
            [JsonInclude, JsonRequired] internal bool RetainedByteStreamTrapAvailable;
            [JsonInclude, JsonRequired] internal bool PlaybackCompleted;
            [JsonInclude, JsonRequired] internal bool PlaybackPaused;
            [JsonInclude, JsonRequired] internal bool PausedByStopMarker;
            [JsonInclude, JsonRequired] internal ulong PausedPulseElapsedTStates;
            [JsonInclude, JsonRequired] internal int StopMarkerResumeBlockIndex;
            [JsonInclude, JsonRequired] internal int EarPlaybackStateValue;
            [JsonInclude, JsonRequired] internal int TapeStateValue;
            [JsonInclude, JsonRequired] internal int? ExpectedDataLength;
            [JsonInclude, JsonRequired] internal string? PendingHeaderName;
            [JsonInclude, JsonRequired] internal TapHeaderInfo? PendingHeaderInfo;
        }

        public QuickState CaptureQuickState()
        {
            return new QuickState
            {
                NextBlockIndex = nextBlockIndex,
                EarPlaybackBlockIndex = earPlaybackBlockIndex,
                EarStreamByteIndex = earStreamByteIndex,
                EarBitIndex = earBitIndex,
                EarPulseRepeatCount = earPulseRepeatCount,
                EarPilotPulsesRemaining = earPilotPulsesRemaining,
                EarPulseLengthTStates = earPulseLengthTStates,
                EarPulseSequenceIndex = earPulseSequenceIndex,
                EarNextBlockIndexAfterPause = earNextBlockIndexAfterPause,
                EarPauseLowTailTStates = earPauseLowTailTStates,
                PendingPrePlaybackPauseTStates = pendingPrePlaybackPauseTStates,
                RomStreamTrapBlockIndex = romStreamTrapBlockIndex,
                RomStreamTrapByteIndex = romStreamTrapByteIndex,
                CompletedRomLoadableBlockIndex = completedRomLoadableBlockIndex,
                LastEarSampleTStates = lastEarSampleTStates,
                EarLevel = earLevel,
                EarPlaybackStarted = earPlaybackStarted,
                RetainedByteStreamTrapAvailable = retainedByteStreamTrapAvailable,
                PlaybackCompleted = playbackCompleted,
                PlaybackPaused = playbackPaused,
                PausedByStopMarker = pausedByStopMarker,
                PausedPulseElapsedTStates = pausedPulseElapsedTStates,
                StopMarkerResumeBlockIndex = stopMarkerResumeBlockIndex,
                EarPlaybackStateValue = (int)earPlaybackState,
                TapeStateValue = (int)state,
                ExpectedDataLength = expectedDataLength,
                PendingHeaderName = pendingHeaderName,
                PendingHeaderInfo = pendingHeaderInfo
            };
        }

        public void RestoreQuickState(QuickState state)
        {
            ArgumentNullException.ThrowIfNull(state);
            nextBlockIndex = state.NextBlockIndex;
            earPlaybackBlockIndex = state.EarPlaybackBlockIndex;
            earStreamByteIndex = state.EarStreamByteIndex;
            earBitIndex = state.EarBitIndex;
            earPulseRepeatCount = state.EarPulseRepeatCount;
            earPilotPulsesRemaining = state.EarPilotPulsesRemaining;
            earPulseLengthTStates = state.EarPulseLengthTStates;
            earPulseSequenceIndex = state.EarPulseSequenceIndex;
            earNextBlockIndexAfterPause = state.EarNextBlockIndexAfterPause;
            earPauseLowTailTStates = state.EarPauseLowTailTStates;
            pendingPrePlaybackPauseTStates = state.PendingPrePlaybackPauseTStates;
            romStreamTrapBlockIndex = state.RomStreamTrapBlockIndex;
            romStreamTrapByteIndex = state.RomStreamTrapByteIndex;
            completedRomLoadableBlockIndex = state.CompletedRomLoadableBlockIndex;
            lastEarSampleTStates = state.LastEarSampleTStates;
            earLevel = state.EarLevel;
            earPlaybackStarted = state.EarPlaybackStarted;
            retainedByteStreamTrapAvailable = state.RetainedByteStreamTrapAvailable;
            playbackCompleted = state.PlaybackCompleted;
            playbackPaused = state.PlaybackPaused;
            pausedByStopMarker = state.PausedByStopMarker;
            pausedPulseElapsedTStates = state.PausedPulseElapsedTStates;
            stopMarkerResumeBlockIndex = state.StopMarkerResumeBlockIndex;
            earPlaybackState = (EarPlaybackState)state.EarPlaybackStateValue;
            this.state = (TapeState)state.TapeStateValue;
            expectedDataLength = state.ExpectedDataLength;
            pendingHeaderName = state.PendingHeaderName;
            pendingHeaderInfo = state.PendingHeaderInfo;
        }
    }
}
