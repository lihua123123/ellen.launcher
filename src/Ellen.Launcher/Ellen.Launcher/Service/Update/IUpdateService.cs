// Ellen.Launcher

namespace Ellen.Launcher.Service.Update;

public interface IUpdateService
{
    /// <summary>最近一次检查的结论文本，供设置页和通知直接显示。</summary>
    string? UpdateInfo { get; }

    /// <summary>最近一次检查的完整结果；未检查过为 null。</summary>
    CheckUpdateResult? LastResult { get; }

    ValueTask<CheckUpdateResult> CheckUpdateAsync(CancellationToken token = default);

    /// <summary>打开发布页（GitHub Releases 或清单里的等价地址），不在客户端内下载。</summary>
    ValueTask OpenDownloadPageAsync(CheckUpdateResult result);
}
