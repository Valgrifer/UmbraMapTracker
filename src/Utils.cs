using Dalamud.Plugin.Services;
using Umbra.Common;
using UmbraMapTracker.Services;

namespace UmbraMapTracker;

internal sealed class Utils
{
    public static readonly IDataManager        DataManager         = Framework.Service<IDataManager>();
    
    public static readonly Config              Config              = Config.GetConfig();
    public static readonly GameFunction        GameFunction        = Framework.Service<GameFunction>();
    
    public static readonly LocalPlayerMapState LocalPlayerMapState = Framework.Service<LocalPlayerMapState>();
    public static readonly Share               Share               = Framework.Service<Share>();
}