using System;

namespace Derby.Tape
{
    internal sealed class TapeTransportController
    {
        internal sealed record QuickState(
            MountedTape? LiveTape,
            MountedTape? EndedTape,
            MountedTape.QuickState? TapeState,
            TapeTransportState State,
            TapeTransportTransition LastTransition,
            IReadOnlyList<TapeTimelineMarker> Markers);

        public MountedTape? MountedTape { get; private set; }
        public TapeTransportState State { get; private set; } = TapeTransportState.NoTape;
        public TapeTransportTransition LastTransition { get; private set; } = TapeTransportTransition.None;
        public bool IsStoppedAtMarker => MountedTape?.IsPausedByStopMarker == true;
        private MountedTape? endedTape;
        private MountedTape? DeckTape => MountedTape ?? endedTape;
        public bool HasTapeInDeck => DeckTape != null;
        public void NameDeckImage(string identity) => DeckTape?.SetMediaIdentity(identity);
        public void KeepFastLoadedImage(MountedTape tape, int clockHz)
        {
            if (DeckTape != null)
                return;
            tape.ConfigurePlaybackClock(clockHz);
            endedTape = tape;
        }
        private IReadOnlyList<TapeTimelineMarker> markers = Array.Empty<TapeTimelineMarker>();

        public void Reset()
        {
            MountedTape = null;
            endedTape = null;
            markers = Array.Empty<TapeTimelineMarker>();
            State = TapeTransportState.NoTape;
            LastTransition = TapeTransportTransition.Reset;
        }

        public void Mount(MountedTape tape, int playbackClockHz, bool startStopped = false)
        {
            MountedTape = tape ?? throw new ArgumentNullException(nameof(tape));
            endedTape = null;
            markers = Array.Empty<TapeTimelineMarker>();
            MountedTape.ConfigurePlaybackClock(playbackClockHz);
            MountedTape.Reset();
            if (startStopped)
                MountedTape.HoldAtMount();
            State = TapeTransportState.NoTape;
            SynchronizeState(MountedTape);
            LastTransition = TapeTransportTransition.Mounted;
        }

        public TapeTransportState Toggle(ulong currentTStates)
        {
            if (MountedTape == null)
                return State;

            // A protected-loader hold can pause the signal while the deck still says Playing.
            bool resume = State == TapeTransportState.Stopped;
            if (resume)
                MountedTape.ResumePlayback(currentTStates);
            else
                MountedTape.PausePlayback(currentTStates);

            SynchronizeState(MountedTape);
            LastTransition = resume
                ? TapeTransportTransition.ManualResume
                : TapeTransportTransition.ManualStop;
            if (!resume && State == TapeTransportState.Stopped)
                RecordMarker(MountedTape, "Manual stop");
            return State;
        }

        public void Eject()
        {
            MountedTape = null;
            endedTape = null;
            markers = Array.Empty<TapeTimelineMarker>();
            State = TapeTransportState.NoTape;
            LastTransition = TapeTransportTransition.Ejected;
        }

        public void CompleteIfCurrent(MountedTape? tape)
        {
            if (!ReferenceEquals(MountedTape, tape))
                return;

            endedTape = tape;
            if (tape != null)
                RecordMarker(tape, "Tape ended", ended: true);
            MountedTape = null;
            State = TapeTransportState.Ended;
            LastTransition = TapeTransportTransition.Ended;
        }

        public void SynchronizeState(MountedTape? tape)
        {
            if (!ReferenceEquals(MountedTape, tape) || tape == null)
                return;

            TapeTransportState previousState = State;
            State = tape.HasCompletedPlayback
                ? TapeTransportState.Ended
                : tape.IsPlaybackPaused
                    ? TapeTransportState.Stopped
                    : TapeTransportState.Playing;
            if (State != previousState && tape.IsPausedByStopMarker)
            {
                LastTransition = TapeTransportTransition.StopMarker;
                RecordMarker(tape, "Tape stop marker");
            }
        }

        public void MarkPlaying(MountedTape tape)
        {
            if (ReferenceEquals(MountedTape, tape))
            {
                State = TapeTransportState.Playing;
                LastTransition = TapeTransportTransition.AutomaticRomResume;
            }
        }

        public void MarkStopped(MountedTape tape)
        {
            if (ReferenceEquals(MountedTape, tape))
            {
                State = TapeTransportState.Stopped;
                LastTransition = TapeTransportTransition.AutomaticHandoffStop;
                RecordMarker(tape, "Automatic pause");
            }
        }

        private void RecordMarker(MountedTape tape, string label, bool ended = false)
        {
            int blockIndex = ended ? tape.Timeline.BlockCount : tape.TimelinePosition;
            var marker = new TapeTimelineMarker(blockIndex, label, ended ? null : tape.CaptureCue());
            markers = Array.AsReadOnly(markers
                .Where(existing => existing.BlockIndex != blockIndex || existing.Label != label)
                .TakeLast(15).Append(marker).ToArray());
        }

        public TapeDeckView? CaptureDeck(bool canSeek)
        {
            if (DeckTape is not { } tape)
                return null;
            TapeTransportState deckState = MountedTape == null ? TapeTransportState.Ended : State;
            int index = deckState == TapeTransportState.Ended ? tape.Timeline.BlockCount : tape.TimelinePosition;
            return new(tape.Timeline, deckState, index, canSeek && CanSeek && tape.Timeline.BlockCount != 0, markers)
                { Position = deckState == TapeTransportState.Stopped ? tape.ScrubPosition ?? index : index };
        }

        private bool CanSeek => State == TapeTransportState.Stopped || (MountedTape == null && endedTape != null);

        public TapeCue? CaptureCue(TapeTimeline expected)
            => State == TapeTransportState.Stopped && Matches(expected) ? DeckTape!.CaptureCue() : null;

        public bool Matches(TapeTimeline expected) => ReferenceEquals(DeckTape?.Timeline, expected);

        public bool Scrub(TapeTimeline expected, double position)
        {
            if (State != TapeTransportState.Stopped || !Matches(expected) || MountedTape?.Scrub(position) != true)
                return false;
            LastTransition = TapeTransportTransition.ManualSeek;
            return true;
        }

        public short[]? Preview(TapeTimeline expected) =>
            State == TapeTransportState.Stopped && Matches(expected) ? MountedTape?.RenderPreview() : null;

        public bool Seek(TapeTimeline expected, int blockIndex, TapeCue? cue)
        {
            if (!CanSeek || !Matches(expected) || DeckTape is not { } tape)
                return false;
            MountedTape? repositioned = cue != null
                ? tape.RestoreCue(cue)
                : tape.SeekToBlock(blockIndex) ? tape : null;
            if (repositioned == null)
                return false;
            MountedTape = repositioned;
            endedTape = null;
            State = TapeTransportState.Stopped;
            LastTransition = TapeTransportTransition.ManualSeek;
            return true;
        }

        public QuickState CaptureQuickState() => new QuickState(
            MountedTape,
            endedTape,
            DeckTape?.CaptureQuickState(),
            State,
            LastTransition,
            markers);

        public void RestoreQuickState(QuickState state)
        {
            MountedTape = state.LiveTape;
            endedTape = state.EndedTape;
            if (DeckTape is { } tape && state.TapeState != null)
                tape.RestoreQuickState(state.TapeState);
            State = state.State;
            LastTransition = state.LastTransition;
            markers = state.Markers;
        }
    }
}
