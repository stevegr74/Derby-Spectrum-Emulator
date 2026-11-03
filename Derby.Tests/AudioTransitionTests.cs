using System;
using System.IO;
using Derby.Audio;
using Xunit;

namespace Derby.Tests
{
    public sealed class AudioTransitionTests
    {
        [Fact]
        public void ReenableCaptureMidFrame_DiscardsIncompleteFrameAndResumesAtNextBoundary()
        {
            string romFolder = CreateTempRoms();
            try
            {
                var machine = new SpectrumMachine(romFolder);
                machine.SetAudioFrameCaptureEnabled(false);

                machine.ExecuteTimeSlice(machine.FrameTStates / 2);
                machine.SetAudioFrameCaptureEnabled(true);
                machine.ExecuteTimeSlice(machine.FrameTStates / 2);

                Assert.False(machine.TryDequeueCompletedAudioFrame(out _));

                machine.ExecuteTimeSlice(machine.FrameTStates);

                Assert.True(machine.TryDequeueCompletedAudioFrame(out AudioFrame frame));
                Assert.NotNull(frame.InitialAyState);
            }
            finally
            {
                Directory.Delete(romFolder, true);
            }
        }

        [Fact]
        public void QuickState_PreservesInvalidTransitionFrameUntilNextCleanBoundary()
        {
            string romFolder = CreateTempRoms();
            try
            {
                var machine = new SpectrumMachine(romFolder);
                machine.SetAudioFrameCaptureEnabled(false);
                machine.ExecuteTimeSlice(machine.FrameTStates / 2);
                machine.SetAudioFrameCaptureEnabled(true);
                SpectrumMachine.QuickState state = machine.CaptureQuickState();

                machine.ExecuteTimeSlice(machine.FrameTStates / 2);
                Assert.False(machine.TryDequeueCompletedAudioFrame(out _));

                machine.RestoreQuickState(state);
                machine.ExecuteTimeSlice(machine.FrameTStates / 2);
                Assert.False(machine.TryDequeueCompletedAudioFrame(out _));

                machine.ExecuteTimeSlice(machine.FrameTStates);
                Assert.True(machine.TryDequeueCompletedAudioFrame(out _));
            }
            finally
            {
                Directory.Delete(romFolder, true);
            }
        }

        [Theory]
        [InlineData(SpectrumMachine.FrameTStates48, SpectrumMachine.CpuClockHz48)]
        [InlineData(SpectrumMachine.FrameTStates128, SpectrumMachine.CpuClockHz128)]
        public void SampleClock_HasNoCumulativeDriftAcrossTenMinutes(int frameTStates, int cpuClockHz)
        {
            const uint sampleRate = 44100;
            const int frameCount = 30000;
            var clock = new TStateSampleClock(sampleRate);
            long actualSamples = 0;

            for (int frame = 0; frame < frameCount; frame++)
                actualSamples += clock.Consume(frameTStates, cpuClockHz);

            long expectedSamples = (long)frameTStates * frameCount * sampleRate / cpuClockHz;
            Assert.Equal(expectedSamples, actualSamples);
        }

        private static string CreateTempRoms()
        {
            string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "128-0.rom"), new byte[16384]);
            File.WriteAllBytes(Path.Combine(folder, "128-1.rom"), new byte[16384]);
            return folder;
        }
    }
}
