// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Domain.GameRecord;
using Ellen.Launcher.Model;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.UI.Xaml.Data;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.HardChallenge;
using System.Collections.Immutable;
using System.Globalization;
using MetadataHardChallengeSchedule = Ellen.Launcher.Model.Metadata.HardChallengeSchedule;

namespace Ellen.Launcher.ViewModel.HardChallenge;

public sealed partial class HardChallengeView : IEntityAccess<HardChallengeEntry?>, IPropertyValuesProvider
{
    private HardChallengeView(HardChallengeEntry entity, HardChallengeMetadataContext context)
        : this(context.IdHardChallengeScheduleMap[entity.ScheduleId], context)
    {
        Entity = entity;

        HardChallengeData hardChallengeData = entity.HardChallengeData;

        ImmutableArray<DataEntryView>.Builder builder = ImmutableArray.CreateBuilder<DataEntryView>(2);

        if (DataEntryView.Create(true, hardChallengeData.SinglePlayer, context) is { } singlePlayer)
        {
            builder.Add(singlePlayer);
        }

        if (DataEntryView.Create(false, hardChallengeData.MultiPlayer, context) is { } multiPlayer)
        {
            builder.Add(multiPlayer);
        }

        DataEntries = builder.ToImmutable().AsAdvancedCollectionView();

        HardChallengeDifficultyLevel maxDifficulty = default;
        if (hardChallengeData.SinglePlayer.HasData)
        {
            maxDifficulty = hardChallengeData.SinglePlayer.Best.Difficulty;
        }

        if (hardChallengeData.MultiPlayer.HasData && hardChallengeData.MultiPlayer.Best.Difficulty > maxDifficulty)
        {
            maxDifficulty = hardChallengeData.MultiPlayer.Best.Difficulty;
        }

        if (maxDifficulty > HardChallengeDifficultyLevel.None)
        {
            MaxDifficulty = maxDifficulty.GetLocalizedDescription(SH.ResourceManager, CultureInfo.CurrentCulture);
        }

        BlingAvatars = hardChallengeData.Blings.SelectAsArray(AvatarBling.Create, context);
        Engaged = true;
    }

    private HardChallengeView(MetadataHardChallengeSchedule hardChallengeSchedule, HardChallengeMetadataContext context)
    {
        ScheduleId = hardChallengeSchedule.Id;
        ScheduleBegin = hardChallengeSchedule.Begin;
        ScheduleName = ScheduleTimeline.ResolveName(hardChallengeSchedule.Name, null);
        FormattedTime = ScheduleTimeline.FormatTimeRange(hardChallengeSchedule.Begin, hardChallengeSchedule.End, SH.ModelEntityScheduleTimeUnknown);
    }

    public uint ScheduleId { get; }

    /// <summary>
    /// 期次开始时间，列表排序与占位卡片插入位置都用它。
    /// </summary>
    public DateTimeOffset ScheduleBegin { get; }

    public string ScheduleName { get; }

    /// <summary>
    /// 期次名称为空（静态表与接口都没给）时走兜底文案，不留空白。
    /// </summary>
    public string Schedule
    {
        get => ScheduleName.Length == 0
            ? SH.FormatModelEntityHardChallengeScheduleUnnamed(ScheduleId - 5269000)
            : SH.FormatModelEntityHardChallengeSchedule(ScheduleId - 5269000, ScheduleName);
    }

    public string FormattedTime { get; }

    public string? MaxDifficulty { get; }

    public bool Engaged { get; }

    public HardChallengeEntry? Entity { get; }

    public IAdvancedCollectionView<DataEntryView>? DataEntries { get; }

    public ImmutableArray<AvatarBling> BlingAvatars { get; } = [];

    public static HardChallengeView Create(HardChallengeEntry entity, HardChallengeMetadataContext context)
    {
        return new(entity, context);
    }

    public static HardChallengeView Create(HardChallengeEntry? entity, MetadataHardChallengeSchedule meta, HardChallengeMetadataContext context)
    {
        return entity is not null ? new(entity, context) : new(meta, context);
    }
}