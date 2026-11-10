// Ellen.Launcher
// 在线更新只做两件事：比对版本、把用户送到发布页。
// 不再内置 Snap.Hutao.Remastered.Deployment.exe —— 那个部署器会按上游源下载、
// 装的是上游构建，装完就把本分支覆盖掉；而且带 UAC 与静默安装，出问题无法回滚。

using Ellen.Launcher.Core;
using Ellen.Launcher.Core.Setting;

namespace Ellen.Launcher.Service.Update;

[Service(ServiceLifetime.Singleton, typeof(IUpdateService))]
public sealed partial class UpdateService : IUpdateService
{
    private readonly UpdateManifestClient manifestClient;

    [GeneratedConstructor]
    public partial UpdateService(IServiceProvider serviceProvider);

    public string? UpdateInfo { get; private set; }

    public CheckUpdateResult? LastResult { get; private set; }

    public async ValueTask<CheckUpdateResult> CheckUpdateAsync(CancellationToken token = default)
    {
        UpdateManifestFetchResult fetched = await manifestClient.FetchAsync(token).ConfigureAwait(false);

        CheckUpdateResult result = new()
        {
            Manifest = fetched.Manifest,
            Source = fetched.Source,
            Error = fetched.Error,
        };

        if (fetched.Manifest?.ParsedVersion is not { } latest)
        {
            result.Kind = CheckUpdateResultKind.SourceUnavailable;
            UpdateInfo = "检查失败：所有更新源都不可达";
            LastResult = result;
            return result;
        }

        if (latest > EllenRuntime.Version)
        {
            result.Kind = CheckUpdateResultKind.UpdateAvailable;
            UpdateInfo = $"发现新版本 {latest}（当前 {EllenRuntime.Version}）";
            LocalSetting.Set(SettingKeys.UpdateKnownLatestVersion, latest.ToString());
        }
        else
        {
            result.Kind = CheckUpdateResultKind.AlreadyUpdated;
            UpdateInfo = $"已是最新版本（{EllenRuntime.Version}）";
            LocalSetting.Set(SettingKeys.UpdateKnownLatestVersion, string.Empty);
        }

        LastResult = result;
        return result;
    }

    public async ValueTask OpenDownloadPageAsync(CheckUpdateResult result)
    {
        string? url = result.Manifest?.PageUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            url = result.Manifest?.DownloadUrl;
        }

        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return;
        }

        await Windows.System.Launcher.LaunchUriAsync(uri);
    }
}
