using Derby.Tape;

namespace Derby
{
    internal sealed class TapeTransportOverlay
    {
        private const double FullOverlaySeconds = 3.0;
        private const double StoppedIconSeconds = 5.0;

        private TapeTransportState overlayState = TapeTransportState.NoTape;
        private bool overlayMarkerStop;
        private long fullOverlayExpiresAtTicks;
        private long iconExpiresAtTicks;

        internal TapeTransportState ObservedState { get; private set; } = TapeTransportState.NoTape;
        internal bool ObservedMarkerStop { get; private set; }
        internal TapeTransportState FeedbackState => overlayState;

        internal void SetObservedState(TapeTransportState state, bool markerStop)
        {
            ObservedState = state;
            ObservedMarkerStop = markerStop;
        }

        internal bool Observe(TapeTransportState state, bool markerStop, long nowTicks)
        {
            if (state == ObservedState && markerStop == ObservedMarkerStop)
                return false;

            SetObservedState(state, markerStop);
            if (state is TapeTransportState.Playing or TapeTransportState.Stopped or TapeTransportState.Ended)
                Show(state, markerStop, nowTicks);
            else
                Clear();

            return true;
        }

        internal void Show(TapeTransportState state, bool markerStop, long nowTicks)
        {
            overlayState = state;
            overlayMarkerStop = markerStop;
            fullOverlayExpiresAtTicks = nowTicks +
                (long)(FullOverlaySeconds * System.Diagnostics.Stopwatch.Frequency);
            iconExpiresAtTicks = state switch
            {
                TapeTransportState.Playing => long.MaxValue,
                TapeTransportState.Stopped => nowTicks +
                    (long)(StoppedIconSeconds * System.Diagnostics.Stopwatch.Frequency),
                _ => fullOverlayExpiresAtTicks
            };
        }

        internal void Clear()
        {
            overlayState = TapeTransportState.NoTape;
            overlayMarkerStop = false;
            fullOverlayExpiresAtTicks = 0;
            iconExpiresAtTicks = 0;
        }

        internal void ExpireEndedFeedback(long nowTicks)
        {
            if (overlayState == TapeTransportState.Ended && nowTicks >= iconExpiresAtTicks)
                Clear();
        }

        internal bool IsVisible(long nowTicks) =>
            overlayState != TapeTransportState.NoTape && nowTicks < iconExpiresAtTicks;

        internal bool IsFullOverlayVisible(long nowTicks) =>
            IsVisible(nowTicks) && nowTicks < fullOverlayExpiresAtTicks;

        internal void Draw(Bitmap bitmap, int scale, long nowTicks)
        {
            if (!IsVisible(nowTicks))
                return;

            string text = overlayState switch
            {
                TapeTransportState.Playing => "TAPE PLAYING - F5 STOP",
                TapeTransportState.Stopped when overlayMarkerStop => "TAPE AUTO-STOPPED - F5 RESUME",
                TapeTransportState.Stopped => "TAPE PAUSED - F5 RESUME",
                TapeTransportState.Ended => "TAPE ENDED",
                _ => string.Empty
            };
            Color accent = overlayState switch
            {
                TapeTransportState.Playing => Color.FromArgb(60, 220, 150),
                TapeTransportState.Stopped => Color.FromArgb(255, 190, 55),
                _ => Color.FromArgb(210, 215, 220)
            };
            bool showFullOverlay = IsFullOverlayVisible(nowTicks);

            int x = 8 * scale;
            int y = 8 * scale;
            int width = (showFullOverlay ? (overlayMarkerStop ? 238 : 190) : 30) * scale;
            int height = (showFullOverlay ? 30 : 24) * scale;
            int iconLeft = x + (8 * scale);
            int iconSize = (showFullOverlay ? 14 : 11) * scale;
            int iconTop = y + ((height - iconSize) / 2);

            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var backgroundBrush = new SolidBrush(Color.FromArgb(showFullOverlay ? 210 : 168, 8, 12, 14));
            using var accentBrush = new SolidBrush(accent);
            using var borderPen = new Pen(Color.FromArgb(showFullOverlay ? 235 : 190, accent), Math.Max(1, scale));
            using var font = new Font(FontFamily.GenericSansSerif, 8.5f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            graphics.FillRectangle(backgroundBrush, x, y, width, height);
            graphics.DrawRectangle(borderPen, x, y, width - 1, height - 1);

            if (overlayState == TapeTransportState.Playing)
            {
                graphics.FillPolygon(accentBrush, new[]
                {
                    new Point(iconLeft, iconTop),
                    new Point(iconLeft, iconTop + iconSize),
                    new Point(iconLeft + iconSize, iconTop + (iconSize / 2))
                });
            }
            else
            {
                graphics.FillRectangle(accentBrush, iconLeft, iconTop, iconSize, iconSize);
            }

            if (showFullOverlay)
                graphics.DrawString(text, font, accentBrush, x + (28 * scale), y + (9 * scale));
        }
    }
}
