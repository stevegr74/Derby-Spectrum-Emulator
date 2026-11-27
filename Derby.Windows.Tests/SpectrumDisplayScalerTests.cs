using System.Drawing;
using Derby.Tape;
using Xunit;

namespace Derby.Tests
{
    public class SpectrumDisplayScalerTests
    {
        [Fact]
        public void NativeScale_CopiesPixelsWithoutModification()
        {
            uint[] source = { 1, 2, 3, 4 };
            uint[] destination = new uint[4];

            SpectrumDisplayScaler.Scale(source, 2, 2, destination, 2, scale: 1);

            Assert.Equal(source, destination);
        }

        [Fact]
        public void Scale2x_SmoothsSupportedDiagonalEdge()
        {
            uint[] source =
            {
                0, 1, 0,
                1, 2, 0,
                0, 0, 0
            };
            uint[] destination = new uint[6 * 6];

            SpectrumDisplayScaler.Scale(source, 3, 3, destination, 6, scale: 2);

            Assert.Equal(1u, destination[(2 * 6) + 2]);
            Assert.Equal(2u, destination[(2 * 6) + 3]);
            Assert.Equal(2u, destination[(3 * 6) + 2]);
            Assert.Equal(0u, destination[(3 * 6) + 3]);
        }

        [Fact]
        public void Scale3x_SmoothsSupportedDiagonalEdge()
        {
            uint[] source =
            {
                0, 1, 0,
                1, 2, 0,
                0, 0, 0
            };
            uint[] destination = new uint[9 * 9];

            SpectrumDisplayScaler.Scale(source, 3, 3, destination, 9, scale: 3);

            Assert.Equal(1u, destination[(3 * 9) + 3]);
            Assert.Equal(1u, destination[(3 * 9) + 4]);
            Assert.Equal(2u, destination[(3 * 9) + 5]);
            Assert.Equal(1u, destination[(4 * 9) + 3]);
            Assert.Equal(2u, destination[(4 * 9) + 4]);
            Assert.Equal(0u, destination[(4 * 9) + 5]);
            Assert.Equal(2u, destination[(5 * 9) + 3]);
            Assert.Equal(0u, destination[(5 * 9) + 4]);
            Assert.Equal(0u, destination[(5 * 9) + 5]);
        }

        [Fact]
        public void DisplayModes_UseExactAspectPreservingLayouts()
        {
            SpectrumDisplayLayout native = SpectrumDisplayModes.GetLayout(SpectrumDisplayMode.Native);
            SpectrumDisplayLayout enhanced2x = SpectrumDisplayModes.GetLayout(SpectrumDisplayMode.Enhanced2x);
            SpectrumDisplayLayout enhanced3x = SpectrumDisplayModes.GetLayout(SpectrumDisplayMode.Enhanced3x);

            Assert.Equal((320, 240, 1, 0, 0), (native.ClientWidth, native.ClientHeight, native.Scale, native.ViewportX, native.ViewportY));
            Assert.Equal((640, 480, 2, 0, 0), (enhanced2x.ClientWidth, enhanced2x.ClientHeight, enhanced2x.Scale, enhanced2x.ViewportX, enhanced2x.ViewportY));
            Assert.Equal((960, 720, 3, 0, 0), (enhanced3x.ClientWidth, enhanced3x.ClientHeight, enhanced3x.Scale, enhanced3x.ViewportX, enhanced3x.ViewportY));
        }

        [Fact]
        public void DisplayModes_CycleInPresentationOrder()
        {
            Assert.Equal(SpectrumDisplayMode.Enhanced2x, SpectrumDisplayModes.Next(SpectrumDisplayMode.Native));
            Assert.Equal(SpectrumDisplayMode.Enhanced3x, SpectrumDisplayModes.Next(SpectrumDisplayMode.Enhanced2x));
            Assert.Equal(SpectrumDisplayMode.Native, SpectrumDisplayModes.Next(SpectrumDisplayMode.Enhanced3x));
        }

        [Theory]
        [InlineData(SpectrumDisplayMode.Native, 199, 6, 115, 50)]
        [InlineData(SpectrumDisplayMode.Enhanced2x, 462, 8, 170, 70)]
        [InlineData(SpectrumDisplayMode.Enhanced3x, 755, 10, 195, 80)]
        public void StatusOverlay_IsAnchoredToTopRight(
            SpectrumDisplayMode mode,
            int expectedX,
            int expectedY,
            int expectedWidth,
            int expectedHeight)
        {
            SpectrumDisplayLayout layout = SpectrumDisplayModes.GetLayout(mode);

            Rectangle bounds = SpectrumStatusOverlay.GetBounds(
                layout.ClientWidth,
                layout.Scale,
                avoidFullTapeOverlay: false);

            Assert.Equal(new Point(expectedX, expectedY), bounds.Location);
            Assert.Equal(new Size(expectedWidth, expectedHeight), bounds.Size);
        }

        [Fact]
        public void StatusOverlay_MovesBelowFullTapeFeedbackAtNativeScale()
        {
            Rectangle bounds = SpectrumStatusOverlay.GetBounds(
                bitmapWidth: SpectrumFrameBuffer.Width,
                scale: 1,
                avoidFullTapeOverlay: true);

            Assert.Equal(new Point(199, 46), bounds.Location);
        }

        [Theory]
        [InlineData("+2", TapeTransportState.Stopped, false, true, SpectrumDisplayFilter.Digital, "Inserted", "Digital")]
        [InlineData("+2A", TapeTransportState.Stopped, true, false, SpectrumDisplayFilter.SoftCrtWithMask, "Auto-stopped", "Soft CRT + Mask")]
        [InlineData("+3", TapeTransportState.Ended, false, false, SpectrumDisplayFilter.PalTv, "Ended", "PAL TV")]
        public void StatusOverlay_FormatsOneValuePerLine(string profile, TapeTransportState state,
            bool marker, bool inserted, SpectrumDisplayFilter filter, string tapeLabel, string filterLabel)
        {
            var data = new SpectrumStatusOverlayData(
                50,
                profile, state, marker, inserted, "2x Enhanced", filter);

            Assert.Equal(
                ["FPS: 50", $"Model: {profile}", $"Tape: {tapeLabel}", "Display: 2x Enhanced", $"Filter: {filterLabel}"],
                SpectrumStatusOverlay.FormatLines(data));
        }
    }
}
