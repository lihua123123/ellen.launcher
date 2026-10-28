// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Web.Hoyolab.Takumi.Binding;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.Zenless;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.ZZZ;
using Ellen.Launcher.Web.Response;
using EntityUser = Ellen.Launcher.Model.Entity.User;

namespace Ellen.Launcher.ViewModel.DailyNote;

/// <summary>
/// 实时便笺页面上「绝区零」那一张卡片的数据：一个绝区零角色对应一张卡片。
///
/// 与原神便笺不同，绝区零便笺不落数据库（避免再引入一次 EF 迁移），
/// 角色来自「我的角色」的本地缓存，便笺本身每次进页面/点刷新时联网拉取。
/// </summary>
public sealed partial class ZenlessNoteEntry : ObservableObject
{
    private readonly IZZZGameRecordClient gameRecordClient;
    private readonly ITaskContext taskContext;
    private readonly EntityUser user;

    public ZenlessNoteEntry(IZZZGameRecordClient gameRecordClient, ITaskContext taskContext, EntityUser user, ZZZUserGameRole role)
    {
        this.gameRecordClient = gameRecordClient;
        this.taskContext = taskContext;
        this.user = user;

        Role = role;
    }

    public ZZZUserGameRole Role { get; }

    /// <summary>
    /// 所属米游社账号，用于把同一账号下的便笺合并到一张卡里。
    /// </summary>
    public Guid UserId
    {
        get => user.InnerId;
    }

    public string RoleName
    {
        get => Role.Nickname;
    }

    public string UidText
    {
        get => Role.GameUid;
    }

    /// <summary>
    /// 列标题：「昵称 | 区服 | Lv.等级」，与原神便笺的格式保持一致。
    /// </summary>
    public string RoleDisplay
    {
        get
        {
            if (string.IsNullOrEmpty(Role.Nickname))
            {
                return UidText;
            }

            return $"{Role.Nickname} | {RegionName} | Lv.{Role.Level}";
        }
    }

    /// <summary>
    /// 区服显示名：国服统一显示「丽都」，其它服沿用接口给的 region_name。
    /// </summary>
    public string RegionName
    {
        get
        {
            string? mapped = Role.Region switch
            {
                "prod_gf_cn" or "prod_qd_cn" => "丽都",
                "prod_os_usa" => "America",
                "prod_os_euro" => "Europe",
                "prod_os_asia" => "Asia",
                "prod_os_cht" => "TW, HK, MO",
                _ => null,
            };

            if (mapped is not null)
            {
                return mapped;
            }

            return string.IsNullOrEmpty(Role.RegionName) ? Role.Region : Role.RegionName;
        }
    }

    [ObservableProperty]
    public partial ZenlessDailyNote? Note { get; set; }

    [ObservableProperty]
    public partial DateTimeOffset RefreshTime { get; set; }

    /// <summary>
    /// 是否已经拿到过一次数据（决定显示内容还是「获取失败」提示）。
    /// </summary>
    public bool HasNote
    {
        get => Note is not null;
    }

    public string RefreshTimeFormatted
    {
        get => RefreshTime == default
            ? SH.ModelEntityDailyNoteNotRefreshed
            : SH.FormatModelEntityDailyNoteRefreshTime(RefreshTime.ToLocalTime());
    }

    public async ValueTask UpdateAsync(CancellationToken token = default)
    {
        Response<ZenlessDailyNote> response = await gameRecordClient
            .GetZZZNoteAsync(user, Role.GameUid, Role.Region, token)
            .ConfigureAwait(false);

        if (!ResponseValidator.TryValidateWithoutUINotification(response, out ZenlessDailyNote? note))
        {
            return;
        }

        await taskContext.SwitchToMainThreadAsync();
        Note = note;
        RefreshTime = DateTimeOffset.Now;
        OnPropertyChanged(nameof(HasNote));
        OnPropertyChanged(nameof(RefreshTimeFormatted));
    }

    partial void OnNoteChanged(ZenlessDailyNote? value)
    {
        OnPropertyChanged(nameof(HasNote));
    }
}
