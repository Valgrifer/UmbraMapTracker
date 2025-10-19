using System.Diagnostics;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace UmbraMapTracker.Services;

[Service]
public unsafe class LocalPlayerMapState : PlayerMapState {
    public override ulong ContentId => PlayerState.Instance()->ContentId;
    public override string Name => PlayerState.Instance()->CharacterNameString;

    private readonly Stopwatch updateThrottle = Stopwatch.StartNew();

    private uint rankCache;
    private ushort spotCache;

    public override ushort TreasureSpotId {
        get {
            if (updateThrottle.ElapsedMilliseconds < 1000) return spotCache;
            rankCache = Utils.GameFunction.GetCurrentTreasureHuntRank();
            spotCache = Utils.GameFunction.GetCurrentTreasureHuntSpot();
            return spotCache;
        }
    }

    public override uint TreasureHuntRankId {
        get {
            if (updateThrottle.ElapsedMilliseconds < 1000) return rankCache;
            rankCache = Utils.GameFunction.GetCurrentTreasureHuntRank();
            spotCache = Utils.GameFunction.GetCurrentTreasureHuntSpot();
            return rankCache;
        }
    }
}
