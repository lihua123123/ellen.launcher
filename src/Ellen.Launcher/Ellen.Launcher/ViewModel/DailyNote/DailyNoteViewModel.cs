// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Core;
using Ellen.Launcher.Core.Database;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Core.Logging;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Service;
using Ellen.Launcher.Service.DailyNote;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Service.Navigation;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.Service.SignIn;
using Ellen.Launcher.Service.User;
using Ellen.Launcher.UI.Xaml.View.Dialog;
using Ellen.Launcher.UI.Xaml.View.Page;
using Ellen.Launcher.UI.Xaml.View.Window.WebView2;
using Ellen.Launcher.ViewModel.Game;
using Ellen.Launcher.Web.Hoyolab.Takumi.Binding;
using Ellen.Launcher.Web.Hoyolab.Takumi.Event.BbsSignReward;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.ZZZ;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using BindingUser = Ellen.Launcher.ViewModel.User.User;

namespace Ellen.Launcher.ViewModel.DailyNote;

[BindableCustomPropertyProvider]
[Service(ServiceLifetime.Scoped)]
public sealed partial class DailyNoteViewModel : Abstraction.ViewModel
{
    private readonly IContentDialogFactory contentDialogFactory;
    private readonly INavigationService navigationService;
    private readonly IDailyNoteService dailyNoteService;
    private readonly IServiceProvider serviceProvider;
    private readonly IMetadataService metadataService;
    private readonly ITaskContext taskContext;
    private readonly IUserService userService;
    private readonly IMessenger messenger;
    private readonly IZZZGameRecordClient zzzGameRecordClient;
    private readonly IZZZAvatarCacheService zzzAvatarCacheService;
    private readonly IGameRoleCollectionService gameRoleCollectionService;

    private DailyNoteMetadataContext? metadataContext;

    [GeneratedConstructor]
    public partial DailyNoteViewModel(IServiceProvider serviceProvider);

    public partial DailyNoteOptions DailyNoteOptions { get; }

    public partial RuntimeOptions RuntimeOptions { get; }

    public partial AppOptions AppOptions { get; }

    public IJSBridgeUriSourceProvider VerifyUrlSource { get; } = new DailyJSBridgeUriSourceProvider();

    [ObservableProperty]
    public partial AdvancedDbCollectionView<User.User, Model.Entity.User>? Users { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<DailyNoteEntry>? DailyNoteEntries { get; set; }

    /// <summary>
    /// 绝区零便笺卡片。不落库：角色取自「我的角色」的本地缓存，便笺每次进页面/刷新时联网拉取。
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<ZenlessNoteEntry>? ZenlessNoteEntries { get; set; }

    /// <summary>
    /// 按米游社账号分好组的便笺，页面上一组（一个账号）一张卡片。
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<DailyNoteAccountCard>? DailyNoteCards { get; set; }

    protected override async ValueTask<bool> LoadOverrideAsync(CancellationToken token)
    {
        if (!await metadataService.InitializeAsync().ConfigureAwait(false))
        {
            return false;
        }

        metadataContext = await metadataService.GetContextAsync<DailyNoteMetadataContext>(token).ConfigureAwait(false);

        try
        {
            await taskContext.SwitchToBackgroundAsync();
            AdvancedDbCollectionView<User.User, Model.Entity.User> users = await userService.GetUsersAsync().ConfigureAwait(false);

            // 与原神便笺的旧行为不同：不再依赖「自动刷新」开关，每次进入便笺页都强制联网刷新一次，
            // 和下方绝区零便笺「进页面即拉取」的表现保持一致。
            ObservableCollection<DailyNoteEntry> entries = await dailyNoteService.GetDailyNoteEntryCollectionAsync(metadataContext, true, token).ConfigureAwait(false);

            // 绝区零便笺只在内存里，失败不影响原神部分。
            // 与原神便笺共用「是否添加过便笺」这一开关：没添加便笺的账号不显示绝区零列。
            ObservableCollection<ZenlessNoteEntry> zenlessEntries = await CreateZenlessNoteEntriesAsync(users.Source, entries, token).ConfigureAwait(false);

            await taskContext.SwitchToMainThreadAsync();
            Users = users;
            DailyNoteEntries = entries;
            ZenlessNoteEntries = zenlessEntries;

            RebuildCards();

            // 添加 / 移除原神便笺后，绝区零便笺的可见账号集合也会变化，一并重建卡片
            entries.CollectionChanged += (_, _) => RefreshZenlessEntriesAsync().SafeForget();
            return true;
        }
        catch (EllenException ex)
        {
            messenger.Send(InfoBarMessage.Error(ex));
        }

        return false;
    }

    [Command("TrackCurrentUserAndUidCommand")]
    private async Task TrackCurrentUserAndUidAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Add daily note", "DailyNoteViewModel.Command"));

        if (await userService.GetCurrentUserAndUidAsync().ConfigureAwait(false) is not { } userAndUid)
        {
            messenger.Send(InfoBarMessage.Warning(SH.MustSelectUserAndUid));
            return;
        }

        ArgumentNullException.ThrowIfNull(metadataContext);

        ContentDialog dialog = await contentDialogFactory
            .CreateForIndeterminateProgressAsync(SH.ViewModelDailyNoteRequestProgressTitle)
            .ConfigureAwait(false);

        using (await contentDialogFactory.BlockAsync(dialog).ConfigureAwait(false))
        {
            await dailyNoteService.AddDailyNoteAsync(metadataContext, userAndUid).ConfigureAwait(false);
        }
    }

