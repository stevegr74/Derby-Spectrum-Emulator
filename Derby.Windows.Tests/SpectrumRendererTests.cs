using System.Drawing;
using Xunit;

namespace Derby.Tests
{
    public class SpectrumRendererTests
    {
        [Fact]
        public void FrameBuffer_RendersBorderAndFlashWithoutSystemDrawing()
        {
            byte[] screenRam = new byte[0x1B00];
            screenRam[0] = 0x80;
            screenRam[0x1800] = (byte)(0x80 | (1 << 3) | 7);
            var frameBuffer = new SpectrumFrameBuffer();

            frameBuffer.Render(screenRam, borderColor: 2, flashPhase: false);

            ReadOnlySpan<uint> pixels = frameBuffer.Pixels.Span;
            int displayOffset = (SpectrumMachine.BorderTopHeight * SpectrumFrameBuffer.Width) +
                                SpectrumMachine.BorderLeftWidth;
            Assert.Equal(SpectrumFrameBuffer.GetSpectrumColorArgb(2, false), pixels[0]);
            Assert.Equal(SpectrumFrameBuffer.GetSpectrumColorArgb(7, false), pixels[displayOffset]);
            Assert.Equal(SpectrumFrameBuffer.GetSpectrumColorArgb(1, false), pixels[displayOffset + 1]);

            frameBuffer.Render(screenRam, borderColor: 2, flashPhase: true);

            pixels = frameBuffer.Pixels.Span;
            Assert.Equal(SpectrumFrameBuffer.GetSpectrumColorArgb(1, false), pixels[displayOffset]);
            Assert.Equal(SpectrumFrameBuffer.GetSpectrumColorArgb(7, false), pixels[displayOffset + 1]);
        }

        [Fact]
        public void FrameBuffer_RendersBorderColorChangesAtTheirTStatePositions()
        {
            const int firstVisibleTState = 14364 -
                                           (SpectrumMachine.BorderTopHeight * 228) -
                                           (SpectrumMachine.BorderLeftWidth / 2);
            var borderFrame = new BorderFrame(
                SpectrumMachine.FrameTStates128,
                initialColor: 1,
                new[] { new BorderEvent(firstVisibleTState + (10 * 228) + 4, 2) });
            var frameBuffer = new SpectrumFrameBuffer();

            frameBuffer.Render(new byte[0x1B00], borderFrame, flashPhase: false);

            ReadOnlySpan<uint> pixels = frameBuffer.Pixels.Span;
            Assert.Equal(SpectrumFrameBuffer.GetSpectrumColorArgb(1, false), pixels[9 * SpectrumFrameBuffer.Width]);
            Assert.Equal(SpectrumFrameBuffer.GetSpectrumColorArgb(2, false), pixels[10 * SpectrumFrameBuffer.Width]);
        }

        [Theory]
        [InlineData(SpectrumMachine.FrameTStates48, 14336, 224, 6)]
        [InlineData(SpectrumMachine.FrameTStates128, 14364, 228, 4)]
        public void FrameBuffer_LatchesBorderChangesInEightPixelGroups(
            int frameTStates,
            int topLeftDisplayTState,
            int lineTStates,
            int latchLeadTStates)
        {
            int firstVisibleTState = topLeftDisplayTState -
                                     (SpectrumMachine.BorderTopHeight * lineTStates) -
                                     (SpectrumMachine.BorderLeftWidth / 2);
            var borderFrame = new BorderFrame(
                frameTStates,
                initialColor: 1,
                new[] { new BorderEvent(firstVisibleTState + latchLeadTStates + 1, 2) });
            var frameBuffer = new SpectrumFrameBuffer();

            frameBuffer.Render(new byte[0x1B00], borderFrame, flashPhase: false);

            ReadOnlySpan<uint> pixels = frameBuffer.Pixels.Span;
            uint initial = SpectrumFrameBuffer.GetSpectrumColorArgb(1, false);
            uint changed = SpectrumFrameBuffer.GetSpectrumColorArgb(2, false);
            for (int x = 0; x < 8; x++)
                Assert.Equal(initial, pixels[x]);
            for (int x = 8; x < 16; x++)
                Assert.Equal(changed, pixels[x]);
        }

        [Theory]
        [InlineData(SpectrumMachine.FrameTStates48, 14336, 224, 6)]
        [InlineData(SpectrumMachine.FrameTStates128, 14364, 228, 4)]
        public void FrameBuffer_DoesNotStretchVisiblePixelsAcrossHorizontalFlyback(
            int frameTStates,
            int topLeftDisplayTState,
            int lineTStates,
            int latchLeadTStates)
        {
            int firstVisibleTState = topLeftDisplayTState -
                                     (SpectrumMachine.BorderTopHeight * lineTStates) -
                                     (SpectrumMachine.BorderLeftWidth / 2);
            int secondLineStart = firstVisibleTState + lineTStates;
            var borderFrame = new BorderFrame(
                frameTStates,
                initialColor: 1,
                new[] { new BorderEvent(secondLineStart + latchLeadTStates, 2) });
            var frameBuffer = new SpectrumFrameBuffer();

            frameBuffer.Render(new byte[0x1B00], borderFrame, flashPhase: false);

            ReadOnlySpan<uint> pixels = frameBuffer.Pixels.Span;
            uint initial = SpectrumFrameBuffer.GetSpectrumColorArgb(1, false);
            uint changed = SpectrumFrameBuffer.GetSpectrumColorArgb(2, false);
            Assert.Equal(initial, pixels[SpectrumFrameBuffer.Width - 1]);
            Assert.Equal(changed, pixels[SpectrumFrameBuffer.Width]);
        }

