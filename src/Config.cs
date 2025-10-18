using Dalamud.Configuration;

namespace UmbraMapTracker;

public class Config : IPluginConfiguration {
    public int Version { get; set; } = 1;
    public bool WindowOpen;
    public bool UseSsl = true;
    public string Server = "maps.caraxi.dev";
    public bool ConnectOnStartup = true;

    public bool ShowOnMap = true;
    public bool ShowOnMiniMap = true;
}