    [Command("RefreshCommand")]
    private async Task RefreshAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Refresh daily note", "DailyNoteViewModel.Command"));
        await dailyNoteService.RefreshDailyNotesAsync().ConfigureAwait(false);

        // 绝区零便笺只在内存里，按当前已添加便笺的账号重建并重新拉取一次，
        // 这样刚添加的账号也会出现绝区零列，移除后也会随之消失。
        await RefreshZenlessEntriesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 重新创建绝区零便笺卡片：只对已经添加过实时便笺的账号显示。
    /// 失败的角色保留错误信息，不影响原神便笺。
    /// </summary>
    private async Task RefreshZenlessEntriesAsync()
    {
        if (Users is null)
        {
            return;
        }

        ObservableCollection<DailyNoteEntry>? genshinEntries = DailyNoteEntries;
        ObservableCollection<ZenlessNoteEntry> entries = await CreateZenlessNoteEntriesAsync(Users.Source, genshinEntries ?? []).ConfigureAwait(false);

        await taskContext.SwitchToMainThreadAsync();
        ZenlessNoteEntries = entries;
        RebuildCards();
    }

    /// <summary>
    /// 为一个米游社账号下的每个绝区零角色建一张便笺卡片。
    /// 角色优先取「我的角色」的本地缓存（零网络），没有缓存时回退到绑定接口（有 5 分钟缓存）。
    /// 只有 <paramref name="genshinEntries"/> 中包含的账号（即用户手动添加过实时便笺的账号）才会生成卡片。
    /// </summary>
    private async ValueTask<ObservableCollection<ZenlessNoteEntry>> CreateZenlessNoteEntriesAsync(
        IEnumerable<BindingUser> users,
        IEnumerable<DailyNoteEntry> genshinEntries,
        CancellationToken token = default)
    {
        HashSet<Guid> trackedUserIds = genshinEntries.Select(static entry => entry.UserId).ToHashSet();
        List<ZenlessNoteEntry> entries = [];

        foreach (BindingUser user in users)
        {
            if (user.Entity.IsOversea || !trackedUserIds.Contains(user.InnerId))
            {
                // 国际服 fp 逻辑与国服不同，先只做国服；未添加便笺的账号也不显示绝区零列
                continue;
            }

            foreach (ZZZUserGameRole role in await GetZenlessRolesAsync(user, token).ConfigureAwait(false))
            {
                entries.Add(new(zzzGameRecordClient, taskContext, user.Entity, role));
            }
        }

        if (entries.Count is 0)
        {
            return [];
        }

        await Task.WhenAll(entries.Select(entry => entry.UpdateAsync(token).AsTask())).ConfigureAwait(false);
        return [.. entries];
    }

