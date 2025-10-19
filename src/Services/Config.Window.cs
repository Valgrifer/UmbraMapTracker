using Umbra.Windows;
using Umbra.Windows.Library.VariableEditor;

namespace UmbraMapTracker.Services;

public partial class Config
{
    private VariablesEditorWindow? window;
    
    public void ToggleWindow()
    {
        if (window == null)
            OpenWindow();
        else
            window.Close();
    }
    
    public void OpenWindow()
    {
        BooleanVariable useSslVar = new("UseSsl") {
            Name        = "Use ssl",
            Value       = UseSsl,
        };

        StringVariable serverVar = new("Server") {
            Name        = "Server url",
            Value       = Server,
            MaxLength   = 4096,
        };         

        window = new("Umbra Map Tracker", [ useSslVar, serverVar, ], []);

        Framework
           .Service<WindowManager>()
           .Present(
               "UmbraMapTracker",
                window,
                _ =>
                {
                    Utils.Share.Reset();
                    window = null;
                }
            );

        useSslVar.ValueChanged           += v => UseSsl = v;
        serverVar.ValueChanged           += v => Server = v;
    }
}