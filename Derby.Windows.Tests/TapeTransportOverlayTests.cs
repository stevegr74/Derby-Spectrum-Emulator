using System.Diagnostics;
using Derby.Tape;
using Xunit;

namespace Derby.Tests
{
    public class TapeTransportOverlayTests
    {
        [Fact]
        public void Observe_ReportsOnlyTransportChanges()
        {
            var overlay = new TapeTransportOverlay();

            Assert.False(overlay.Observe(TapeTransportState.NoTape, markerStop: false, nowTicks: 100));
            Assert.True(overlay.Observe(TapeTransportState.Playing, markerStop: false, nowTicks: 100));
            Assert.False(overlay.Observe(TapeTransportState.Playing, markerStop: false, nowTicks: 200));
            Assert.True(overlay.Observe(TapeTransportState.Stopped, markerStop: true, nowTicks: 200));
        }

        [Fact]
        public void StoppedFeedback_ChangesFromFullToIconThenExpires()
        {
            var overlay = new TapeTransportOverlay();
            long start = 100;
            overlay.Show(TapeTransportState.Stopped, markerStop: false, start);

            Assert.True(overlay.IsFullOverlayVisible(start));
            Assert.False(overlay.IsFullOverlayVisible(start + (4 * Stopwatch.Frequency)));
            Assert.True(overlay.IsVisible(start + (4 * Stopwatch.Frequency)));
            Assert.False(overlay.IsVisible(start + (6 * Stopwatch.Frequency)));
        }

        [Fact]
        public void PlayingIcon_RemainsVisibleAfterFullFeedbackExpires()
        {
            var overlay = new TapeTransportOverlay();
            long start = 100;
            overlay.Show(TapeTransportState.Playing, markerStop: false, start);

            Assert.False(overlay.IsFullOverlayVisible(start + (4 * Stopwatch.Frequency)));
            Assert.True(overlay.IsVisible(start + (60 * Stopwatch.Frequency)));
        }

        [Fact]
        public void EndedFeedback_ClearsAfterItsDisplayWindow()
        {
            var overlay = new TapeTransportOverlay();
            long start = 100;
            overlay.Observe(TapeTransportState.Ended, markerStop: false, start);

            overlay.ExpireEndedFeedback(start + (4 * Stopwatch.Frequency));

            Assert.Equal(TapeTransportState.Ended, overlay.ObservedState);
            Assert.Equal(TapeTransportState.NoTape, overlay.FeedbackState);
            Assert.False(overlay.IsVisible(start + (4 * Stopwatch.Frequency)));
        }
    }
}
