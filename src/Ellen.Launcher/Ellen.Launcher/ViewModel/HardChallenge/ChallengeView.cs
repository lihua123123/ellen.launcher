// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Endgame;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.HardChallenge;
using System.Collections.Immutable;

namespace Ellen.Launcher.ViewModel.HardChallenge;

/// <summary>
/// 幽境危战里单个 Boss 的一张卡片。
/// <para>
/// 有挑战记录时展示个人成绩（用时 / 阵容 / 最强一击 / 最高总伤害），
/// 无论有没有记录都会带上内置攻略（Boss 图片、N5/N6 血量、机制、介绍与背景）。
/// </para>
/// </summary>
public sealed class ChallengeView
{
    private ChallengeView(HardChallengeChallenge challenge, HardChallengeMetadataContext context)
    {
        Name = challenge.Name;
        FormattedSeconds = SH.FormatViewModelHardChallengeSeconds(challenge.Seconds);
        Avatars = challenge.Team.SelectAsArray(AvatarView.Create, context);
        BestAvatars = challenge.BestAvatars.SelectAsArray(AvatarDamage.Create, context);
        Monster = MonsterView.Create(challenge.Monster, context);
        HasRecord = true;

        ApplyGuide(HardChallengeBossGuideLookup.Find(context.BossGuidePeriods, challenge.Monster.Name));
    }

    /// <summary>
    /// 仅来自内置攻略的卡片（未登录 / 该期没有记录时使用）。
    /// </summary>
    private ChallengeView(HardChallengeBossGuideBoss boss)
    {
        Name = string.Empty;
        FormattedSeconds = string.Empty;
        Avatars = [];
        BestAvatars = [];
        Monster = null;
        HasRecord = false;

        ApplyGuide(boss);
    }

    public string Name { get; }

    public string FormattedSeconds { get; }

    public ImmutableArray<AvatarView> Avatars { get; }

    public ImmutableArray<AvatarDamage> BestAvatars { get; }

    public MonsterView? Monster { get; }

    public bool HasRecord { get; }

    public bool HasMonster => Monster is not null;

    /// <summary>
    /// 是否展示个人数据区（用时 / 阵容 / 伤害）。
    /// </summary>
    public bool ShowRecord => HasRecord;

    /// <summary>
    /// 是否展示「暂无记录」占位。
    /// </summary>
    public bool ShowRecordPlaceholder => !HasRecord;

    /// <summary>
    /// 匹配到的内置攻略 Boss，未匹配到时为 <see langword="null"/>。
    /// </summary>
    public HardChallengeBossGuideBoss? Boss { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string? SecondaryName { get; private set; }

    public Uri? GuideImageUri { get; private set; }

    public bool HasGuideImage => GuideImageUri is not null;

    /// <summary>
    /// 没有内置配图但有记录里的怪物图标时，回退到远程图标。
    /// </summary>
    public bool ShowRemoteImage => !HasGuideImage && HasMonster;

    public bool ShowImagePlaceholder => !HasGuideImage && !HasMonster;

    public string? HpN5 { get; private set; }

    public string? HpN6 { get; private set; }

    public bool HasHp => !string.IsNullOrEmpty(HpN5) || !string.IsNullOrEmpty(HpN6);

    /// <summary>
    /// 「机制」段，卡片内直接展示。
    /// </summary>
    public HardChallengeGuideSection? Mechanism { get; private set; }

    public bool HasMechanism => Mechanism is { Lines.Length: > 0 };

    /// <summary>
    /// 「介绍」与「背景」段，卡片详情浮出层里展示。
    /// </summary>
    public ImmutableArray<HardChallengeGuideSection> DetailSections { get; private set; } = [];

    public bool HasDetail => !DetailSections.IsDefaultOrEmpty;

    public static ChallengeView Create(HardChallengeChallenge challenge, HardChallengeMetadataContext context)
    {
        return new(challenge, context);
    }

    public static ChallengeView FromGuide(HardChallengeBossGuideBoss boss)
    {
        return new(boss);
    }

    private void ApplyGuide(HardChallengeBossGuideBoss? boss)
    {
        Boss = boss;
        DisplayName = boss?.FullName ?? Monster?.Name ?? string.Empty;
        SecondaryName = boss?.ShortName;
        GuideImageUri = boss?.ImageUri;
        HpN5 = boss?.HpN5;
        HpN6 = boss?.HpN6;
        Mechanism = boss?.Mechanism;
        DetailSections = boss?.DetailSections ?? [];
    }
}