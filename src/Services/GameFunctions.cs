using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;

namespace UmbraMapTracker.Services;

[Service]
public class GameFunction {
    public GameFunction(IGameInteropProvider gameInteropProvider) {
        gameInteropProvider.InitializeFromAttributes(this);
    }

    private delegate uint GetCurrentTreasureHuntRankDelegate(nint a1);

    private delegate ushort GetCurrentTreasureHuntSpotDelegate(nint a1);

    [Signature("E8 ?? ?? ?? ?? 0F B6 D0 45 0F B6 CE", ScanType = ScanType.Text)]
    private readonly GetCurrentTreasureHuntRankDelegate getCurrentTreasureHuntRank = null!;

    [Signature("E8 ?? ?? ?? ?? 44 0F B7 C0 45 33 C9 0F B7 D3", ScanType = ScanType.Text)]
    private readonly GetCurrentTreasureHuntSpotDelegate getCurrentTreasureHuntSpot = null!;

    [Signature("48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 75 E4", Offset = 3, ScanType = ScanType.StaticAddress)]
    private readonly nint treasureHuntManager = nint.Zero;

    public uint GetCurrentTreasureHuntRank() {
        return getCurrentTreasureHuntRank(treasureHuntManager);
    }

    public ushort GetCurrentTreasureHuntSpot() {
        return getCurrentTreasureHuntSpot(treasureHuntManager);
    }
}
