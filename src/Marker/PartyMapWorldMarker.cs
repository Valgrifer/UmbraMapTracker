using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.Interop;
using Lumina.Excel.Sheets;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Umbra.Markers;

namespace UmbraMapTracker.Marker;

[Service]
internal sealed class PartyMapWorldMarker : WorldMarkerFactory
{
    public override string Id          => "Umbra_PartyMapTracker";
    public override string Name        => "Party Treasure Map Marker";
    public override string Description => "Shows world markers on treasure map of party.";

    private readonly IPlayer      player;
    private readonly IZoneManager zoneManager;
    
    private HashSet<MultiLabelMapMarker> mapMarkers     = [];
    private HashSet<Vector3>             minimapMarkers = [];

    private readonly Hook<AgentMap.Delegates.ResetMapMarkers>     resetMapMarkersHook;
    private readonly Hook<AgentMap.Delegates.ResetMiniMapMarkers> resetMiniMapMarkersHook;
    
    public PartyMapWorldMarker(
        IPlayer              player,
        IZoneManager         zoneManager,
        IGameInteropProvider gameInteropProvider
    ) {
        this.player      = player;
        this.zoneManager = zoneManager;
        
        unsafe
        {
            resetMapMarkersHook = gameInteropProvider.HookFromAddress<AgentMap.Delegates.ResetMapMarkers>(AgentMap.Addresses.ResetMapMarkers.Value, ResetMapMarkersDetour);
            resetMapMarkersHook.Enable();
        
            resetMiniMapMarkersHook = gameInteropProvider.HookFromAddress<AgentMap.Delegates.ResetMiniMapMarkers>(AgentMap.Addresses.ResetMiniMapMarkers.Value, ResetMiniMapMarkersDetour);
            resetMiniMapMarkersHook.Enable();
        }
    }

    public override List<IMarkerConfigVariable> GetConfigVariables()
    {
        return [
            ..DefaultStateConfigVariables,
            ..DefaultFadeConfigVariables,
            new BooleanMarkerConfigVariable(
                "ShowOnMap",
                "Show on map",
                null,
                true
            ),
            new BooleanMarkerConfigVariable(
                "ShowOnMiniMap",
                "Show on  mini map",
                null,
                true
            ),
        ];
    }

