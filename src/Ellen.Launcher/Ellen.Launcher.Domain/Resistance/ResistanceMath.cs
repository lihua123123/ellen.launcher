namespace Ellen.Launcher.Domain.Resistance;

/// <summary>
/// 抗性分段函数。引擎只做算术，不含查表；<c>r</c> 由调用方作为入参提供。
/// </summary>
public static class ResistanceMath
{
    /// <summary>
    /// 抗性乘区系数 <c>k(r)</c>：<c>r ≤ 0 → 1 - r/2</c>；
    /// <c>0 &lt; r &lt; 0.75 → 1 - r</c>；<c>r ≥ 0.75 → 1 / (4r + 1)</c>。
    /// </summary>
    /// <remarks>
    /// 两处在 <c>r = 0</c> 与 <c>r = 0.75</c> 连续（均为 1 与 0.25）。
    /// 实测取值域包含 <c>r = 1.35</c> 与 <c>r = 2.0</c>，落在第三段。
    /// </remarks>
    public static double Coefficient(double resistance)
    {
        if (resistance < 0)
        {
            return 1 - (resistance / 2);
        }

        if (resistance < 0.75)
        {
            return 1 - resistance;
        }

        return 1 / ((4 * resistance) + 1);
    }
}
