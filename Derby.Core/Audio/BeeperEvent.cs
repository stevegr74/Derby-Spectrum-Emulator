namespace Derby.Audio
{
    public readonly struct BeeperEvent
    {
        public BeeperEvent(int tStateOffset, bool speakerHigh)
        {
            TStateOffset = tStateOffset;
            SpeakerHigh = speakerHigh;
        }

        public int TStateOffset { get; }
        public bool SpeakerHigh { get; }
    }
}
