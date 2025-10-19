using System;

namespace UmbraMapTracker.Services;

[Serializable]
public partial class Config
{
    public int Version { get; set; } = 1;
    
    public bool UseSsl { get; set; } = true;
    
    public string Server { get; set; } = "maps.caraxi.dev";

    public static Config GetConfig()
    {
        Config? config = ReadFile<Config>("UmbraMapTracker.Config.json");
        return config ?? new Config();
    }

    public void SaveConfig()
    {
        WriteFile("UmbraMapTracker.Config.json", this);
    }
}