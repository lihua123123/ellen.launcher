namespace Ellen.Launcher.Domain.Resistance;

/// <summary>
/// 抗性元素维度，顺序与 <c>Monster.json</c> 的 <c>BaseValue</c> 字段后缀一一对应
/// （<c>{元素}SubHurt</c>），数组下标即此枚举值。
/// </summary>
public enum ResistanceElement
{
    Physical = 0,
    Fire = 1,
    Water = 2,
    Grass = 3,
    Elec = 4,
    Wind = 5,
    Ice = 6,
    Rock = 7,
}