    [OnTick(100)]
    private void OnTick()
    {
        if (!GetConfigValue<bool>("Enabled") || !zoneManager.HasCurrentZone) {
            RemoveAllMarkers();
            return;
        }

        if (player.IsBetweenAreas) {
            RemoveAllMarkers();
            return;
        }
        
        bool    showOnCompass    = GetConfigValue<bool>("ShowOnCompass");
        Vector2 fadeDist         = new(GetConfigValue<int>("FadeAttenuation"), GetConfigValue<int>("FadeDistance"));
        
        List<string> usedKeys    = [];

        foreach (var playerState in Utils.Share.GetPlayers())
        {
            if (!playerState.HasMap) continue;
        
            Level? l = playerState.TreasureSpot?.Location.ValueNullable;
        
            if (l == null) continue;

            string key = $"PartyMapMarker_{playerState.Name}";
        
            Level level = l.Value;

            usedKeys.Add(key);
            SetMarker(
                new() {
                    Key                = key,
                    MapId              = level.Map.RowId,
                    IconId             = (uint) (playerState is LocalPlayerMapState ? 60354 : 60355),
                    Position           = new (level.X, level.Y, level.Z),
                    Label              = playerState.Name,
                    SubLabel           = "Carte aux trésors",
                    FadeDistance       = fadeDist,
                    MaxVisibleDistance = GetConfigValue<int>("MaxVisibleDistance"),
                    ShowOnCompass      = showOnCompass,
                }
            );
        }

        RemoveMarkersExcept(usedKeys);

        UpdateMapIcons();
    }
    
    
    private unsafe void UpdateMapIcons() {
        var oldMarkers = mapMarkers;
        var oldMinimapMarkers = minimapMarkers;
        mapMarkers = [];
        minimapMarkers = [];
        
        void AddMapForPlayer(PlayerMapState state) {

            if (state.TerritoryId != AgentMap.Instance()->CurrentTerritoryId && state.TerritoryId != AgentMap.Instance()->SelectedTerritoryId) {
                return;
            }
            
            var position = state.ActualPosition;
            if (position == null) return;
            
            var markerPosition = new Vector3(position.Value.X, position.Value.Y, position.Value.Z);

            if (state.TerritoryId == AgentMap.Instance()->CurrentTerritoryId) {
                minimapMarkers.Add(markerPosition);
            }

            if (state.TerritoryId == AgentMap.Instance()->SelectedTerritoryId && AgentMap.Instance()->IsAgentActive() && AgentMap.Instance()->AddonId != 0) {
                var labelMarker = new MultiLabelMapMarker(markerPosition);
                if (!mapMarkers.TryGetValue(labelMarker, out var l)) {
                    l = labelMarker;
                    mapMarkers.Add(l);
                }

                l.Labels.Add(state.Name);

            }
        }
        
        AddMapForPlayer(Utils.LocalPlayerMapState);
        for (var i = 0; i < GroupManager.Instance()->MainGroup.MemberCount; i++) {
            var gm = GroupManager.Instance()->MainGroup.PartyMembers.GetPointer(i);
            if (gm == null || gm->ContentId == PlayerState.Instance()->ContentId) continue;
            AddMapForPlayer(Share.GetMapState(gm->ContentId, gm->NameString));
        }
        
        if (!oldMarkers.SetEquals(mapMarkers)) {
            Logger.Debug("[MapTracker] Redrawing Map Icons");
            AgentMap.Instance()->ResetMapMarkers();
        }

        if (!oldMinimapMarkers.SetEquals(minimapMarkers)) {
            Logger.Debug("[MapTracker] Redrawing Minimap Icons");
            AgentMap.Instance()->ResetMiniMapMarkers();
        }
    }
    
    
    private unsafe void ResetMapMarkersDetour(AgentMap* agentMap) {
        resetMapMarkersHook.Original(agentMap);
        if (!GetConfigValue<bool>("ShowOnMap")) return;
        
        Logger.Debug("[MapTracker] Adding Map Markers");

        var i = 0;
        foreach (var m in mapMarkers) {
            var strPtr = mapMarkerStrings[i++];
            MemoryHelper.WriteString(strPtr, $"{m.Label}\0", Encoding.UTF8);
            agentMap->AddMapMarker(m.Position, 60355, 0, (byte*) strPtr, 1, 5);
        }

    }
    private unsafe void ResetMiniMapMarkersDetour(AgentMap* agentMap) {
        
        resetMiniMapMarkersHook.Original(agentMap);
        if (!GetConfigValue<bool>("ShowOnMiniMap")) return;
        Logger.Debug("[MapTracker] Adding Mini Map Markers");
        foreach (var m in minimapMarkers) {
            agentMap->AddMiniMapMarker(m, 60355);
        }
    }
    
    
    private readonly List<nint> mapMarkerStrings = [
        Marshal.AllocHGlobal(512),
        Marshal.AllocHGlobal(512),
        Marshal.AllocHGlobal(512),
        Marshal.AllocHGlobal(512),
        Marshal.AllocHGlobal(512),
        Marshal.AllocHGlobal(512),
        Marshal.AllocHGlobal(512),
        Marshal.AllocHGlobal(512),
    ];

    public override void Dispose()
    {
        base.Dispose();
        
        resetMapMarkersHook.Disable();
        resetMapMarkersHook.Dispose();
        resetMiniMapMarkersHook.Disable();
        resetMiniMapMarkersHook.Dispose();

        unsafe
        {
            AgentMap.Instance()->ResetMapMarkers();
            AgentMap.Instance()->ResetMiniMapMarkers();
        }
        
        foreach (var strPtr in mapMarkerStrings) {
            Marshal.FreeHGlobal(strPtr);
        }
    }
    
    

    public sealed class MultiLabelMapMarker(Vector3 position) {
        public readonly Vector3 Position = position;
        public HashSet<string> Labels = [];
        public string Label => string.Join('\n', Labels);
        public override bool Equals(object? obj) {
            return obj is MultiLabelMapMarker other && Position.Equals(other.Position);
        }

        public override int GetHashCode() {
            return Position.GetHashCode();
        }
    }
}