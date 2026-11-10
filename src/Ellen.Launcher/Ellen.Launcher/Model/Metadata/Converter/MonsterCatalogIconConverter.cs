// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Domain.WikiSource;
using Ellen.Launcher.UI.Xaml.Data.Converter;
using System;
using System.IO;

namespace Ellen.Launcher.Model.Metadata.Converter;

/// <summary>
/// 图鉴头像转换器。
/// <para>
/// legacy-722-bundled：图标名 → 随包 PNG（<c>ms-appx:///Resource/EnemyArchives/MonsterIcons/</c>）。
/// 与 <see cref="MonsterIconConverter"/> 的区别是数据源：图鉴用上游 GIEnemyChartXML 的目录表头像，
/// 其中近一半（动物 / 活动 / 道具图标）静态服务器上没有，只能随包发布。
/// </para>
/// <para>
/// P2 enemy-web-753：输入改为 ellen.wiki 的图片 URI 或 <c>id/file</c> 缓存键。
/// 优先查内容寻址缓存（<c>&lt;cacheRoot&gt;/&lt;id&gt;/&lt;file&gt;</c>），未命中用在线 URL；
/// 空值 / 非法值降级占位图，由 WikiImageCache 记录诊断，不阻塞页面。不打包图片（P4）。
/// </para>
/// </summary>
public sealed partial class MonsterCatalogIconConverter : ValueConverter<string, Uri>
{
    private const string Root = "ms-appx:///Resource/EnemyArchives/MonsterIcons";

    /// <summary>P2 图片缓存根目录；由 WikiMirrorPaths 提供，空表示只走在线 URL。</summary>
    public static string ImageCacheRoot { get; set; } = string.Empty;

    public static Uri IconNameToUri(string name)
    {
        return new Uri($"{Root}/{name}.png");
    }

    public override Uri Convert(string from)
    {
        return IconNameToUri(from);
    }

    /// <summary>P2：在线 URL + 内容寻址缓存 → 可绑定 URI；失败降级占位图。</summary>
    public static Uri WebImageToUri(string? uriOrKey, string enemyId, string file)
    {
        if (string.IsNullOrWhiteSpace(uriOrKey))
        {
            return new Uri(WikiImageCache.PlaceholderUri);
        }

        // 本地缓存优先（内容寻址：id + file；P4 补 hash 后键变为 id/file:hash）。
        if (!string.IsNullOrEmpty(ImageCacheRoot))
        {
            string local = Path.Combine(ImageCacheRoot, enemyId, file);
            if (File.Exists(local))
            {
                return new Uri(local);
            }
        }

        return Uri.TryCreate(uriOrKey, UriKind.Absolute, out Uri? absolute)
            ? absolute
            : new Uri(WikiImageCache.PlaceholderUri);
    }
}
