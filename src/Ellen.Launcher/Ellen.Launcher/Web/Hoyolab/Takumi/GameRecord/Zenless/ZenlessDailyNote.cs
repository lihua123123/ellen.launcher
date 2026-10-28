// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.Zenless;

/// <summary>
/// 绝区零的实时便笺（电量 / 活跃度 / 丽都周纪 / 悬赏委托 / 录像店 / 打卡）。
/// </summary>
public sealed class ZenlessDailyNote
{
    [JsonPropertyName("energy")]
    public ZenlessResource Energy { get; init; } = new();

    /// <summary>
    /// 今日活跃度。接口字段叫 vitality，但语义是每日活跃度（不是电量），
    /// 而且是平铺的 current/max（不嵌在 progress 里）。
    /// </summary>
    [JsonPropertyName("vitality")]
    public ZenlessProgress Vitality { get; init; } = new();

    [JsonPropertyName("vhs_sale")]
    public ZenlessVhsSale VhsSale { get; init; } = new();

    [JsonPropertyName("card_sign")]
    public string CardSign { get; init; } = string.Empty;

    /// <summary>
    /// 绳网会员（每日菲林领取状态 + 剩余天数）。
    /// </summary>
    [JsonPropertyName("member_card")]
    public ZenlessMemberCard MemberCard { get; init; } = new();

    /// <summary>
    /// 随便观经营（游历 / 制造 / 售卖 + 百通宝）。未解锁该玩法时接口可能不返回。
    /// </summary>
    [JsonPropertyName("temple_running")]
    public ZenlessTempleRunning? TempleRunning { get; init; }

    /// <summary>
    /// 悬赏委托进度。
    /// </summary>
    [JsonPropertyName("bounty_commission")]
    public ZenlessBountyCommission BountyCommission { get; init; } = new();

    /// <summary>
    /// 丽都周纪获取积分（周常）。
    /// </summary>
    [JsonPropertyName("weekly_task")]
    public ZenlessPointProgress WeeklyTask { get; init; } = new();

    [JsonIgnore]
    public string FormattedEnergy
    {
        get => $"{Energy.Progress.Current}/{Energy.Progress.Max}";
    }

    [JsonIgnore]
    public int EnergyCurrent
    {
        get => Energy.Progress.Current;
    }

    /// <summary>
    /// 进度条的 Maximum 不能为 0，否则控件会画成「已满」。
    /// </summary>
    [JsonIgnore]
    public int EnergyMax
    {
        get => Math.Max(Energy.Progress.Max, 1);
    }

    [JsonIgnore]
    public string EnergyLabel
    {
        get => SH.ViewPageDailyNoteZenlessEnergy;
    }

    /// <summary>
    /// 电量下方的说明：满值时提示已满，否则给出恢复满值的时刻。
    /// 不再重复「电量 · 」前缀，行标签已经写在下面一行。
    /// </summary>
    [JsonIgnore]
    public string EnergyDescription
    {
        get
        {
            if (Energy.Restore <= 0)
            {
                return SH.ViewPageDailyNoteZenlessEnergyFull;
            }

            return SH.FormatViewPageDailyNoteZenlessEnergyRecovering(DateTimeOffset.Now.AddSeconds(Energy.Restore));
        }
    }

    [JsonIgnore]
    public string FormattedVitality
    {
        get => $"{Vitality.Current}/{Vitality.Max}";
    }

    [JsonIgnore]
    public int VitalityCurrent
    {
        get => Vitality.Current;
    }

    [JsonIgnore]
    public int VitalityMax
    {
        get => Math.Max(Vitality.Max, 1);
    }

    [JsonIgnore]
    public string VitalityLabel
    {
        get => SH.ViewPageDailyNoteZenlessVitality;
    }

    [JsonIgnore]
    public string FormattedWeeklyTask
    {
        get => $"{WeeklyTask.Current}/{WeeklyTask.Max}";
    }

    [JsonIgnore]
    public int WeeklyTaskCurrent
    {
        get => WeeklyTask.Current;
    }

    [JsonIgnore]
    public int WeeklyTaskMax
    {
        get => Math.Max(WeeklyTask.Max, 1);
    }

    [JsonIgnore]
    public string WeeklyTaskLabel
    {
        get => SH.ViewPageDailyNoteZenlessWeeklyTask;
    }

