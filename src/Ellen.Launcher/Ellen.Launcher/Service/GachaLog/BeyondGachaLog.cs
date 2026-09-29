using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;
using System.Collections.Frozen;

namespace Ellen.Launcher.Service.GachaLog;

public static class BeyondGachaLog
{
    public static readonly FrozenSet<GachaType> QueryTypes =
    [
        GachaType.UGCStandard,
        GachaType.UGCAvatarEventWish,
    ];
}
