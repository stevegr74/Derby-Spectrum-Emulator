using Derby.Pokes;

namespace Derby
{
    internal sealed class QuickStateSlot : IDisposable
    {
        public QuickStateSlot(
            SpectrumMachine.QuickState state,
            byte[] persistentState,
            SpectrumRomSet romSet,
            string? mediaDisplayName,
            PokeMediaIdentity? mediaIdentity,
            Bitmap thumbnail)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            PersistentState = persistentState ?? throw new ArgumentNullException(nameof(persistentState));
            RomSet = romSet ?? throw new ArgumentNullException(nameof(romSet));
            MediaDisplayName = mediaDisplayName;
            MediaIdentity = mediaIdentity;
            Thumbnail = thumbnail ?? throw new ArgumentNullException(nameof(thumbnail));
            CapturedAtUtc = state.CapturedAtUtc;
        }

        private QuickStateSlot(
            byte[] persistentState,
            SpectrumRomSet romSet,
            string? mediaDisplayName,
            PokeMediaIdentity? mediaIdentity,
            Bitmap thumbnail,
            DateTime capturedAtUtc)
        {
            PersistentState = persistentState ?? throw new ArgumentNullException(nameof(persistentState));
            RomSet = romSet ?? throw new ArgumentNullException(nameof(romSet));
            MediaDisplayName = mediaDisplayName;
            MediaIdentity = mediaIdentity;
            Thumbnail = thumbnail ?? throw new ArgumentNullException(nameof(thumbnail));
            CapturedAtUtc = capturedAtUtc;
        }

        public SpectrumMachine.QuickState? State { get; }
        public byte[] PersistentState { get; }
        public SpectrumRomSet RomSet { get; }
        public string? MediaDisplayName { get; }
        public PokeMediaIdentity? MediaIdentity { get; }
        public Bitmap Thumbnail { get; }
        public DateTime CapturedAtUtc { get; }

        public static QuickStateSlot FromPersistent(
            byte[] persistentState,
            SpectrumRomSet romSet,
            string? mediaDisplayName,
            PokeMediaIdentity? mediaIdentity,
            Bitmap thumbnail,
            DateTime capturedAtUtc) =>
            new(persistentState, romSet, mediaDisplayName, mediaIdentity, thumbnail, capturedAtUtc);

        public void Dispose() => Thumbnail.Dispose();
    }
}