    private async ValueTask<ImmutableArray<ZZZUserGameRole>> GetZenlessRolesAsync(BindingUser user, CancellationToken token)
    {
        if (zzzAvatarCacheService.LoadRoles(user.InnerId) is { Roles.Count: > 0 } cache)
        {
            return [.. cache.Roles];
        }

        ImmutableArray<SignInGameRole> roles = await gameRoleCollectionService.GetRolesAsync(user, token).ConfigureAwait(false);
        return
        [
            .. roles
                .Where(static role => role.Game is SignInGame.ZenlessZoneZero)
                .Select(static role => new ZZZUserGameRole
                {
                    GameBiz = role.GameBiz,
                    Region = role.Region,
                    GameUid = role.Uid,
                    Nickname = role.Nickname ?? string.Empty,
                    Level = role.Level,
                })
        ];
    }

    private void RebuildCards()
    {
        DailyNoteCards = DailyNoteAccountCard.CreateFrom(DailyNoteEntries ?? [], ZenlessNoteEntries ?? [], LookupAccount);
    }

    private (string? Nickname, Uri? AvatarUri, string? Aid) LookupAccount(Guid userId)
    {
        if (Users?.Source.FirstOrDefault(user => user.InnerId == userId) is { } user)
        {
            return (user.UserInfo?.Nickname, user.UserInfo?.AvatarUri, user.Entity.Aid);
        }

        return (null, null, null);
    }

    /// <summary>
    /// 删除整张卡片里的原神便笺（绝区零便笺是派生数据，没有可删的记录）。
    /// </summary>
    [Command("RemoveDailyNoteCardCommand")]
    private async Task RemoveDailyNoteCardAsync(DailyNoteAccountCard? card)
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory2.CreateUI("Remove daily note card", "DailyNoteViewModel.Command", [("columns", card?.Columns.Count.ToString() ?? "<null>")]));

        if (card is null)
        {
            return;
        }

        foreach (DailyNoteEntry entry in card.GenshinEntries.ToArray())
        {
            await dailyNoteService.RemoveDailyNoteAsync(entry).ConfigureAwait(false);
        }
    }

    [Command("RemoveDailyNoteCommand")]
    private async Task RemoveDailyNoteAsync(DailyNoteEntry? entry)
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory2.CreateUI("Remove daily note", "DailyNoteViewModel.Command", [("uid", entry?.Uid ?? "<null>")]));

        if (entry is not null)
        {
            await dailyNoteService.RemoveDailyNoteAsync(entry).ConfigureAwait(false);
        }
    }

    [Command("ModifyNotificationCommand")]
    private async Task ModifyDailyNoteNotificationAsync(DailyNoteEntry? entry)
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory2.CreateUI("Modify daily note notification settings", "DailyNoteViewModel.Command", [("uid", entry?.Uid ?? "<null>")]));

        if (entry is not null)
        {
            using (await EnterCriticalSectionAsync().ConfigureAwait(false))
            {
                DailyNoteNotificationDialog dialog = await contentDialogFactory.CreateInstanceAsync<DailyNoteNotificationDialog>(serviceProvider, entry).ConfigureAwait(true);
                await contentDialogFactory.EnqueueAndShowAsync(dialog).ShowTask.ConfigureAwait(false);

                await taskContext.SwitchToBackgroundAsync();
                await dailyNoteService.UpdateDailyNoteAsync(entry).ConfigureAwait(false);
            }
        }
    }

    [Command("ConfigDailyNoteWebhookUrlCommand")]
    private async Task ConfigDailyNoteWebhookUrlAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Modify daily note webhook settings", "DailyNoteViewModel.Command"));

        DailyNoteWebhookDialog dialog = await contentDialogFactory.CreateInstanceAsync<DailyNoteWebhookDialog>(serviceProvider).ConfigureAwait(true);
        dialog.Text = DailyNoteOptions.WebhookUrl.Value;
        (bool isOk, string? url) = await dialog.GetInputUrlAsync().ConfigureAwait(false);

        if (isOk)
        {
            await taskContext.SwitchToMainThreadAsync();
            DailyNoteOptions.WebhookUrl.Value = url;
            messenger.Send(InfoBarMessage.Information(SH.ViewModelDailyNoteConfigWebhookUrlComplete));
        }
    }
}