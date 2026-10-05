using System;

namespace Ellen.Launcher.Domain.Resistance;

/// <summary>
/// 抗性取值来源，数值越大优先级越高。
/// </summary>
public enum ResistanceSourceKind
{
    Default = 0,
    Memory = 1,
    Rule = 2,
    User = 3,
}

/// <summary>
/// 一个带出处的抗性候选值。
/// </summary>
/// <param name="Kind">来源优先级。</param>
/// <param name="Value">抗性值，直接作为 <see cref="ResistanceMath.Coefficient"/> 的入参。</param>
public readonly record struct ResistanceSource(ResistanceSourceKind Kind, double Value)
{
    public static ResistanceSource Default { get; } = new(ResistanceSourceKind.Default, ResistanceTiers.Default);
}

/// <summary>
/// 按优先级挑选抗性入参。这属于领域服务而非引擎：引擎只接受一个已确定的
/// <c>resistance</c> 入参，不查表、不硬编码任何抗性字面量。
/// </summary>
public static class ResistanceResolver
{
    /// <summary>
    /// 在候选中选出优先级最高者；全为 <see langword="null"/> 时回退
    /// <see cref="ResistanceTiers.Default"/>。
    /// </summary>
    public static ResistanceSource Resolve(params ResistanceSource?[] candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        ResistanceSource? best = null;
        foreach (ResistanceSource? candidate in candidates)
        {
            if (candidate is { } value && (best is null || value.Kind > best.Value.Kind))
            {
                best = value;
            }
        }

        return best ?? ResistanceSource.Default;
    }
}
