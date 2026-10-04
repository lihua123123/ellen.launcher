// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Model.Formulas;
using System.IO;

namespace Ellen.Launcher.Service.Formulas;

/// <summary>
/// 从应用目录的 JSON 资源懒加载「计算公式」。
/// <para>
/// 数据由 <c>tools/FormulasGen/gen.mjs</c> 从 ellen-wiki 的
/// <c>content/formulas/&lt;game&gt;.md</c> 生成，随应用发布，因此完全离线可用。
/// 资源缺失或格式错误时只记录日志并返回 <see langword="null"/>，不影响页面的既有功能。
/// </para>
/// </summary>
[Service(ServiceLifetime.Singleton, typeof(IFormulasService))]
public sealed partial class FormulasService : IFormulasService
{
    private readonly ILogger<FormulasService> logger;

    private readonly Dictionary<string, FormulaDocument?> cache = [];

    [GeneratedConstructor]
    public partial FormulasService(IServiceProvider serviceProvider);

    public ValueTask<FormulaDocument?> GetDocumentAsync(string game)
    {
        if (cache.TryGetValue(game, out FormulaDocument? cached))
        {
            return ValueTask.FromResult(cached);
        }

        FormulaDocument? document = Load(game);
        cache[game] = document;
        return ValueTask.FromResult(document);
    }

    private FormulaDocument? Load(string game)
    {
        string path = InstalledLocation.GetAbsolutePath(Path.Combine("Resource", "Formulas", $"{game}Formulas.json"));
        if (!File.Exists(path))
        {
            logger.LogWarning("Formula document not found: {Path}", path);
            return null;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            FormulaDocument? document = JsonSerializer.Deserialize<FormulaDocument>(stream);

            if (document is null || document.Sections.IsDefaultOrEmpty)
            {
                logger.LogWarning("Formula document is empty: {Path}", path);
                return null;
            }

            return document;
        }
        catch (Exception ex)
        {
            // 计算公式属于“锦上添花”，任何读取/解析失败都不应影响页面的既有功能。
            logger.LogError(ex, "Failed to load formula document from {Path}", path);
            return null;
        }
    }
}
