using Derby.Tape;
using Xunit;

namespace Derby.Tests
{
    public class TapeActivityTrackerTests
    {
        [Fact]
        public void AutomaticHandoff_StopThenRecognizedReads_ResumesAtExactCursor()
        {
            var tracker = new TapeActivityTracker();
            MountedTape tape = CreateDrivingTape();
            tracker.ObserveLoaderActivity();

            Assert.False(TryStop(tracker, tape));
            Assert.False(TryStop(tracker, tape));
            Assert.True(TryStop(tracker, tape));
            Assert.True(tape.IsPlaybackPaused);
            Assert.True(tracker.AutoPausedAtHandoff);

            for (int read = 1; read < 64; read++)
                Assert.False(tracker.TryAutomaticallyResume(tape, true, 500, 64));

            Assert.True(tracker.TryAutomaticallyResume(tape, true, 500, 64));
            Assert.False(tape.IsPlaybackPaused);
            Assert.False(tracker.AutoPausedAtHandoff);
            Assert.True(tracker.AwaitingPostResumeHandoff);
            Assert.False(tape.ReadEarBit(599));
            Assert.True(tape.ReadEarBit(600));
        }

        [Fact]
        public void AutomaticResume_DoesNotCrossExplicitStopMarker()
        {
            var tracker = new TapeActivityTracker
            {
                AutoPausedAtHandoff = true
            };
            var tape = new MountedTape(
                "stop-marker.tzx",
                new[]
                {
                    TapeBlock.CreatePureTone(pulseLength: 10, pulseCount: 1),
                    TapeBlock.CreateStopTape(),
                    TapeBlock.CreatePureTone(pulseLength: 20, pulseCount: 1)
                });

            tape.ReadEarBit(0);
            tape.ReadEarBit(10);
            Assert.True(tape.IsPausedByStopMarker);

            for (int read = 0; read < 128; read++)
                Assert.False(tracker.TryAutomaticallyResume(tape, true, 500, 64));

            Assert.True(tape.IsPlaybackPaused);
            Assert.True(tape.IsPausedByStopMarker);
        }

        [Fact]
        public void AutomaticResume_StopsAgainAfterStableRamHandoff()
        {
            var tracker = new TapeActivityTracker();
            MountedTape tape = CreateDrivingTape();
            tracker.ObserveLoaderActivity();

            Assert.False(TryStop(tracker, tape));
            Assert.False(TryStop(tracker, tape));
            Assert.True(TryStop(tracker, tape));
            for (int read = 0; read < 64; read++)
                tracker.TryAutomaticallyResume(tape, true, 500, 64);

            Assert.True(tracker.AwaitingPostResumeHandoff);
            Assert.False(RecordActiveFrame(tracker, tape));
            Assert.False(RecordActiveFrame(tracker, tape));
            Assert.True(RecordActiveFrame(tracker, tape));
            Assert.True(tape.IsPlaybackPaused);
            Assert.False(tracker.AwaitingPostResumeHandoff);
        }

        [Fact]
        public void AutomaticResume_DoesNotStopWhileEarActivityContinues()
        {
            var tracker = new TapeActivityTracker();
            MountedTape tape = CreateDrivingTape();
            tracker.ObserveLoaderActivity();

            Assert.False(TryStop(tracker, tape));
            Assert.False(TryStop(tracker, tape));
            Assert.True(TryStop(tracker, tape));
            for (int read = 0; read < 64; read++)
                tracker.TryAutomaticallyResume(tape, true, 500, 64);

            for (int frame = 0; frame < 4; frame++)
            {
                for (int read = 0; read < 65; read++)
                    tracker.RecordPortRead(keyboardOnly: false);
                Assert.False(RecordActiveFrame(tracker, tape));
            }

            Assert.False(tape.IsPlaybackPaused);
            Assert.True(tracker.AwaitingPostResumeHandoff);
        }