        [Fact]
        public void GetSpectrumColor_Uses_Standard_ZxSpectrum_Palette_Intensities()
        {
            Assert.Equal(Color.FromArgb(0xFF, 0x00, 0x00, 0xD7).ToArgb(), SpectrumRenderer.GetSpectrumColor(1, false).ToArgb());
            Assert.Equal(Color.FromArgb(0xFF, 0xD7, 0x00, 0x00).ToArgb(), SpectrumRenderer.GetSpectrumColor(2, false).ToArgb());
            Assert.Equal(Color.FromArgb(0xFF, 0xD7, 0xD7, 0xD7).ToArgb(), SpectrumRenderer.GetSpectrumColor(7, false).ToArgb());

            Assert.Equal(Color.FromArgb(0xFF, 0x00, 0x00, 0xFF).ToArgb(), SpectrumRenderer.GetSpectrumColor(1, true).ToArgb());
            Assert.Equal(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF).ToArgb(), SpectrumRenderer.GetSpectrumColor(7, true).ToArgb());
            Assert.Equal(Color.Black.ToArgb(), SpectrumRenderer.GetSpectrumColor(0, false).ToArgb());
            Assert.Equal(Color.Black.ToArgb(), SpectrumRenderer.GetSpectrumColor(0, true).ToArgb());
        }

        [Fact]
        public void RenderToBitmap_Fills_The_Visible_Border_With_The_Selected_Color()
        {
            using var bitmap = new Bitmap(
                SpectrumMachine.ScreenWidth,
                SpectrumMachine.ScreenHeight,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            SpectrumRenderer.RenderToBitmap(bitmap, new byte[0x1B00], borderColor: 2, flashPhase: false);

            int expectedBorder = SpectrumRenderer.GetSpectrumColor(2, false).ToArgb();
            Assert.Equal(expectedBorder, bitmap.GetPixel(0, 0).ToArgb());
            Assert.Equal(expectedBorder, bitmap.GetPixel(SpectrumMachine.BorderLeftWidth - 1, SpectrumMachine.BorderTopHeight).ToArgb());
            Assert.Equal(expectedBorder, bitmap.GetPixel(SpectrumMachine.ScreenWidth - 1, SpectrumMachine.ScreenHeight - 1).ToArgb());
        }

        [Fact]
        public void RenderToBitmap_Renders_Flash_And_NonFlash_Correctly()
        {
            using var bitmap = new Bitmap(
                SpectrumMachine.ScreenWidth,
                SpectrumMachine.ScreenHeight,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            byte[] screenRam = new byte[0x1B00];

            // Top-left 8-pixel cell, first scanline:
            // 1000 0000 -> first pixel on, remaining 7 off
            screenRam[0] = 0x80;

            // Attribute for top-left cell:
            // FLASH=1, BRIGHT=0, PAPER=1 (blue), INK=7 (white)
            screenRam[0x1800] = (byte)(0x80 | (1 << 3) | 7);

            SpectrumRenderer.RenderToBitmap(bitmap, screenRam, borderColor: 0, flashPhase: false);

            int displayX = SpectrumMachine.BorderLeftWidth;
            int displayY = SpectrumMachine.BorderTopHeight;
            Color litNormal = bitmap.GetPixel(displayX, displayY);
            Color unlitNormal = bitmap.GetPixel(displayX + 1, displayY);

            Assert.Equal(SpectrumRenderer.GetSpectrumColor(7, false).ToArgb(), litNormal.ToArgb());
            Assert.Equal(SpectrumRenderer.GetSpectrumColor(1, false).ToArgb(), unlitNormal.ToArgb());

            SpectrumRenderer.RenderToBitmap(bitmap, screenRam, borderColor: 0, flashPhase: true);

            Color litFlash = bitmap.GetPixel(displayX, displayY);
            Color unlitFlash = bitmap.GetPixel(displayX + 1, displayY);

            Assert.Equal(SpectrumRenderer.GetSpectrumColor(1, false).ToArgb(), litFlash.ToArgb());
            Assert.Equal(SpectrumRenderer.GetSpectrumColor(7, false).ToArgb(), unlitFlash.ToArgb());
        }

        [Fact]
        public void RenderToBitmap_Does_Not_Swap_When_Flash_Bit_Is_Clear()
        {
            using var bitmap = new Bitmap(
                SpectrumMachine.ScreenWidth,
                SpectrumMachine.ScreenHeight,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            byte[] screenRam = new byte[0x1B00];
            screenRam[0] = 0x80;

            // FLASH=0, BRIGHT=0, PAPER=1, INK=7
            screenRam[0x1800] = (byte)((1 << 3) | 7);

            SpectrumRenderer.RenderToBitmap(bitmap, screenRam, borderColor: 0, flashPhase: false);
            int displayX = SpectrumMachine.BorderLeftWidth;
            int displayY = SpectrumMachine.BorderTopHeight;
            Color litNormal = bitmap.GetPixel(displayX, displayY);
            Color unlitNormal = bitmap.GetPixel(displayX + 1, displayY);

            SpectrumRenderer.RenderToBitmap(bitmap, screenRam, borderColor: 0, flashPhase: true);
            Color litFlash = bitmap.GetPixel(displayX, displayY);
            Color unlitFlash = bitmap.GetPixel(displayX + 1, displayY);

            Assert.Equal(litNormal.ToArgb(), litFlash.ToArgb());
            Assert.Equal(unlitNormal.ToArgb(), unlitFlash.ToArgb());
        }
    }
}
