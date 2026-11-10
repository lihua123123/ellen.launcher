// Ellen.Launcher

namespace Ellen.Launcher.Service.Update;

public enum CheckUpdateResultKind
{
    None = 0,

    /// <summary>所有更新源都不可达，或返回的清单无效。</summary>
    SourceUnavailable = 1,

    /// <summary>本地版本 &gt;= 清单版本。</summary>
    AlreadyUpdated = 2,

    /// <summary>清单版本更新。</summary>
    UpdateAvailable = 3,
}
