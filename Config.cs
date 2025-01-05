using Dalamud.Configuration;

namespace SimpleMapTracker;

public class Config : IPluginConfiguration {
    public int Version { get; set; } = 1;
    public bool WindowOpen;
    public bool UseSsl = true;
    public string Server = "maps.caraxi.dev";
    public bool ConnectOnStartup = true;
}
