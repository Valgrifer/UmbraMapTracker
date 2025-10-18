using System.Diagnostics;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace SimpleMapTracker;

public unsafe class LocalPlayerMapState : PlayerMapState {
    public static LocalPlayerMapState Instance { get; } = new();
    public override ulong ContentId => PlayerState.Instance()->ContentId;
    public override string Name => PlayerState.Instance()->CharacterNameString;

    private readonly Stopwatch updateThrottle = Stopwatch.StartNew();

    private uint rankCache;
    private ushort spotCache;

    public override ushort TreasureSpotId {
        get {
            if (updateThrottle.ElapsedMilliseconds < 1000) return spotCache;
            rankCache = Plugin.GameFunction.GetCurrentTreasureHuntRank();
            spotCache = Plugin.GameFunction.GetCurrentTreasureHuntSpot();
            return spotCache;
        }
    }

    public override uint TreasureHuntRankId {
        get {
            if (updateThrottle.ElapsedMilliseconds < 1000) return rankCache;
            rankCache = Plugin.GameFunction.GetCurrentTreasureHuntRank();
            spotCache = Plugin.GameFunction.GetCurrentTreasureHuntSpot();
            return rankCache;
        }
    }
}
