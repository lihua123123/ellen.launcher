// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.GachaLog;

/// <summary>
/// 卡池素材（<c>GachaEvent</c> 与卡池图）的后台自动更新：从上游 Snap.Metadata 拉取，
/// 合并进本地缓存，使新版本卡池无需发版即可出现在「祈愿记录」里。
/// </summary>
public interface IGachaEventAutoUpdater
{
    /// <summary>
    /// 在后台尝试刷新一次卡池数据，内部自带节流与异常吞掉，调用方无需等待或处理失败。
    /// </summary>
    ValueTask UpdateInBackgroundAsync(CancellationToken token = default);
}
