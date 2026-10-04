// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Formulas;

namespace Ellen.Launcher.Service.Formulas;

/// <summary>
/// 提供随应用发布的「计算公式」数据（离线可用，无需登录）。
/// </summary>
public interface IFormulasService
{
    /// <summary>
    /// 获取指定游戏的计算公式文档；资源缺失或解析失败时返回 <see langword="null"/>。
    /// </summary>
    ValueTask<FormulaDocument?> GetDocumentAsync(string game);
}
