// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using System.Globalization;

namespace Ellen.Launcher.Service.AvatarInfo.Factory;

/// <summary>
/// 计算圣遗物副词条的「强化数」。
/// <para>
/// 把「一次满档强化」定义为 1：暴击率 3.3%、暴击伤害 6.6%、元素精通 19.8、
/// 元素充能效率 5.5%、生命值 5%、攻击力 5%、防御力 6.2%。
/// 副词条数值除以对应基准即得该词条的强化数。
/// </para>
/// <para>
/// 固定生命值/攻击力/防御力先除以角色对应的白值折算成百分比，再按百分比基准计算。
/// </para>
/// </summary>
public static class ReliquaryEnhancementCalculator
{
    private const double CritRateUnit = 3.3;
    private const double CritHurtUnit = 6.6;
    private const double ElementalMasteryUnit = 19.8;
    private const double ChargeEfficiencyUnit = 5.5;
    private const double HpPercentUnit = 5.0;
    private const double AttackPercentUnit = 5.0;
    private const double DefensePercentUnit = 6.2;

    public static double Calculate(
        FightProperty propertyType,
        string value,
        double baseHp,
        double baseAttack,
        double baseDefense)
    {
        return Calculate(propertyType, ParseValue(value), baseHp, baseAttack, baseDefense);
    }

    public static double Calculate(
        FightProperty propertyType,
        double value,
        double baseHp,
        double baseAttack,
        double baseDefense)
    {
        return propertyType switch
        {
            FightProperty.FIGHT_PROP_CRITICAL => value / CritRateUnit,
            FightProperty.FIGHT_PROP_CRITICAL_HURT => value / CritHurtUnit,
            FightProperty.FIGHT_PROP_ELEMENT_MASTERY => value / ElementalMasteryUnit,
            FightProperty.FIGHT_PROP_CHARGE_EFFICIENCY => value / ChargeEfficiencyUnit,
            FightProperty.FIGHT_PROP_HP_PERCENT => value / HpPercentUnit,
            FightProperty.FIGHT_PROP_ATTACK_PERCENT => value / AttackPercentUnit,
            FightProperty.FIGHT_PROP_DEFENSE_PERCENT => value / DefensePercentUnit,
            FightProperty.FIGHT_PROP_HP => ToPercent(value, baseHp) / HpPercentUnit,
            FightProperty.FIGHT_PROP_ATTACK => ToPercent(value, baseAttack) / AttackPercentUnit,
            FightProperty.FIGHT_PROP_DEFENSE => ToPercent(value, baseDefense) / DefensePercentUnit,
            _ => 0,
        };
    }

    private static double ToPercent(double value, double baseValue)
    {
        return baseValue > 0 ? value / baseValue * 100.0 : 0;
    }

    private static double ParseValue(string value)
    {
        if (value.EndsWith('%'))
        {
            value = value[..^1];
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? result : 0;
    }
}
