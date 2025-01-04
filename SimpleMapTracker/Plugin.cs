using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using Dalamud.Game.Command;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using ImGuiNET;

namespace SimpleMapTracker;

public unsafe class Plugin : Window, IDalamudPlugin {
    public static IDataManager DataManager { get; private set; } = null!;
    public static GameFunction GameFunction { get; private set; } = null!;
    public static IPluginLog Log { get; private set; } = null!;
    public static Share Share { get; private set; } = null!;

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IGameGui gameGui;
    private readonly ICommandManager commandManager;
    private readonly WindowSystem windowSystem;

    public Plugin(IDalamudPluginInterface pluginInterface, IFramework framework, IDataManager dataManager, IGameInteropProvider gameInteropProvider, IPluginLog pluginLog, IGameGui gameGui, ICommandManager commandManager) : base("Simple Map Helper", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize) {
        RespectCloseHotkey = false;
        this.pluginInterface = pluginInterface;
        this.gameGui = gameGui;
        this.commandManager = commandManager;
        Share = new Share();
        DataManager = dataManager;

        Log = pluginLog;
        GameFunction = new GameFunction(gameInteropProvider);

        windowSystem = new WindowSystem(nameof(SimpleMapHelper));
        windowSystem.AddWindow(this);

        pluginInterface.UiBuilder.Draw += windowSystem.Draw;
        pluginInterface.UiBuilder.OpenMainUi += Toggle;

        commandManager.AddHandler("/maps", new CommandInfo((_, _) => Toggle()) { ShowInHelp = true, HelpMessage = "Open the Simple Map Helper window" });

        framework.Update += FrameworkUpdate;
#if DEBUG
        IsOpen = true;
#endif
    }

    private readonly Stopwatch updateThrottle = Stopwatch.StartNew();
    private readonly Dictionary<(ulong, long, long), string> hashCache = new();

    private string MakeId(ulong contentId, long groupId, long groupId2) {
        if (hashCache.TryGetValue((contentId, groupId, groupId2), out var h)) return h;
        var join = new List<byte>();
        join.AddRange(BitConverter.GetBytes(contentId));
        join.AddRange(BitConverter.GetBytes(groupId));
        join.AddRange(BitConverter.GetBytes(groupId2));
        h = Convert.ToHexString(SHA256.HashData(join.ToArray()));
        hashCache.TryAdd((contentId, groupId, groupId2), h);
        return h;
    }

    private void FrameworkUpdate(IFramework framework) {
        if (updateThrottle.ElapsedMilliseconds > 1000) {
            updateThrottle.Restart();
            var contentId = PlayerState.Instance()->ContentId;
            var groupId = GroupManager.Instance()->MainGroup.PartyId;
            var groupId2 = GroupManager.Instance()->MainGroup.PartyId_2;

            if (contentId == 0 || groupId == 0 || groupId2 == 0) {
                Share.Update(string.Empty, 0, 0, []);
            } else {
                var party = GroupManager.Instance()->MainGroup.PartyMembers.ToArray()
                    .Where(p => p.ContentId != 0 && p.ContentId != contentId)
                    .Select(p => {
                        var id = MakeId(p.ContentId, groupId, groupId2);
                        var state = GetMapState(p.ContentId, p.NameString);
                        if (Share.Data.Party.TryGetValue(id, out var sharedPartyMember) && sharedPartyMember != null) {
                            state.TreasureHuntRankId = sharedPartyMember.TreasureHuntRankId;
                            state.TreasureSpotId = sharedPartyMember.TreasureSpot;
                        }

                        return id;
                    });

                Share.Update(MakeId(contentId, groupId, groupId2), LocalPlayerMapState.Instance.TreasureHuntRankId, LocalPlayerMapState.Instance.TreasureSpotId, party);
            }
        }
    }

    private readonly Dictionary<ulong, PlayerMapState> mapStateCache = new();

    public PlayerMapState GetMapState(ulong contentId, string name) {
        if (mapStateCache.TryGetValue(contentId, out var state)) return state;
        state = new PlayerMapState { ContentId = contentId, Name = name };
        mapStateCache.Add(contentId, state);
        return state;
    }

    public List<PlayerMapState> GetPlayers() {
        var playerList = new List<PlayerMapState> { LocalPlayerMapState.Instance };
        foreach (var pm in GroupManager.Instance()->MainGroup.PartyMembers) {
            if (pm.EntityId == 0xE0000000) continue;
            if (playerList.Any(p => p.ContentId == pm.ContentId)) continue;
            var partyMemberState = GetMapState(pm.ContentId, pm.NameString);
            playerList.Add(partyMemberState);
        }

        return playerList;
    }

    public override void Draw() {
        using (ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new Vector2(4))) {
            if (ImGui.BeginTable("partyMembers", 3, ImGuiTableFlags.NoClip | ImGuiTableFlags.Sortable | ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit)) {
                ImGui.TableSetupColumn("Party Member", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("###controls", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Zone", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableHeadersRow();
                var sortSpecs = ImGui.TableGetSortSpecs();
                var players = GetPlayers();
                players.Sort((a, b) => {
                    return sortSpecs.Specs.ColumnIndex switch {
                        1 => string.Compare(a.Name, b.Name, StringComparison.InvariantCultureIgnoreCase),
                        3 => string.Compare(a.ZoneName, b.ZoneName, StringComparison.InvariantCultureIgnoreCase),
                        _ => 0
                    } * (sortSpecs.Specs.SortDirection == ImGuiSortDirection.Ascending ? 1 : -1);
                });

                foreach (var p in players) {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text(p.Name);
#if DEBUG
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Content ID: {p.ContentId:X}");
#endif
                    ImGui.TableNextColumn();

                    using (ImRaii.PushFont(UiBuilder.IconFont)) {
                        using (ImRaii.Disabled(p.TreasureSpot == null)) {
                            ImGui.Text(FontAwesomeIcon.MapMarked.ToIconString());
                        }

                        if (ImGui.IsItemClicked() && p.TreasureSpot != null) {
                            var link = p.CreateMapLink();
                            if (link == null)
                                Log.Warning("Failed to open map.");
                            else
                                gameGui.OpenMapWithMapLink(link);
                        }
                    }

                    ImGui.TableNextColumn();
                    ImGui.Text(p.ZoneName);
                }

                ImGui.EndTable();
            }
        }
    }

    public void Dispose() {
        Share.Dispose();
        pluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        windowSystem.RemoveAllWindows();
        commandManager.RemoveHandler("/maps");
    }
}
