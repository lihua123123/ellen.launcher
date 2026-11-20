// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.ViewModel.Guide;

/// <summary>
/// 指引状态
/// </summary>
public enum GuideState : uint
{
    /// <summary>
    /// 选择语言
    /// </summary>
    Language,

    /// <summary>
    /// 查看文档与隐私政策
    /// </summary>
    Document,

    /// <summary>
    /// 查看环境配置
    /// </summary>
    Environment,

    /// <summary>
    /// 查看数据文件夹
    /// </summary>
    DataFolder,

    /// <summary>
    /// 查看常用设置
    /// </summary>
    CommonSetting,

    /// <summary>
    /// 查看图像资源设置（已下线）
    /// <para>
    /// 图片改为逐张走 R2 后不再有「选画质/档案」这一步，界面已删除。
    /// 数值保留：老版本的 settings.json 里可能还存着 5，一旦重排就会错位。
    /// </para>
    /// </summary>
    StaticResourceSetting,

    /// <summary>
    /// 开始下载图像资源（已下线）
    /// <para>同上，数值仅为兼容历史存档保留；读到时会自动推进到 <see cref="Completed"/>。</para>
    /// </summary>
    StaticResourceBegin,

    /// <summary>
    /// 完成
    /// </summary>
    Completed,
}