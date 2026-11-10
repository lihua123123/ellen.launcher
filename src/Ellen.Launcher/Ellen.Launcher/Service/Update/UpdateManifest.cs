// Ellen.Launcher
// 更新清单 latest.json —— 与具体托管平台解耦：GitCode / GitHub / COS 放同一份内容，
// 客户端按顺序取第一个能读到的，谁先响应谁生效。

namespace Ellen.Launcher.Service.Update;

/// <summary>
/// 版本清单。所有字段都可缺省，缺字段只影响展示，不影响版本比较。
/// </summary>
public sealed class UpdateManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("publishedAt")]
    public string? PublishedAt { get; set; }

    /// <summary>
    /// 给人看的发布页（GitHub Releases 页面或等价的镜像页）。
    /// </summary>
    [JsonPropertyName("pageUrl")]
    public string? PageUrl { get; set; }

    /// <summary>
    /// 安装包直链。缺失时只提供发布页跳转。
    /// </summary>
    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonIgnore]
    public System.Version? ParsedVersion
    {
        get
        {
            // 清单里写 "7.2.0.3"；System.Version 至少需要两段，这里统一补成四段。
            string text = Version.Trim();
            if (text.Length == 0)
            {
                return null;
            }

            return System.Version.TryParse(text, out System.Version? parsed) ? parsed : null;
        }
    }
}