    [JsonIgnore]
    public string FormattedBountyCommission
    {
        get => $"{BountyCommission.Num}/{BountyCommission.Total}";
    }

    [JsonIgnore]
    public int BountyNum
    {
        get => BountyCommission.Num;
    }

    [JsonIgnore]
    public int BountyTotal
    {
        get => Math.Max(BountyCommission.Total, 1);
    }

    [JsonIgnore]
    public string BountyLabel
    {
        get => SH.ViewPageDailyNoteZenlessBounty;
    }

    /// <summary>
    /// 录像店今日是否已营业完成。
    /// </summary>
    [JsonIgnore]
    public bool IsVhsSaleDone
    {
        get => VhsSale.IsDone;
    }

    /// <summary>
    /// 录像店经营状态：正在营业（SaleStateDoing）/ 待结算（SaleStateDone）。
    /// </summary>
    [JsonIgnore]
    public string VhsSaleText
    {
        get => IsVhsSaleDone ? SH.ViewPageDailyNoteZenlessVhsSaleSettling : SH.ViewPageDailyNoteZenlessVhsSaleOpen;
    }

    [JsonIgnore]
    public int VhsSaleProgress
    {
        get => IsVhsSaleDone ? 1 : 0;
    }

    [JsonIgnore]
    public string VhsSaleLabel
    {
        get => SH.ViewPageDailyNoteZenlessVhsSale;
    }

    /// <summary>
    /// 今日是否已打卡。
    /// </summary>
    [JsonIgnore]
    public bool IsCardSigned
    {
        get => string.Equals(CardSign, "CardSignDone", StringComparison.Ordinal);
    }

    [JsonIgnore]
    public string CardSignText
    {
        get => IsCardSigned ? SH.ViewPageDailyNoteZenlessTaskDone : SH.ViewPageDailyNoteZenlessTaskUndone;
    }

    [JsonIgnore]
    public int CardSignProgress
    {
        get => IsCardSigned ? 1 : 0;
    }

    /// <summary>
    /// 每日一次的三选一：饼铺盲盒 / 刮刮卡 / 占卜（三者共享每日次数，故只有一个是非状态）。
    /// </summary>
    [JsonIgnore]
    public string CardSignLabel
    {
        get => SH.ViewPageDailyNoteZenlessDailyLottery;
    }

    /// <summary>
    /// 是否开通绳网会员（未开通时不显示剩余天数）。
    /// </summary>
    [JsonIgnore]
    public bool IsMemberCardOpen
    {
        get => MemberCard.IsOpen && MemberCardRemainingDays > 0;
    }

    /// <summary>
    /// 绳网会员剩余可领取天数（接口给的是剩余秒数）。
    /// </summary>
    [JsonIgnore]
    public int MemberCardRemainingDays
    {
        get => (int)Math.Ceiling(MemberCard.RemainingSeconds / 86400D);
    }

    [JsonIgnore]
    public string MemberCardValue
    {
        get => IsMemberCardOpen
            ? SH.FormatViewPageDailyNoteZenlessMemberCardRemaining(MemberCardRemainingDays)
            : SH.ViewPageDailyNoteZenlessMemberCardNotOpened;
    }

    /// <summary>
    /// 绳网会员说明：已开通时附带「今日菲林」领取状态。
    /// </summary>
    [JsonIgnore]
    public string MemberCardDescription
    {
        get
        {
            if (!IsMemberCardOpen)
            {
                return SH.ViewPageDailyNoteZenlessMemberCard;
            }

            string state = MemberCard.IsClaimed
                ? SH.ViewPageDailyNoteZenlessMemberCardClaimed
                : SH.ViewPageDailyNoteZenlessMemberCardUnclaimed;

            return $"{SH.ViewPageDailyNoteZenlessMemberCard} · {state}";
        }
    }

    /// <summary>
    /// 是否解锁了随便观经营（未解锁时不显示百通宝与游历/制造/售卖）。
    /// </summary>
    [JsonIgnore]
    public bool HasTemple
    {
        get => TempleRunning is not null;
    }

    [JsonIgnore]
    public string TempleHeader
    {
        get => SH.FormatViewPageDailyNoteZenlessTempleHeader(TempleRunning?.Level ?? 0);
    }

