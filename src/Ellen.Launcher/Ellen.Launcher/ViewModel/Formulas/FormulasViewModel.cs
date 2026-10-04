// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Model.Formulas;
using Ellen.Launcher.Service.Formulas;
using System.Collections.Immutable;

namespace Ellen.Launcher.ViewModel.Formulas;

/// <summary>
/// 「计算公式」页面的视图模型：只负责把随应用发布的 <c>GenshinFormulas.json</c> 读出来。
/// </summary>
[BindableCustomPropertyProvider]
[Service(ServiceLifetime.Scoped)]
public sealed partial class FormulasViewModel : Abstraction.ViewModel
{
    private readonly IFormulasService formulasService;

    [GeneratedConstructor]
    public partial FormulasViewModel(IServiceProvider serviceProvider);

    /// <summary>
    /// 章节列表（按文档顺序）。
    /// </summary>
    [ObservableProperty]
    public partial ImmutableArray<FormulaSection> Sections { get; set; } = [];

    /// <summary>
    /// 页面标题，例如「原神 · 伤害计算公式」。
    /// </summary>
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    /// <summary>
    /// 数据加载失败（资源缺失 / 解析错误）时为 <see langword="false"/>，页面展示空状态。
    /// </summary>
    [ObservableProperty]
    public partial bool HasContent { get; set; }

    protected override async ValueTask<bool> LoadOverrideAsync(CancellationToken token)
    {
        FormulaDocument? document = await formulasService.GetDocumentAsync("Genshin").ConfigureAwait(true);
        if (document is null)
        {
            HasContent = false;
            return true;
        }

        Title = document.Title;
        Sections = document.Sections;
        HasContent = !Sections.IsDefaultOrEmpty;
        return true;
    }
}
