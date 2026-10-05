namespace Ellen.Launcher.Domain.Resistance;

/// <summary>
/// 兜底档位常量。主源是随包元数据 <c>Monster.json</c> 的 <c>BaseValue.*SubHurt</c>（474/562），
/// 本表只服务缺 <c>BaseValue</c> 的 88 条与「无来源」默认值。
/// </summary>
/// <remarks>
/// 值取自 <c>Resource/CombatNotes/ResistanceTiers.json</c>，该文件与本文档同源同版本。
/// </remarks>
public static class ResistanceTiers
{
    /// <summary>通用档，同时是「无来源」时的默认值。</summary>
    public const double Universal = 0.10;

    /// <summary>对自身元素高抗。实测常见 0.5。</summary>
    public const double ElementalHigh = 0.40;

    /// <summary>完全免疫。实测存在 1.35 / 2.0 的超免疫，仍走第三段。</summary>
    public const double Immune = 1.00;

    /// <summary>负抗特例。实测存在 -0.4 / -0.5。</summary>
    public const double Negative = -0.20;

    /// <summary>
    /// 默认值站在安全侧：低估伤害而非高估。低估是可用性问题，高估是信誉问题。
    /// </summary>
    public const double Default = Universal;
}
