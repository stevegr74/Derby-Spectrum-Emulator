using Derby.Tape;
using Xunit;

namespace Derby.Tests
{
    public sealed class TapeTurboPlaybackPolicyTests
    {
        [Fact]
        public void AllowsTurboForSilentStreamingTape()
        {
            var policy = new TapeTurboPlaybackPolicy();

            Assert.True(policy.IsTurboPlaybackAllowed(isStreamingEarSignal: true, hasAudibleOutput: false));
        }

        [Fact]
        public void AudibleStreamingStageDisablesTurboForTheRestOfTheTapeSession()
        {
            var policy = new TapeTurboPlaybackPolicy();

            Assert.True(policy.IsTurboPlaybackAllowed(isStreamingEarSignal: true, hasAudibleOutput: false));
            Assert.False(policy.IsTurboPlaybackAllowed(isStreamingEarSignal: true, hasAudibleOutput: true));

            // Audio generators may be silent on individual scheduler ticks.
            Assert.False(policy.IsTurboPlaybackAllowed(isStreamingEarSignal: true, hasAudibleOutput: false));
        }

        [Fact]
        public void ResetAllowsTurboForANewTapeSession()
        {
            var policy = new TapeTurboPlaybackPolicy();
            policy.IsTurboPlaybackAllowed(isStreamingEarSignal: true, hasAudibleOutput: true);

            policy.Reset();

            Assert.True(policy.IsTurboPlaybackAllowed(isStreamingEarSignal: true, hasAudibleOutput: false));
        }

        [Fact]
        public void AudibleOutputOutsideAnEarStreamDoesNotDisableFutureTurbo()
        {
            var policy = new TapeTurboPlaybackPolicy();

            Assert.False(policy.IsTurboPlaybackAllowed(isStreamingEarSignal: false, hasAudibleOutput: true));
            Assert.True(policy.IsTurboPlaybackAllowed(isStreamingEarSignal: true, hasAudibleOutput: false));
        }
    }
}
