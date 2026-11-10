// Ellen.Launcher
// 更新检查结果。只关心「有没有新版本」和「去哪个页面下载」两件事。

namespace Ellen.Launcher.Service.Update;

public sealed class CheckUpdateResult
{
    public CheckUpdateResultKind Kind { get; set; }

    /// <summary>命中的清单；<see cref="CheckUpdateResultKind.SourceUnavailable"/> 时为 null。</summary>
    public UpdateManifest? Manifest { get; set; }

    /// <summary>这次生效的源地址，用来在设置页显示「数据来自哪里」。</summary>
    public string? Source { get; set; }

    /// <summary>全部源都失败时，每个源各自的失败原因。</summary>
    public string? Error { get; set; }

    public System.Version? LatestVersion => Manifest?.ParsedVersion;
}
