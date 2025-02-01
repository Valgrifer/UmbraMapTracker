using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;

namespace SimpleMapTracker;

public class ConfigWindow(Config config, IDalamudPluginInterface pluginInterface) : Window("Simple Map Tracker - Config") {
    
    public override void OnOpen() {
        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = new Vector2(300, 300),
        };
    }

    public override void Draw() {
        var configChanged = false;

        if (ImGui.CollapsingHeader("Client Config", ImGuiTreeNodeFlags.DefaultOpen)) {
            configChanged |= ImGui.Checkbox("Show maps on map", ref config.ShowOnMap);
            configChanged |= ImGui.Checkbox("Show maps on minimap", ref config.ShowOnMiniMap);
        }
        
        if (ImGui.CollapsingHeader("Server Config")) {
            var serverConfigChanged = false;
            serverConfigChanged |= ImGui.InputText("Host", ref config.Server, 512);
            serverConfigChanged |= ImGui.Checkbox("Use SSL", ref config.UseSsl);

            configChanged |= serverConfigChanged;
            
            configChanged |= ImGui.Checkbox("Connect On Startup", ref config.ConnectOnStartup);
            
            if (serverConfigChanged) {
                Plugin.Share.DisposeAsync().ConfigureAwait(false);
                Plugin.Share = new Share();
            }

            if (!Plugin.Share.IsSetup) {
                if (ImGui.Button("Connect", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetTextLineHeightWithSpacing()))) {
                    Plugin.Share.Setup();
                }
            }
        }

        if (configChanged) {
            pluginInterface.SavePluginConfig(config);
        }
    }
}