    /// <summary>
    /// 百通宝：本周获取量 / 周上限。
    /// </summary>
    [JsonIgnore]
    public string FormattedCurrency
    {
        get => $"{TempleRunning?.Currency ?? 0}/{TempleRunning?.WeeklyCurrencyMax ?? 0}";
    }

    [JsonIgnore]
    public int CurrencyCurrent
    {
        get => TempleRunning?.Currency ?? 0;
    }

    [JsonIgnore]
    public int CurrencyMax
    {
        get => Math.Max(TempleRunning?.WeeklyCurrencyMax ?? 0, 1);
    }

    [JsonIgnore]
    public string CurrencyLabel
    {
        get => SH.ViewPageDailyNoteZenlessCurrencyWeekly;
    }

    /// <summary>
    /// 随便观 · 游历状态。
    /// </summary>
    [JsonIgnore]
    public string ExpeditionStateText
    {
        get => LocalizeTempleState(
            TempleRunning?.ExpeditionState,
            SH.ViewPageDailyNoteZenlessTempleExpeditionCanSend,
            SH.ViewPageDailyNoteZenlessTempleExpeditionDoing,
            SH.ViewPageDailyNoteZenlessTempleExpeditionDone);
    }

    /// <summary>
    /// 随便观 · 制造状态。
    /// </summary>
    [JsonIgnore]
    public string BenchStateText
    {
        get => LocalizeTempleState(
            TempleRunning?.BenchState,
            SH.ViewPageDailyNoteZenlessTempleBenchCanProduce,
            SH.ViewPageDailyNoteZenlessTempleBenchProducing,
            SH.ViewPageDailyNoteZenlessTempleBenchDone);
    }

    /// <summary>
    /// 随便观 · 售卖状态（货架空了显示「待补货」）。
    /// </summary>
    [JsonIgnore]
    public string ShelveStateText
    {
        get => LocalizeTempleState(
            TempleRunning?.ShelveState,
            SH.ViewPageDailyNoteZenlessTempleShelveCanSell,
            SH.ViewPageDailyNoteZenlessTempleShelveSelling,
            SH.ViewPageDailyNoteZenlessTempleShelveRestock,
            SH.ViewPageDailyNoteZenlessTempleShelveRestock);
    }

    /// <summary>
    /// 把接口的枚举字符串（如 ExpeditionStateInCanSend）折算成一句人话。
    /// 接口没有公开完整枚举，按关键字匹配，匹配不上一律显示占位符，避免界面出现英文枚举。
    /// </summary>
    private static string LocalizeTempleState(string? state, string can, string doing, string done, string? empty = null)
    {
        if (string.IsNullOrEmpty(state))
        {
            return SH.ViewPageDailyNoteZenlessTempleStateUnknown;
        }

        if (empty is not null
            && (state.Contains("Empty", StringComparison.OrdinalIgnoreCase)
                || state.Contains("NoGoods", StringComparison.OrdinalIgnoreCase)))
        {
            return empty;
        }

        if (state.Contains("Receive", StringComparison.OrdinalIgnoreCase)
            || state.Contains("Claim", StringComparison.OrdinalIgnoreCase)
            || state.Contains("Done", StringComparison.OrdinalIgnoreCase)
            || state.Contains("Finish", StringComparison.OrdinalIgnoreCase))
        {
            return done;
        }

        if (state.Contains("Doing", StringComparison.OrdinalIgnoreCase)
            || state.Contains("Progress", StringComparison.OrdinalIgnoreCase)
            || state.Contains("Running", StringComparison.OrdinalIgnoreCase)
            || state.Contains("Selling", StringComparison.OrdinalIgnoreCase)
            || state.Contains("Producing", StringComparison.OrdinalIgnoreCase)
            || state.Contains("Sending", StringComparison.OrdinalIgnoreCase))
        {
            return doing;
        }

        if (state.Contains("Can", StringComparison.OrdinalIgnoreCase))
        {
            return can;
        }

        return SH.ViewPageDailyNoteZenlessTempleStateUnknown;
    }
}

/// <summary>
/// 电量这类「当前值 + 恢复时间」的资源。
/// </summary>
public sealed class ZenlessResource
{
    [JsonPropertyName("progress")]
    public ZenlessProgress Progress { get; init; } = new();