        [Fact]
        public void ProtectedSynchronization_WaitsForEarSensitiveLoaderReads()
        {
            var tracker = new TapeActivityTracker();
            MountedTape tape = CreateProtectedSynchronizationTape();

            for (int read = 0; read < 65; read++)
                tracker.RecordPortRead(keyboardOnly: true);

            Assert.True(RecordActiveFrame(tracker, tape));
            Assert.True(tape.IsPlaybackPaused);
            Assert.True(tracker.AwaitingProtectedLoaderRead);

            Assert.False(tracker.TryAutomaticallyResume(
                tape,
                isRecognizedRomTapeRead: false,
                sampleTStates: 500,
                requiredReadCount: 64,
                isEarSensitiveRead: false));

            for (int read = 1; read < 64; read++)
            {
                Assert.False(tracker.TryAutomaticallyResume(
                    tape,
                    isRecognizedRomTapeRead: false,
                    sampleTStates: 500,
                    requiredReadCount: 64,
                    isEarSensitiveRead: true));
            }

            Assert.True(tracker.TryAutomaticallyResume(
                tape,
                isRecognizedRomTapeRead: false,
                sampleTStates: 500,
                requiredReadCount: 64,
                isEarSensitiveRead: true));
            Assert.False(tape.IsPlaybackPaused);
            Assert.False(tracker.AwaitingProtectedLoaderRead);
        }

        [Fact]
        public void ProtectedSynchronization_DoesNotPauseAfterLoaderStartsReadingEar()
        {
            var tracker = new TapeActivityTracker();
            MountedTape tape = CreateProtectedSynchronizationTape();

            tracker.RecordPortRead(keyboardOnly: false);

            Assert.False(RecordActiveFrame(tracker, tape));
            Assert.False(tape.IsPlaybackPaused);
            Assert.False(tracker.AwaitingProtectedLoaderRead);
        }

        [Fact]
        public void OrdinaryTone_DoesNotTriggerProtectedSynchronizationPause()
        {
            var tracker = new TapeActivityTracker();
            MountedTape tape = CreateDrivingTape();

            Assert.False(RecordActiveFrame(tracker, tape));
            Assert.False(tape.IsPlaybackPaused);
            Assert.False(tracker.AwaitingProtectedLoaderRead);
        }

        [Theory]
        [InlineData(false, true, 0x8000, false)]
        [InlineData(true, false, 0x8000, false)]
        [InlineData(true, true, 0x3FFF, false)]
        [InlineData(true, true, 0x8000, true)]
        public void AutomaticHandoff_StopRequiresEverySafetyGate(
            bool loaderActivityObserved,
            bool cpuIsHalted,
            int programCounter,
            bool hasPendingContinuation)
        {
            var tracker = new TapeActivityTracker();
            MountedTape tape = CreateDrivingTape();
            if (loaderActivityObserved)
                tracker.ObserveLoaderActivity();

            for (int frame = 0; frame < 4; frame++)
            {
                Assert.False(tracker.TryAutomaticallyStop(
                    tape,
                    cpuIsHalted,
                    (ushort)programCounter,
                    isRecognizedRomWait: false,
                    hasPendingContinuation,
                    100,
                    activityReadsPerFrame: 64,
                    requiredHandoffFrames: 3));
            }

            Assert.False(tape.IsPlaybackPaused);
        }

        [Fact]
        public void AutomaticHandoff_RecognizedRomWaitMayStopBelowRam()
        {
            var tracker = new TapeActivityTracker();
            MountedTape tape = CreateDrivingTape();
            tracker.ObserveLoaderActivity();

            Assert.True(TryStop(tracker, tape, programCounter: 0x1F3E, isRecognizedRomWait: true));

            Assert.True(tape.IsPlaybackPaused);
        }

        [Fact]
        public void QuickState_RestoresAllActivityAndHandoffCounters()
        {
            var tracker = new TapeActivityTracker();
            tracker.RecordPortRead(keyboardOnly: false);
            tracker.RecordPortRead(keyboardOnly: true);
            tracker.ObserveLoaderActivity();
            tracker.AwaitingPostResumeHandoff = true;
            tracker.AwaitingProtectedLoaderRead = true;
            TapeActivityTracker.QuickState state = tracker.CaptureQuickState();

            tracker.Reset();
            tracker.RestoreQuickState(state);

            Assert.Equal((ulong)2, tracker.PortReadCount);
            Assert.Equal((ulong)1, tracker.KeyboardOnlyPortReadCount);
            Assert.True(tracker.LoaderActivityObserved);
            Assert.True(tracker.AwaitingPostResumeHandoff);
            Assert.True(tracker.AwaitingProtectedLoaderRead);
        }

