// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.Transfer;

/// <summary>
/// 成就的 UIAF 导入 / 导出。原先散落在「成就」页的工具栏里，现在统一由设置页调用。
/// 具体解析与写库仍在 <see cref="ViewModel.Achievement.AchievementImporter"/> 里，这里只做入口收敛。
/// </summary>
public interface IAchievementTransferService
{
    ValueTask<bool> ImportFromFileAsync();

    ValueTask<bool> ImportFromClipboardAsync();

    /// <summary>
    /// 从游戏内嵌 Yae 读取成就。需要管理员权限，首次使用还要用户确认注入风险。
    /// </summary>
    ValueTask<bool> ImportFromEmbeddedYaeAsync();

    /// <summary>
    /// 把当前选中的成就存档导出成 UIAF JSON。
    /// </summary>
    ValueTask<bool> ExportAsync();
}
