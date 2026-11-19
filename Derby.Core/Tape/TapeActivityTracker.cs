namespace Derby.Tape
{
    internal sealed class TapeActivityTracker
    {
        internal sealed record QuickState(
            ulong PortReadCount,
            ulong PortReadCountAtFrameStart,
            ulong KeyboardOnlyPortReadCount,
            ulong KeyboardOnlyPortReadCountAtFrameStart,
            bool LoaderActivityObserved,
            int HandoffFrames,
            bool AutoPausedAtHandoff,
            bool AwaitingPostResumeHandoff,
            bool AwaitingProtectedLoaderRead,
            int AutomaticResumeReads);

        public ulong PortReadCount { get; set; }
        public ulong PortReadCountAtFrameStart { get; set; }
        public ulong KeyboardOnlyPortReadCount { get; set; }
        public ulong KeyboardOnlyPortReadCountAtFrameStart { get; set; }
        public bool LoaderActivityObserved { get; set; }
        public int HandoffFrames { get; set; }
        public bool AutoPausedAtHandoff { get; set; }
        public bool AwaitingPostResumeHandoff { get; set; }
        public bool AwaitingProtectedLoaderRead { get; set; }
        public int AutomaticResumeReads { get; set; }

        public void RecordPortRead(bool keyboardOnly)
        {
            PortReadCount++;
            if (keyboardOnly)
                KeyboardOnlyPortReadCount++;
        }

        public void ObserveLoaderActivity()
        {
            LoaderActivityObserved = true;
        }

        public bool TryAutomaticallyResume(
            MountedTape? tape,
            bool isRecognizedRomTapeRead,
            ulong sampleTStates,
            int requiredReadCount,
            bool isEarSensitiveRead = false)
        {
            if (tape == null ||
                !AutoPausedAtHandoff ||
                !tape.IsPlaybackPaused ||
                tape.IsPausedByStopMarker)
            {
                AutomaticResumeReads = 0;
                return false;
            }

            bool recognizedResumeRead = isRecognizedRomTapeRead ||
                                        (AwaitingProtectedLoaderRead && isEarSensitiveRead);
            if (!recognizedResumeRead)
            {
                AutomaticResumeReads = 0;
                return false;
            }

            AutomaticResumeReads++;
            if (AutomaticResumeReads < requiredReadCount)
                return false;

            bool resumingProtectedLoader = AwaitingProtectedLoaderRead;
            bool resumed = tape.ResumePlayback(sampleTStates);
            Reset();
            AwaitingPostResumeHandoff = resumed && !resumingProtectedLoader;
            return resumed;
        }

        public bool TryAutomaticallyStop(
            MountedTape? tape,
            bool cpuIsHalted,
            ushort programCounter,
            bool isRecognizedRomWait,
            bool hasPendingContinuation,
            ulong currentTStates,
            int activityReadsPerFrame,
            int requiredHandoffFrames)
        {
            ulong readsThisFrame = PortReadCount - PortReadCountAtFrameStart;
            PortReadCountAtFrameStart = PortReadCount;
            ulong keyboardOnlyReadsThisFrame =
                KeyboardOnlyPortReadCount - KeyboardOnlyPortReadCountAtFrameStart;
            KeyboardOnlyPortReadCountAtFrameStart = KeyboardOnlyPortReadCount;
            ulong earSensitiveReadsThisFrame = readsThisFrame >= keyboardOnlyReadsThisFrame
                ? readsThisFrame - keyboardOnlyReadsThisFrame
                : 0;

            if (tape == null || tape.IsPlaybackPaused || tape.HasCompletedPlayback)
            {
                LoaderActivityObserved = false;
                HandoffFrames = 0;
                return false;
            }

            if (tape.IsAtProtectedSynchronizationPreamble &&
                earSensitiveReadsThisFrame == 0)
            {
                bool held = tape.PausePlaybackAtCurrentBlockStart(currentTStates);
                if (held)
                {
                    AutoPausedAtHandoff = true;
                    AwaitingPostResumeHandoff = false;
                    AwaitingProtectedLoaderRead = true;
                    AutomaticResumeReads = 0;
                    HandoffFrames = 0;
                }

                return held;
            }

            if (earSensitiveReadsThisFrame > (ulong)activityReadsPerFrame)
            {
                LoaderActivityObserved = true;
                HandoffFrames = 0;
                return false;
            }

            bool sustainedKeyboardOnlyWait =
                keyboardOnlyReadsThisFrame > (ulong)activityReadsPerFrame &&
                earSensitiveReadsThisFrame <= (ulong)activityReadsPerFrame;
            bool lowRateHaltLoop =
                cpuIsHalted &&
                readsThisFrame <= (ulong)activityReadsPerFrame;
            bool postResumeRamHandoff =
                AwaitingPostResumeHandoff &&
                programCounter >= 0x4000 &&
                earSensitiveReadsThisFrame <= (ulong)activityReadsPerFrame;
            // A BASIC USR may never return when the game hands control to a
            // keyboard prompt. Hold the final record before its signal starts.
            bool finalRecordKeyboardWait =
                tape.IsInPrePlaybackPauseBeforeFinalRomBlock &&
                lowRateHaltLoop &&
                keyboardOnlyReadsThisFrame != 0 &&
                earSensitiveReadsThisFrame == 0 &&
                programCounter >= 0x4000;
            bool loaderHasRelinquishedTape =
                ((LoaderActivityObserved &&
                  (sustainedKeyboardOnlyWait || lowRateHaltLoop) &&
                  (programCounter >= 0x4000 || isRecognizedRomWait)) ||
                 postResumeRamHandoff) &&
                tape.IsActivelyDrivingEarLine &&
                (!hasPendingContinuation || finalRecordKeyboardWait);
            if (!loaderHasRelinquishedTape)
            {
                // Game loops often interleave keyboard polling with several frames of
                // music or animation. Preserve positive keyboard-wait evidence across
                // frames with no tape reads, but cancel it as soon as EAR is sampled.
                if (earSensitiveReadsThisFrame != 0 && !AwaitingPostResumeHandoff)
                    HandoffFrames = 0;
                return false;
            }

            HandoffFrames++;
            int effectiveRequiredFrames = isRecognizedRomWait ? 1 : requiredHandoffFrames;
            if (HandoffFrames < effectiveRequiredFrames)
                return false;

            bool rewindUnclaimedRecord = isRecognizedRomWait || AwaitingPostResumeHandoff;
            bool stopped = rewindUnclaimedRecord
                ? tape.PausePlaybackAtCurrentBlockStart(currentTStates)
                : tape.PausePlayback(currentTStates);
            if (stopped)
            {
                AutoPausedAtHandoff = true;
                AwaitingPostResumeHandoff = false;
                AutomaticResumeReads = 0;
            }

            HandoffFrames = 0;
            return stopped;
        }

        public void Reset()
        {
            PortReadCount = 0;
            PortReadCountAtFrameStart = 0;
            KeyboardOnlyPortReadCount = 0;
            KeyboardOnlyPortReadCountAtFrameStart = 0;
            LoaderActivityObserved = false;
            HandoffFrames = 0;
            AutoPausedAtHandoff = false;
            AwaitingPostResumeHandoff = false;
            AwaitingProtectedLoaderRead = false;
            AutomaticResumeReads = 0;
        }

        public QuickState CaptureQuickState() => new QuickState(
            PortReadCount,
            PortReadCountAtFrameStart,
            KeyboardOnlyPortReadCount,
            KeyboardOnlyPortReadCountAtFrameStart,
            LoaderActivityObserved,
            HandoffFrames,
            AutoPausedAtHandoff,
            AwaitingPostResumeHandoff,
            AwaitingProtectedLoaderRead,
            AutomaticResumeReads);

        public void RestoreQuickState(QuickState state)
        {
            PortReadCount = state.PortReadCount;
            PortReadCountAtFrameStart = state.PortReadCountAtFrameStart;
            KeyboardOnlyPortReadCount = state.KeyboardOnlyPortReadCount;
            KeyboardOnlyPortReadCountAtFrameStart = state.KeyboardOnlyPortReadCountAtFrameStart;
            LoaderActivityObserved = state.LoaderActivityObserved;
            HandoffFrames = state.HandoffFrames;
            AutoPausedAtHandoff = state.AutoPausedAtHandoff;
            AwaitingPostResumeHandoff = state.AwaitingPostResumeHandoff;
            AwaitingProtectedLoaderRead = state.AwaitingProtectedLoaderRead;
            AutomaticResumeReads = state.AutomaticResumeReads;
        }
    }
}
