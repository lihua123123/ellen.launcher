// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Model.Combat;
using System.IO;
using System.Net.Http;

namespace Ellen.Launcher.Service.EllenWiki;

/// <summary>
/// 从 ellen.wiki 的公开数据 API 同步数据快照到本地数据目录，供运行期使用。
/// <para>
/// 接口说明见 https://ellen.wiki/#/developer 。同步是「锦上添花」：失败时静默回退到
/// 随应用发布的快照，不影响既有功能。快照写入后触发 <see cref="AttachmentUpdated"/>，
/// 订阅方据此丢弃已缓存的反序列化结果。
/// </para>
/// </summary>
[Service(ServiceLifetime.Singleton)]
[HttpClient(HttpClientConfiguration.Default)]
public sealed partial class EllenWikiDataService
{
    public const string BaseUrl = "https://ellen.wiki";

    private const string AttachmentApiPath = "api/attachment";
    private const string AttachmentFileName = "attachment.json";

    private readonly ILogger<EllenWikiDataService> logger;
    private readonly HttpClient httpClient;

    [GeneratedConstructor]
    public partial EllenWikiDataService(IServiceProvider serviceProvider);

    /// <summary>
    /// 「元素附着及产球」快照更新后触发。
    /// </summary>
    public event EventHandler? AttachmentUpdated;

    /// <summary>
    /// 返回运行期同步得到的「元素附着及产球」快照路径；尚未同步时返回 <see langword="null"/>，
    /// 调用方应回退到随包快照。
    /// </summary>
    public string? GetAttachmentSnapshotPath()
    {
        string path = Path.Combine(EllenRuntime.GetDataEllenWikiDirectory(), AttachmentFileName);
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// 同步全部数据集。启动时在后台调用。
    /// </summary>
    public async ValueTask InitializeAsync(CancellationToken token = default)
    {
        await RefreshAttachmentAsync(token).ConfigureAwait(false);
    }

    private async ValueTask RefreshAttachmentAsync(CancellationToken token)
    {
        try
        {
            string url = $"{BaseUrl}/{AttachmentApiPath}";
            string json = await httpClient.GetStringAsync(url, token).ConfigureAwait(false);

            // 只有能解析成数据集时才写入，避免坏响应污染缓存。
            if (JsonSerializer.Deserialize(json, AvatarCombatNoteJsonContext.Default.EllenWikiAttachmentDataset) is null)
            {
                logger.LogWarning("ellen.wiki returned unparsable attachment data: {Url}", url);
                return;
            }

            string path = Path.Combine(EllenRuntime.GetDataEllenWikiDirectory(), AttachmentFileName);

            // 内容一致时不必重写，也不必触发刷新。
            if (File.Exists(path) && File.ReadAllText(path) == json)
            {
                return;
            }

            await File.WriteAllTextAsync(path, json, token).ConfigureAwait(false);
            logger.LogInformation("Updated ellen.wiki attachment data: {Path}", path);
            AttachmentUpdated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(ex, "Failed to sync ellen.wiki attachment data; using bundled snapshot.");
        }
    }
}
