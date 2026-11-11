// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Service.UIGF;

namespace Ellen.Launcher.Service.Transfer;

/// <summary>
/// 祈愿记录的 UIGF 导入 / 导出。
/// 从「祈愿记录」页的 ViewModel 里抽出来，现在由设置页统一调用，
/// 这样只有一个实现，页面上不再重复摆放同一套按钮。
/// </summary>
public interface IGachaLogTransferService
{
    /// <summary>
    /// 选择 UIGF JSON 文件并按用户勾选的 UID 导入。返回是否真的写入了数据。
    /// </summary>
    ValueTask<bool> ImportAsync();

    /// <summary>
    /// 导出为 UIGF JSON。返回是否真的写出了文件。
    /// </summary>
    ValueTask<bool> ExportAsync(UIGFVersion version);
}
