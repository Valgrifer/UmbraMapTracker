using Dalamud.Plugin.Services;
using System.Linq;
using Umbra.Widgets;
using Una.Drawing;

namespace UmbraMapTracker.Widget;

[ToolbarWidget(
    "PartyMapTrackerWidget",
    "Party Map Tracker Widget",
    "Treasure map tracker widget of your party.",
    ["treasuresMap", "treasures", "map"]
)]
internal sealed class PartyMapWidget(
    WidgetInfo info,
    string? guid = null,
    Dictionary<string, object>? configValues = null
) : StandardToolbarWidget(info, guid, configValues)
{
    public override MenuPopup Popup { get; } = new();
    
    protected override StandardWidgetFeatures Features =>
        StandardWidgetFeatures.Text |
        StandardWidgetFeatures.Icon |
        StandardWidgetFeatures.CustomizableIcon;

    protected override uint DefaultGameIconId => 115;
    
    private readonly IGameGui gameGui = Framework.Service<IGameGui>();

    protected override IEnumerable<IWidgetConfigVariable> GetConfigVariables()
    {
        return [
            ..base.GetConfigVariables(),
            new BooleanWidgetConfigVariable(
                "AutoHide",
                "Auto Hide",
                "Auto hide without open treasure map in party",
                true
            ) { Category = I18N.Translate("Widgets.Standard.Config.Category.General") },
            new ColorWidgetConfigVariable(
                "HasMapColor",
                "Label color possession treasure map",
                "Label color if treasure map is in possession",
                0xFF10C938
            ) { Category  = I18N.Translate("Widgets.Standard.Config.Category.General") },
            new ColorWidgetConfigVariable(
                "DontHasMapColor",
                "Label color no possession treasure map",
                "Label color if no treasure map is available",
                0xFF2499D4
            ) { Category  = I18N.Translate("Widgets.Standard.Config.Category.General") }
        ];
    }
    
    private readonly MenuPopup.Group playersGroup = new("Party Map Tracker");

    protected override void OnLoad()
    {
        Node.OnRightClick += OpenConfigWindow;
        
        Popup.Add(playersGroup);
        for (var i = 0; i < 8; i++)
        {
            var i1 = i;
            playersGroup.Add(new MenuPopup.Button("") { Id = $"Slot_{i}", SortIndex = i, IsDisabled = true, IsVisible = false, OnClick = () => OnClick(i1)});
        }
        
        SetText("0");
    }

    protected override void OnUnload()
    {
        Node.OnRightClick -= OpenConfigWindow;
    }

    protected override void OnDraw()
    {
        var playerList = Utils.Share.GetPlayers();
        var count      = playerList.Where(p => p.HasMap).ToList().Count;

        if (GetConfigValue<bool>("AutoHide") && count == 0) {
            IsVisible = false;
            return;
        }
        IsVisible = true;
        
        SetText(count.ToString());

        int index = 0;
        foreach (var player in playerList)
        {
            MenuPopup.Button button = playersGroup.Get<MenuPopup.Button>($"Slot_{index}");
            index++;

            button.IsVisible = true;
            button.Label = player.Name;
            button.IsDisabled = !player.HasMap;
            button.TextColor = new Color(player.HasMap ? GetConfigValue<uint>("HasMapColor") : GetConfigValue<uint>("DontHasMapColor"));
        }

        for (int i = index; i < 8; i++)
        {
            MenuPopup.Button button = playersGroup.Get<MenuPopup.Button>($"Slot_{i}");
            button.IsVisible = false;
            button.Label = "";
            button.IsDisabled = true;
        }
    }
    
    private void OnClick(int index)
    {
        var playerList = Utils.Share.GetPlayers();
        
        if (playerList.Count < index) return;

        var link = playerList[index].CreateMapLink();
        if (link == null) {
            Logger.Warning("[MapTracker] Failed to open map.");
        } else {
            gameGui.OpenMapWithMapLink(link);
        }
    }
    
    private void OpenConfigWindow(Node _)
    {
        Utils.Config.ToggleWindow();
    }
}