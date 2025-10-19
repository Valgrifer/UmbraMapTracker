
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Websocket.Client;

namespace UmbraMapTracker.Services;

public class MapIdentifier {
    [JsonProperty("mapType")] public uint TreasureHuntRankId;
    [JsonProperty("mapSpot")] public ushort TreasureSpot;

    public override string ToString() {
        return $"{TreasureHuntRankId}.{TreasureSpot}";
    }
}

public class ShareData : MapIdentifier {
    [JsonProperty("user")] public string User = string.Empty;
    [JsonProperty("party")] public Dictionary<string, MapIdentifier?> Party = new();

    public override string ToString() {
        return $"{User} - {TreasureHuntRankId}.{TreasureSpot}";
    }
}

[Service]
public class Share : IDisposable, IAsyncDisposable {
    public bool IsSetup => client?.IsStarted ?? false;
    public bool IsConnected => client?.IsRunning ?? false;
    
    private WebsocketClient? client;

    public ShareData Data { get; } = new();
    private string sharedData = string.Empty;
    private readonly Stopwatch updated = Stopwatch.StartNew();

    public Share()
    {
        Task.Run(async delegate
        {
            await Task.Delay(1000);
            Setup();
        });
    }

    public void Setup() {
        client = new WebsocketClient(new Uri($"{(Utils.Config.UseSsl ? "wss" : "ws")}://{Utils.Config.Server}"));
        client.ReconnectionHappened.Subscribe(OnConnected);
        client.MessageReceived.Subscribe(OnMessage, OnError);
        client.DisconnectionHappened.Subscribe(OnDisconnect);
        client.ReconnectTimeout = TimeSpan.FromSeconds(10);
        client.ErrorReconnectTimeout = TimeSpan.FromSeconds(10);
        client.LostReconnectTimeout = TimeSpan.FromSeconds(10);
        client.Start();
    }

    private void OnConnected(ReconnectionInfo info) {
        sharedData = string.Empty;
        Logger.Debug("[MapTracker] Reconnected");
    }

    private void OnMessage(ResponseMessage msg) {
        if (msg.Text == null) return;
        try {
            var updateData = JsonConvert.DeserializeObject<ShareData>(msg.Text);
            if (updateData == null) return;
            foreach (var p in updateData.Party) {
                if (Data.Party.ContainsKey(p.Key)) Data.Party[p.Key] = p.Value;
            }
        } catch (Exception ex) {
            OnError(ex);
        }
    }

    private void OnError(Exception exception) {
        sharedData = string.Empty;
        Logger.Error($"[MapTracker] Error: {exception.Message}");
    }

    private void OnDisconnect(DisconnectionInfo info) {
        sharedData = string.Empty;
        Logger.Warning($"[MapTracker] Disconnected - {info.Exception?.Message}");
    }
    
    
    
    
    private static readonly Dictionary<(ulong, long, long), string> HashCache     = new();
    private static readonly Dictionary<ulong, PlayerMapState>       MapStateCache = new();
    
    public static string MakeId(ulong contentId, long groupId, long groupId2) {
        if (HashCache.TryGetValue((contentId, groupId, groupId2), out var h)) return h;
        var join = new List<byte>();
        join.AddRange(BitConverter.GetBytes(contentId));
        join.AddRange(BitConverter.GetBytes(groupId));
        join.AddRange(BitConverter.GetBytes(groupId2));
        h = Convert.ToHexString(SHA256.HashData(join.ToArray()));
        HashCache.TryAdd((contentId, groupId, groupId2), h);
        return h;
    }

    public static PlayerMapState GetMapState(ulong contentId, string name) {
        if (MapStateCache.TryGetValue(contentId, out var state)) return state;
        state = new PlayerMapState { ContentId = contentId, Name = name };
        MapStateCache.Add(contentId, state);
        return state;
    }

    [OnTick(1000)]
    private void FrameworkUpdate() {
        if (!IsConnected) return;
        unsafe
        {
            var contentId = PlayerState.Instance()->ContentId;
            var groupId = GroupManager.Instance()->MainGroup.PartyId;
            var groupId2 = GroupManager.Instance()->MainGroup.PartyId_2;
            
            if (contentId == 0 || groupId == 0 || groupId2 == 0) {
                Update(string.Empty, 0, 0, []);
            } else {
                List<string> party = [];
                foreach (var pm in GroupManager.Instance()->MainGroup.PartyMembers[..GroupManager.Instance()->MainGroup.MemberCount]) {
                    if (pm.ContentId == 0) continue;
                    if (pm.ContentId == contentId) continue;
                    var id = MakeId(pm.ContentId, groupId, groupId2);
                    var state = GetMapState(pm.ContentId, pm.NameString);
                    if (Data.Party.TryGetValue(id, out var sharedPartyMember) && sharedPartyMember != null) {
                        state.TreasureHuntRankId = sharedPartyMember.TreasureHuntRankId;
                        state.TreasureSpotId = sharedPartyMember.TreasureSpot;
                    }
                    party.Add(id);
                }

                Update(MakeId(contentId, groupId, groupId2), Utils.LocalPlayerMapState.TreasureHuntRankId, Utils.LocalPlayerMapState.TreasureSpotId, party);
            }
        }
    }
    
    public void Update(string name, uint rankId, ushort spot, IEnumerable<string> party) {
        Data.User = name;
        Data.TreasureHuntRankId = rankId;
        Data.TreasureSpot = spot;
        foreach (var member in Data.Party.Keys.Where(p => !party.Contains(p))) Data.Party.Remove(member);
        foreach (var member in party) Data.Party.TryAdd(member, null);
        var dataJson = JsonConvert.SerializeObject(Data);
        if (updated.ElapsedMilliseconds <= 30000 && sharedData == dataJson) return; 
        sharedData = dataJson;
        updated.Restart();
        client?.Send(dataJson);
    }

    public List<PlayerMapState> GetPlayers() {
        unsafe
        {
            var playerList = new List<PlayerMapState> { Utils.LocalPlayerMapState };
            foreach (var pm in GroupManager.Instance()->MainGroup.PartyMembers[..GroupManager.Instance()->MainGroup.MemberCount]) {
                if (pm.EntityId == 0xE0000000) continue;
                if (playerList.Any(p => p.ContentId == pm.ContentId)) continue;
                var partyMemberState = GetMapState(pm.ContentId, pm.NameString);
                playerList.Add(partyMemberState);
            }

            return playerList;
        }
    }

    public void Reset()
    {
        if (IsSetup)
            Stop();
        Setup();
    }

    private void Stop()
    {
        sharedData = string.Empty;
        client?.Stop(WebSocketCloseStatus.NormalClosure, "Closing").Wait();
        client?.Dispose();
        client = null;
    }
    

    [WhenFrameworkDisposing]
    public void Dispose()
    {
        Stop();
    }

    public async ValueTask DisposeAsync() {
        if (client != null) {
            await client.Stop(WebSocketCloseStatus.NormalClosure, "Closing");
            client.Dispose();
        }
    }
}
