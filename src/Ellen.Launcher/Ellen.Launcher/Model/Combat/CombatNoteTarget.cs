// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model.Combat;

/// <summary>
/// 战斗备注行所归属的条目。
/// <para>
/// 与 <c>SkillDepot</c> 的对应关系：
/// <c>NormalAttack</c> / <c>ElementalSkill</c> → <c>Skills</c>；
/// <c>ElementalBurst</c> → <c>EnergySkill</c>；
/// <c>InherentN</c> → <c>Inherents[N-1]</c>；
/// <c>ConstellationN</c> → <c>Talents[N-1]</c>。
/// </para>
/// </summary>
public enum CombatNoteTarget
{
    NormalAttack,
    ElementalSkill,
    ElementalBurst,

    /// <summary>
    /// 替代冲刺（如莫娜「虚实流动」、神里绫华「神里流·霰步」）。
    /// 在 <c>SkillDepot.Skills</c> 里是第 3 个技能。
    /// </summary>
    Sprint,

    Inherent1,
    Inherent2,
    Inherent3,
    Constellation1,
    Constellation2,
    Constellation3,
    Constellation4,
    Constellation5,
    Constellation6,
}
