// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.UI.Xaml.Data.Converter;

namespace Ellen.Launcher.Model.Metadata.Converter;

/// <summary>
/// 图鉴头像转换器：图标名 → 随包发布的本地 PNG。
/// <para>
/// 与 <see cref="MonsterIconConverter"/> 的区别是数据源：图鉴用上游 GIEnemyChartXML 的目录表头像，
/// 其中有近一半（动物 / 活动 / 道具图标）静态服务器上并没有，只能随包发布，
/// 因此这里走 <c>ms-appx:///Resource/EnemyArchives/MonsterIcons/</c>。
/// </para>
/// </summary>
public sealed partial class MonsterCatalogIconConverter : ValueConverter<string, Uri>
{
    private const string Root = "ms-appx:///Resource/EnemyArchives/MonsterIcons";

    public static Uri IconNameToUri(string name)
    {
        return new Uri($"{Root}/{name}.png");
    }

    public override Uri Convert(string from)
    {
        return IconNameToUri(from);
    }
}
