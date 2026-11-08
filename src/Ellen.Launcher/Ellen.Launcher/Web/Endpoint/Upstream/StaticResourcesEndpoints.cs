// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Endpoint.Upstream;

public static class StaticResourcesEndpoints
{
    public static string Root { get => ServerDomain.GetApiRoot(); }

    public static Uri UIIconNone { get => StaticRaw("Bg", "UI_Icon_None.png").ToUri(); }

    public static Uri UIItemIconNone { get => StaticRaw("Bg", "UI_ItemIcon_None.png").ToUri(); }

    public static Uri UIAvatarIconSideNone { get => StaticRaw("AvatarIcon", "UI_AvatarIcon_Side_None.png").ToUri(); }

    public static string StaticRaw(string category, string fileName)
    {
        /* 自建数据站补了 Snap CDN 没有的资源（新角色的侧头像 / 名片 / 天赋 / 技能等），
           索引命中就走镜像；未命中会回退 Snap，但**必须记账**（P4：不允许静默回退），
           诊断页按分类展示回退次数。 */
        if (LauncherAssetMirror.TryResolve(category, fileName, out string? mirrored) && mirrored is not null)
        {
            return mirrored;
        }

        LauncherAssetMirror.RecordFallback(category);
        return string.Intern($"{Root}/static/raw/{category}/{fileName}");
    }

    public static string StaticZip(string fileName)
    {
        return $"{Root}/static/zip/{fileName}.zip";
    }

    public static string StaticSize()
    {
        return $"{Root}/static/size";
    }
}