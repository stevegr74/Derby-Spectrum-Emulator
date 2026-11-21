using System;

namespace Derby.Tape
{
    internal sealed class TapeTransportController
    {
        internal sealed record QuickState(
            MountedTape? MountedTape,
            MountedTape.QuickState? MountedTapeState,
            TapeTransportState State,
            TapeTransportTransition LastTransition);

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

        public void Mount(MountedTape tape, int playbackClockHz)
        {
            MountedTape = tape ?? throw new ArgumentNullException(nameof(tape));
            endedTape = null;
            markers = Array.Empty<TapeTimelineMarker>();
            MountedTape.ConfigurePlaybackClock(playbackClockHz);
            MountedTape.Reset();
            State = TapeTransportState.NoTape;
            SynchronizeState(MountedTape);
            LastTransition = TapeTransportTransition.Mounted;
        }

        public TapeTransportState Toggle(ulong currentTStates)
        {
            if (MountedTape == null)
                return State;

            bool wasPaused = MountedTape.IsPlaybackPaused;
            if (wasPaused)
                MountedTape.ResumePlayback(currentTStates);
            else
                MountedTape.PausePlayback(currentTStates);

            SynchronizeState(MountedTape);
            LastTransition = wasPaused
                ? TapeTransportTransition.ManualResume
                : TapeTransportTransition.ManualStop;
            if (!wasPaused && State == TapeTransportState.Stopped)
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
            return new(tape.Timeline, deckState,
                deckState == TapeTransportState.Ended ? tape.Timeline.BlockCount : tape.TimelinePosition,
                canSeek && CanSeek && tape.Timeline.BlockCount != 0, markers);
        }

        private bool CanSeek => State == TapeTransportState.Stopped || (MountedTape == null && endedTape != null);

        public TapeCue? CaptureCue(TapeTimeline expected)
            => State == TapeTransportState.Stopped && Matches(expected) ? DeckTape!.CaptureCue() : null;

        public bool Matches(TapeTimeline expected) => ReferenceEquals(DeckTape?.Timeline, expected);

        public void StopAtHeldPosition()
        {
            if (MountedTape is not { IsPlaybackPaused: true } tape)
                return;
            State = TapeTransportState.Stopped;
            LastTransition = TapeTransportTransition.ManualStop;
            RecordMarker(tape, "Manual stop");
        }

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
            MountedTape?.CaptureQuickState(),
            State,
            LastTransition);

        public void RestoreQuickState(QuickState state)
        {
            MountedTape = state.MountedTape;
            endedTape = null;
            markers = Array.Empty<TapeTimelineMarker>();
            if (MountedTape != null && state.MountedTapeState != null)
                MountedTape.RestoreQuickState(state.MountedTapeState);
            State = state.State;
            LastTransition = state.LastTransition;
        }
    }
}
