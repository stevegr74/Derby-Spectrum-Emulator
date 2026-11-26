using System.Security.Cryptography;
using Derby.Tape;

namespace Derby;

internal sealed class TapeSideImage
{
    private readonly byte[] data;
    public TapeSidePairStore.Side Side { get; }

    private TapeSideImage(string path, byte[] data)
    {
        this.data = data;
        Side = new(Convert.ToHexString(SHA256.HashData(data)), path);
    }

    public static bool IsTapePath(string path) =>
        Path.GetExtension(path).Equals(".tap", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(path).Equals(".tzx", StringComparison.OrdinalIgnoreCase);

    public static TapeSideImage Read(string path)
    {
        if (!IsTapePath(path))
            throw new InvalidOperationException("Choose a .tap or .tzx tape image.");
        return new(Path.GetFullPath(path), File.ReadAllBytes(path));
    }

    public static bool CanSwitch(TapeDeckView? deck) =>
        deck is { CanSeek: true, State: TapeTransportState.Stopped or TapeTransportState.Ended };

    public bool TryMount(SpectrumMachine machine, TapeTimeline expected, string pairedIdentity)
    {
        TapeDeckView? deck = machine.CaptureTapeDeck();
        if (!CanSwitch(deck) || !ReferenceEquals(deck?.Timeline, expected) ||
            Side.Identity != pairedIdentity || Side.Identity == expected.Identity)
            return false;

        if (Path.GetExtension(Side.Path).Equals(".tzx", StringComparison.OrdinalIgnoreCase))
            TzxLoader.MountImage(machine, Side.Name, data, startStopped: true);
        else
            TapLoader.MountImage(machine, Side.Name, data, startStopped: true);
        return true;
    }
}
