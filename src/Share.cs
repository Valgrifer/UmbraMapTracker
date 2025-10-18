﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Websocket.Client;

namespace SimpleMapTracker;

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

public class Share : IDisposable, IAsyncDisposable {
    public bool IsSetup => client?.IsStarted ?? false;
    public bool IsConnected => client?.IsRunning ?? false;
    
    private WebsocketClient? client;

    public ShareData Data { get; } = new();
    private string sharedData = string.Empty;
    private readonly Stopwatch updated = Stopwatch.StartNew();

    public void Setup() {
        client = new WebsocketClient(new Uri($"{(Plugin.Config.UseSsl ? "wss://" : "ws://")}{Plugin.Config.Server}"));
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
        Plugin.Log.Debug("Reconnected");
    }

    public void Update(string user, uint rank, ushort spot, IEnumerable<string> party) {
        Data.User = user;
        Data.TreasureHuntRankId = rank;
        Data.TreasureSpot = spot;
        foreach (var member in Data.Party.Keys.Where(p => !party.Contains(p))) Data.Party.Remove(member);
        foreach (var member in party) Data.Party.TryAdd(member, null);
        var dataJson = JsonConvert.SerializeObject(Data);
        if (updated.ElapsedMilliseconds <= 30000 && sharedData == dataJson) return; 
        sharedData = dataJson;
        updated.Restart();
        client?.Send(dataJson);
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
        Plugin.Log.Error($"Error: {exception.Message}");
    }

    private void OnDisconnect(DisconnectionInfo info) {
        sharedData = string.Empty;
        Plugin.Log.Warning($"Disconnected - {info.Exception?.Message}");
    }

    public void Dispose() {
        sharedData = string.Empty;
        client?.Stop(WebSocketCloseStatus.NormalClosure, "Closing").Wait();
        client?.Dispose();
    }

    public async ValueTask DisposeAsync() {
        if (client != null) {
            await client.Stop(WebSocketCloseStatus.NormalClosure, "Closing");
            client.Dispose();
        }
    }
}
