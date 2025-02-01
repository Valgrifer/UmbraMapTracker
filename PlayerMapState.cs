using Dalamud.Game.Text.SeStringHandling.Payloads;
using Lumina.Excel.Sheets;

namespace SimpleMapTracker;

public class PlayerMapState {
    public virtual ulong ContentId { get; init; }
    public virtual string Name { get; init; } = string.Empty;
    public virtual uint TreasureHuntRankId { get; set; }
    public virtual ushort TreasureSpotId { get; set; }

    public TreasureHuntRank? TreasureHuntRank =>
        TreasureHuntRankId == 0
            ? null
            : Plugin.DataManager.GetExcelSheet<TreasureHuntRank>()
                .GetRowOrDefault(TreasureHuntRankId);

    public TreasureSpot? TreasureSpot =>
        TreasureHuntRankId == 0
            ? null
            : Plugin.DataManager.GetSubrowExcelSheet<TreasureSpot>()
                .GetSubrowOrDefault(TreasureHuntRankId, TreasureSpotId);

    public string ZoneName => TreasureSpot?.Location.ValueNullable?.Territory.ValueNullable?.PlaceName.ValueNullable?.Name.ExtractText() ?? "No Open Map";

    public MapLinkPayload? CreateMapLink() {
        var location = TreasureSpot?.Location.ValueNullable;
        var map = location?.Map.ValueNullable;
        var territory = map?.TerritoryType.ValueNullable;
        if (territory == null || location == null || map == null) return null;
        var scale = map.Value.SizeFactor / 100f;
        var xPosition = 41f / scale * ((location.Value.X * scale + 1024f) / 2048f) + 1;
        var yPosition = 41f / scale * ((location.Value.Z * scale + 1024f) / 2048f) + 1;
        return new MapLinkPayload(territory.Value.RowId, map.Value.RowId, xPosition, yPosition);
    }
}