        [Fact]
        public void AutomaticHandoff_KeyboardEvidenceMaySpanIdleFrames()
        {
            var tracker = new TapeActivityTracker();
            MountedTape tape = CreateDrivingTape();
            tracker.ObserveLoaderActivity();

            Assert.False(RecordKeyboardWaitFrame(tracker, tape));
            Assert.False(RecordIdleFrame(tracker, tape));
            Assert.False(RecordKeyboardWaitFrame(tracker, tape));
            Assert.False(RecordIdleFrame(tracker, tape));
            Assert.True(RecordKeyboardWaitFrame(tracker, tape));

            Assert.True(tape.IsPlaybackPaused);
        }

        [Fact]
        public void AutomaticHandoff_IdleFramesBeforeKeyboardEvidenceDoNotCauseEarlyStop()
        {
            var tracker = new TapeActivityTracker();
            MountedTape tape = CreateDrivingTape();
            tracker.ObserveLoaderActivity();

            Assert.False(RecordIdleFrame(tracker, tape));
            Assert.False(RecordIdleFrame(tracker, tape));
            Assert.False(RecordIdleFrame(tracker, tape));
            Assert.False(RecordKeyboardWaitFrame(tracker, tape));
            Assert.False(RecordKeyboardWaitFrame(tracker, tape));
            Assert.False(tape.IsPlaybackPaused);

            Assert.True(RecordKeyboardWaitFrame(tracker, tape));
            Assert.True(tape.IsPlaybackPaused);
        }

        [Fact]
        public void AutomaticHandoff_EarReadCancelsAccumulatedKeyboardEvidence()
        {
            var tracker = new TapeActivityTracker();
            MountedTape tape = CreateDrivingTape();
            tracker.ObserveLoaderActivity();

            Assert.False(RecordKeyboardWaitFrame(tracker, tape));
            Assert.False(RecordKeyboardWaitFrame(tracker, tape));
            tracker.RecordPortRead(keyboardOnly: false);
            Assert.False(RecordActiveFrame(tracker, tape));
            Assert.False(RecordKeyboardWaitFrame(tracker, tape));
            Assert.False(RecordKeyboardWaitFrame(tracker, tape));
            Assert.True(RecordKeyboardWaitFrame(tracker, tape));
        }

        private static bool TryStop(
            TapeActivityTracker tracker,
            MountedTape tape,
            ushort programCounter = 0x8000,
            bool isRecognizedRomWait = false) =>
            tracker.TryAutomaticallyStop(
                tape,
                cpuIsHalted: true,
                programCounter,
                isRecognizedRomWait,
                hasPendingContinuation: false,
                currentTStates: 100,
                activityReadsPerFrame: 64,
                requiredHandoffFrames: 3);

        private static bool RecordKeyboardWaitFrame(TapeActivityTracker tracker, MountedTape tape)
        {
            for (int read = 0; read < 65; read++)
                tracker.RecordPortRead(keyboardOnly: true);
            return RecordActiveFrame(tracker, tape);
        }

        private static bool RecordIdleFrame(TapeActivityTracker tracker, MountedTape tape) =>
            RecordActiveFrame(tracker, tape);

        private static bool RecordActiveFrame(TapeActivityTracker tracker, MountedTape tape) =>
            tracker.TryAutomaticallyStop(
                tape,
                cpuIsHalted: false,
                programCounter: 0x8000,
                isRecognizedRomWait: false,
                hasPendingContinuation: false,
                currentTStates: 100,
                activityReadsPerFrame: 64,
                requiredHandoffFrames: 3);

        private static MountedTape CreateDrivingTape()
        {
            var tape = new MountedTape(
                "policy.tap",
                new[] { TapeBlock.CreatePureTone(pulseLength: 100, pulseCount: 4) });
            tape.ReadEarBit(0);
            return tape;
        }

        private static MountedTape CreateProtectedSynchronizationTape()
        {
            var tape = new MountedTape(
                "protected.tzx",
                new TapeBlock[]
                {
                    TapeBlock.CreatePureTone(pulseLength: 100, pulseCount: 40),
                    TapeBlock.CreatePulseSequence(new[] { 20, 40, 20 }),
                    TapeBlock.CreateByteStreamData(
                        new byte[] { 0xA0 },
                        zeroBitPulseLength: 10,
                        oneBitPulseLength: 20,
                        usedBitsInLastByte: 4,
                        pauseAfterBlockMs: 0)
                },
                initialEarLevelHigh: false);
            tape.ReadEarBit(0);
            return tape;
        }
    }
}