    /// <summary>
    /// 距离电量回满的秒数，已满时为 0。
    /// </summary>
    [JsonPropertyName("restore")]
    public int Restore { get; init; }
}

public sealed class ZenlessProgress
{
    [JsonPropertyName("current")]
    public int Current { get; init; }

    [JsonPropertyName("max")]
    public int Max { get; init; }
}

/// <summary>
/// 录像店经营状态。
/// </summary>
public sealed class ZenlessVhsSale
{
    [JsonPropertyName("sale_state")]
    public string SaleState { get; init; } = string.Empty;

    [JsonIgnore]
    public bool IsDone
    {
        get => string.Equals(SaleState, "SaleStateDone", StringComparison.Ordinal);
    }
}

/// <summary>
/// 悬赏委托。
/// </summary>
public sealed class ZenlessBountyCommission
{
    [JsonPropertyName("num")]
    public int Num { get; init; }

    [JsonPropertyName("total")]
    public int Total { get; init; }
}

/// <summary>
/// 活跃度这类「积分」进度。
/// </summary>
public sealed class ZenlessPointProgress
{
    [JsonPropertyName("cur_point")]
    public int Current { get; init; }

    [JsonPropertyName("max_point")]
    public int Max { get; init; }
}

/// <summary>
/// 绳网会员：每日菲林领取状态 + 剩余可领取天数。
/// </summary>
public sealed class ZenlessMemberCard
{
    /// <summary>
    /// 是否开通了绳网会员。
    /// </summary>
    [JsonPropertyName("is_open")]
    public bool IsOpen { get; init; }

    /// <summary>
    /// 领取状态。接口未公开完整枚举，实测未领取为 MemberCardStateNo。
    /// </summary>
    [JsonPropertyName("member_card_state")]
    public string State { get; init; } = string.Empty;

    /// <summary>
    /// 剩余可领取秒数。接口给的是字符串（如 "4924298"），个别版本给数字，两种都兼容。
    /// </summary>
    [JsonPropertyName("exp_time")]
    public JsonElement ExpTime { get; init; }

    [JsonIgnore]
    public long RemainingSeconds
    {
        get => ZenlessNoteJson.ReadInt64(ExpTime);
    }

    [JsonIgnore]
    public bool IsClaimed
    {
        get => !string.Equals(State, "MemberCardStateNo", StringComparison.Ordinal);
    }
}

/// <summary>
/// 随便观经营：游历 / 制造 / 售卖 + 百通宝。
/// </summary>
public sealed class ZenlessTempleRunning
{
    [JsonPropertyName("expedition_state")]
    public string ExpeditionState { get; init; } = string.Empty;

    [JsonPropertyName("bench_state")]
    public string BenchState { get; init; } = string.Empty;

    [JsonPropertyName("shelve_state")]
    public string ShelveState { get; init; } = string.Empty;

    /// <summary>
    /// 随便观复兴等级。
    /// </summary>
    [JsonPropertyName("level")]
    public int Level { get; init; }

    /// <summary>
    /// 百通宝周上限，接口给的是字符串（如 "5000"）。
    /// </summary>
    [JsonPropertyName("weekly_currency_max")]
    public JsonElement WeeklyCurrencyMaxRaw { get; init; }

    /// <summary>
    /// 本周已获取的百通宝，接口给的是字符串（如 "0"）。
    /// </summary>
    [JsonPropertyName("current_currency")]
    public JsonElement CurrentCurrencyRaw { get; init; }

    [JsonIgnore]
    public int WeeklyCurrencyMax
    {
        get => (int)ZenlessNoteJson.ReadInt64(WeeklyCurrencyMaxRaw);
    }

    [JsonIgnore]
    public int Currency
    {
        get => (int)ZenlessNoteJson.ReadInt64(CurrentCurrencyRaw);
    }
}

/// <summary>
/// 便笺里那些「可能是字符串也可能是数字」的字段。
/// </summary>
internal static class ZenlessNoteJson
{
    public static long ReadInt64(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => long.TryParse(element.GetString(), out long parsed) ? parsed : 0L,
            JsonValueKind.Number => element.TryGetInt64(out long parsed) ? parsed : 0L,
            _ => 0L,
        };
    }
}
