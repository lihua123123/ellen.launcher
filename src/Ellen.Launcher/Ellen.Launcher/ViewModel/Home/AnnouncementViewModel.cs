// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.
// Copyright (c) Snap Hutao RP. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Core.DataTransfer;
using Ellen.Launcher.Core.DependencyInjection.Abstraction;
using Ellen.Launcher.Core.IO;
using Ellen.Launcher.Core.Logging;
using Ellen.Launcher.Core.Setting;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Factory.Picker;
using Ellen.Launcher.Service;
using Ellen.Launcher.Service.Announcement;
using Ellen.Launcher.Service.Game;
using Ellen.Launcher.Service.Game.FileSystem;
using Ellen.Launcher.Service.Game.Locator;
using Ellen.Launcher.Service.Network;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.Service.Redeem;
using Ellen.Launcher.Service.User;
using Ellen.Launcher.UI.Xaml.Control.Card;
using Ellen.Launcher.UI.Xaml.View.Card;
using Ellen.Launcher.ViewModel.User;
using Ellen.Launcher.Web;
using Ellen.Launcher.Web.Hoyolab.Bbs.Home;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Common.Announcement;
using Ellen.Launcher.Web.Hoyolab.Takumi.Event.BbsSignReward;
using Ellen.Launcher.Web.Hoyolab.Takumi.Event.Miyolive;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.ActCalendar;
using Ellen.Launcher.Web.Request.Builder;
using Ellen.Launcher.Web.Response;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Ellen.Launcher.ViewModel.Home;

[BindableCustomPropertyProvider]
public sealed partial class LiveRedeemCode(SignInGame game, string code, string rewardText)
{
    public SignInGame Game { get; } = game;

    public string Code { get; } = code;

    public string RewardText { get; } = rewardText;
}

[BindableCustomPropertyProvider]
[Service(ServiceLifetime.Scoped)]
public sealed partial class AnnouncementViewModel : Abstraction.ViewModel, IRecipient<UserAndUidChangedMessage>
{
    private readonly IAnnouncementService announcementService;
    private readonly IServiceProvider serviceProvider;
    private readonly CultureOptions cultureOptions;
    private readonly ITaskContext taskContext;
    private readonly INetworkRetryCoordinator networkRetryCoordinator;
    private readonly IGameLocatorFactory gameLocatorFactory;
    private readonly LaunchOptions launchOptions;

    private IDisposable? homeRetryRegistration;
    private int shouldRefreshDashboardOnSuccess;
    private int isFetchingLiveCodes;
    private string? genshinLiveCodeFingerprint;
    private string? zenlessLiveCodeFingerprint;
    private bool hasUnseenGenshinLiveCodes;
    private bool hasUnseenZenlessLiveCodes;

    [GeneratedConstructor]
    public partial AnnouncementViewModel(IServiceProvider serviceProvider);

    [ObservableProperty]
    public partial AnnouncementWrapper? Announcement { get; set; }

    [ObservableProperty]
    public partial string GreetingText { get; set; } = SH.ViewPageHomeGreetingTextDefault;

    [ObservableProperty]
    public partial ImmutableArray<LiveRedeemCode> GenshinRedeemCodes { get; set; } = [];

    [ObservableProperty]
    public partial ImmutableArray<LiveRedeemCode> ZenlessRedeemCodes { get; set; } = [];

    [ObservableProperty]
    public partial string GenshinManualCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ZenlessManualCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasNewLiveRedeemCodes { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRedeemCode))]
    public partial bool IsRedeemingCode { get; set; }

    public bool CanRedeemCode => !IsRedeemingCode;

    [ObservableProperty]
    public partial List<CardReference>? Cards { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActivityCalendarPresented))]
    public partial ActCalendar ActivityCalendar { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActivityCalendarPresented))]
    public partial bool IsActivityCalendarLoading { get; set; } = true;

    [ObservableProperty]
    public partial ImmutableArray<Act> DisplayedActivityCards { get; set; } = [];

    /// <summary>
    /// 供首页快捷开关（「默认以管理员模式启动」）直接绑定。
    /// </summary>
    public partial AppOptions AppOptions { get; }

    public bool IsHomeAnnouncementActivityCalendarPresented
    {
        get => LocalSetting.Get(SettingKeys.IsHomeAnnouncementActivityCalendarPresented, true);
    }

    public bool IsActivityCalendarPresented
    {
        get => LocalSetting.Get(SettingKeys.IsHomeAnnouncementActivityCalendarPresented, true)
            && (ActivityCalendar is not null || IsActivityCalendarLoading);
    }

    public void Receive(UserAndUidChangedMessage message)
    {
        if (message.UserAndUid is { IsOversea: false } userAndUid)
        {
            RefreshActivityCalendarOnUserChangedAsync(userAndUid).SafeForget();
        }
        else
        {
            taskContext.InvokeOnMainThread(() =>
            {
                ActivityCalendar = default!;
                DisplayedActivityCards = [];
                IsActivityCalendarLoading = false;
            });
        }
    }

    [GeneratedRegex("<br\\s*/?>|</(?:p|div|li)\\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex RewardLineBreaks { get; }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex RewardHtmlTags { get; }

    [GeneratedRegex(@"\s*[*×xX]\s*(?<count>[0-9]+(?:[,，][0-9]{3})*)")]
    private static partial Regex RewardQuantities { get; }

    protected override async ValueTask<bool> LoadOverrideAsync(CancellationToken token)
    {
        homeRetryRegistration ??= networkRetryCoordinator.Register("AnnouncementViewModel.LoadHome", RetryHomeAsync);
        await taskContext.SwitchToMainThreadAsync();
        RefreshDashboard();
        UpdateGreetingText();

        RetryHomeAsync(token).AsTask().SafeForget();
        return true;
    }

    protected override void UninitializeOverride()
    {
        homeRetryRegistration?.Dispose();
        homeRetryRegistration = default;
    }

    [SuppressMessage("", "SH003")]
    private async ValueTask<bool> InitializeInGameAnnouncementAsync(CancellationToken token)
    {
        try
        {
            AnnouncementWrapper? announcementWrapper = await announcementService.GetAnnouncementWrapperAsync(cultureOptions.LanguageCode, AppOptions.Region.Value, token).ConfigureAwait(false);
            if (announcementWrapper is null)
            {
                MarkHomePendingForRetry();
                return false;
            }

            await taskContext.SwitchToMainThreadAsync();
            Announcement = announcementWrapper;
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IsNetworkRelatedException(ex))
        {
            MarkHomePendingForRetry();
            return false;
        }
    }

    [Command("RefreshLiveCodesCommand")]
    private async Task RefreshLiveCodesAsync()
    {
        CancellationToken token = CancellationToken;
        try
        {
            await InitializeMiyoliveCodeAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    [SuppressMessage("", "SH003")]
    private async ValueTask<bool> InitializeMiyoliveCodeAsync(CancellationToken token)
    {
        if (Interlocked.Exchange(ref isFetchingLiveCodes, 1) != 0)
        {
            return true;
        }

        try
        {
            return await InitializeMiyoliveCodeCoreAsync(token).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref isFetchingLiveCodes, 0);
        }
    }

    [SuppressMessage("", "SH003")]
    private async ValueTask<bool> InitializeMiyoliveCodeCoreAsync(CancellationToken token)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        IHomeClient homeClient = scope.ServiceProvider.GetRequiredService<IOverseaSupportFactory<IHomeClient>>().Create(false);
        IMiyoliveClient miyoliveClient = scope.ServiceProvider.GetRequiredService<IOverseaSupportFactory<IMiyoliveClient>>().Create(false);
        Task<LiveRedeemBatch?> genshinTask = FetchLiveCodesAsync(homeClient, miyoliveClient, 2, SignInGame.Genshin, token);
        Task<LiveRedeemBatch?> zenlessTask = FetchLiveCodesAsync(homeClient, miyoliveClient, 8, SignInGame.ZenlessZoneZero, token);
        LiveRedeemBatch?[] batches = await Task.WhenAll(genshinTask, zenlessTask).ConfigureAwait(false);

        await taskContext.SwitchToMainThreadAsync();
        token.ThrowIfCancellationRequested();
        if (IsViewUnloaded.Value)
        {
            return true;
        }

        if (batches[0] is { } genshin)
        {
            GenshinRedeemCodes = genshin.Codes;
            UpdateLiveCodeNoticeState(genshin, SettingKeys.HomeRedeemGenshinNotice, ref genshinLiveCodeFingerprint, ref hasUnseenGenshinLiveCodes);
        }

        if (batches[1] is { } zenless)
        {
            ZenlessRedeemCodes = zenless.Codes;
            UpdateLiveCodeNoticeState(zenless, SettingKeys.HomeRedeemZenlessNotice, ref zenlessLiveCodeFingerprint, ref hasUnseenZenlessLiveCodes);
        }

        HasNewLiveRedeemCodes = hasUnseenGenshinLiveCodes || hasUnseenZenlessLiveCodes;

        bool success = batches[0] is not null && batches[1] is not null;
        if (!success)
        {
            MarkHomePendingForRetry();
        }

        return success;
    }

    [SuppressMessage("", "SH003")]
    private static async Task<LiveRedeemBatch?> FetchLiveCodesAsync(IHomeClient homeClient, IMiyoliveClient miyoliveClient, int gid, SignInGame game, CancellationToken token)
    {
        try
        {
            Response<NewHomeNewInfo> response = await homeClient.GetNewHomeInfoAsync(gid, token).ConfigureAwait(false);
            if (!ResponseValidator.TryValidateWithoutUINotification(response, out NewHomeNewInfo? home))
            {
                return new(string.Empty, []);
            }

            string? actId = home.Navigator
                .Where(static nav => (nav.Name is "前瞻直播" or "前瞻节目" or "直播兑换码" or "特别节目") || nav.Name.Contains("前瞻", StringComparison.Ordinal))
                .Select(static nav => GetLiveActivityId(nav.AppPath))
                .Concat(home.Lives.Select(static live => GetLiveActivityId(live.Data.LiveUrl)))
                .FirstOrDefault(static id => !string.IsNullOrWhiteSpace(id));

            if (string.IsNullOrWhiteSpace(actId))
            {
                return new(string.Empty, []);
            }

            Response<CodeListWrapper> codeResponse = await miyoliveClient.RefreshCodeAsync(actId, token).ConfigureAwait(false);
            if (!ResponseValidator.TryValidateWithoutUINotification(codeResponse, out CodeListWrapper? data))
            {
                return new(actId, []);
            }

            ImmutableArray<LiveRedeemCode> codes = [.. data.CodeList
                .Where(static item => !string.IsNullOrWhiteSpace(item.Code))
                .DistinctBy(static item => item.Code.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(item => new LiveRedeemCode(game, item.Code.Trim(), FormatRewardText(item.Title)))];
            return new(actId, codes);
        }
        catch (Exception ex) when (!token.IsCancellationRequested && IsNetworkRelatedException(ex))
        {
            return null;
        }
    }

    private static string? GetLiveActivityId(Uri? url)
    {
        return url is { IsAbsoluteUri: true } ? HttpUtility.ParseQueryString(url.Query)["act_id"] : null;
    }

    private static void UpdateLiveCodeNoticeState(LiveRedeemBatch batch, string settingKey, ref string? currentFingerprint, ref bool hasUnseenCodes)
    {
        if (batch.Codes.IsEmpty)
        {
            return;
        }

        currentFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(batch.ActivityId + "\n" +
            string.Join("\n", batch.Codes.Select(static code => code.Code.ToUpperInvariant()).OrderBy(static code => code, StringComparer.Ordinal)))));
        hasUnseenCodes = !string.Equals(LocalSetting.Get(settingKey, string.Empty), currentFingerprint, StringComparison.Ordinal);
    }

    /// <summary>
    /// 用户打开兑换码浮窗后，将当前已加载的前瞻码批次标记为已查看。
    /// 指纹只在用户查看时持久化，确保应用重启后未查看的新码仍会高亮。
    /// </summary>
    public void MarkLiveRedeemCodesViewed()
    {
        if (genshinLiveCodeFingerprint is { } genshinFingerprint)
        {
            LocalSetting.Set(SettingKeys.HomeRedeemGenshinNotice, genshinFingerprint);
        }

        if (zenlessLiveCodeFingerprint is { } zenlessFingerprint)
        {
            LocalSetting.Set(SettingKeys.HomeRedeemZenlessNotice, zenlessFingerprint);
        }

        hasUnseenGenshinLiveCodes = false;
        hasUnseenZenlessLiveCodes = false;
        HasNewLiveRedeemCodes = false;
    }

    private static string FormatRewardText(string title)
    {
        string text = WebUtility.HtmlDecode(title);
        text = RewardLineBreaks.Replace(text, "\n");
        text = RewardHtmlTags.Replace(text, string.Empty);
        text = RewardQuantities.Replace(text, match =>
        {
            string quantity = match.Groups["count"].Value.Replace(",", string.Empty, StringComparison.Ordinal).Replace("，", string.Empty, StringComparison.Ordinal);
            return long.TryParse(quantity, NumberStyles.None, CultureInfo.InvariantCulture, out long count)
                ? $" × {count.ToString("N0", CultureInfo.CurrentCulture)}\n"
                : $" × {match.Groups["count"].Value}\n";
        });
        return string.Join("\n", text.Split('\n').Select(static line => line.Trim(' ', '\r', '\t', '、', ',', '，', ';', '；')).Where(static line => !string.IsNullOrWhiteSpace(line)));
    }

    private sealed record LiveRedeemBatch(string ActivityId, ImmutableArray<LiveRedeemCode> Codes);

    private async ValueTask<bool> InitializeActivityCalendarAsync(CancellationToken token)
    {
        if (!LocalSetting.Get(SettingKeys.IsHomeAnnouncementActivityCalendarPresented, true))
        {
            return true;
        }

        try
        {
            using (IServiceScope scope = serviceProvider.CreateScope())
            {
                IUserService userService = scope.ServiceProvider.GetRequiredService<IUserService>();
                if (await userService.GetCurrentUserAndUidAsync().ConfigureAwait(false) is not { IsOversea: false } userAndUid)
                {
                    await taskContext.SwitchToMainThreadAsync();
                    IsActivityCalendarLoading = false;
                    return true;
                }

                await RefreshActivityCalendarCoreAsync(userAndUid, token).ConfigureAwait(false);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IsNetworkRelatedException(ex))
        {
            MarkHomePendingForRetry();
            return false;
        }
    }

    [SuppressMessage("", "SH003")]
    private async ValueTask RefreshActivityCalendarOnUserChangedAsync(UserAndUid userAndUid)
    {
        try
        {
            await RefreshActivityCalendarCoreAsync(userAndUid, CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (IsNetworkRelatedException(ex))
        {
        }
    }

    [SuppressMessage("", "SH003")]
    private async ValueTask RefreshActivityCalendarCoreAsync(UserAndUid userAndUid, CancellationToken token)
    {
        if (!LocalSetting.Get(SettingKeys.IsHomeAnnouncementActivityCalendarPresented, true))
        {
            return;
        }

        await taskContext.SwitchToMainThreadAsync();
        IsActivityCalendarLoading = true;

        IGameRecordClient gameRecordClient = serviceProvider
            .GetRequiredService<IOverseaSupportFactory<IGameRecordClient>>()
            .CreateFor(userAndUid);

        Response<ActCalendar> response = await gameRecordClient.GetActCalendarAsync(userAndUid, token).ConfigureAwait(false);

        if (ResponseValidator.TryValidateWithoutUINotification(response, out ActCalendar? calendar))
        {
            await taskContext.SwitchToMainThreadAsync();
            ActivityCalendar = calendar;
            UpdateDisplayedActivityCards();
            IsActivityCalendarLoading = false;
        }
    }

    [Command("SetGamePathCommand")]
    private async Task SetGamePathAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Set game path", "AnnouncementViewModel.Command"));

        if (await gameLocatorFactory.LocateSingleAsync(GameLocationSourceKind.Manual).ConfigureAwait(false) is not (true, { } path))
        {
            return;
        }

        await taskContext.SwitchToMainThreadAsync();
        launchOptions.PerformGamePathEntrySynchronization(path);
        serviceProvider.GetRequiredService<IMessenger>().Send(InfoBarMessage.Success(SH.ViewPageAnnouncementSetGamePathSuccessTitle, SH.FormatViewPageAnnouncementSetGamePathSuccessMessage(path)));
    }

    /// <summary>
    /// 设置绝区零游戏路径，供网页缓存方式获取调频记录使用（本启动器不启动绝区零）。
    /// 适配 Starward 等非官方启动器安装位置，注册表查不到时由用户手动指定。
    /// </summary>
    [Command("SetZZZGamePathCommand")]
    private async Task SetZZZGamePathAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Set ZZZ game path", "AnnouncementViewModel.Command"));

        IFileSystemPickerInteraction fileSystemPickerInteraction = serviceProvider.GetRequiredService<IFileSystemPickerInteraction>();
        (bool isPickerOk, ValueFile file) = fileSystemPickerInteraction.PickFile(
            SH.ViewPageAnnouncementSetZZZGamePathLabel,
            SH.ServiceGameLocatorPickerFilterText,
            GameConstants.ZenlessZoneZeroFileName);

        if (isPickerOk)
        {
            string fileName = Path.GetFileName(file);
            if (fileName.ToUpperInvariant() is GameConstants.ZenlessZoneZeroFileNameUpper)
            {
                await taskContext.SwitchToMainThreadAsync();
                launchOptions.ZZZGamePath.Value = file;
                serviceProvider.GetRequiredService<IMessenger>().Send(InfoBarMessage.Success(SH.ViewPageAnnouncementSetGamePathSuccessTitle, SH.FormatViewPageAnnouncementSetZZZGamePathSuccessMessage(file)));
            }
        }
    }

    private async ValueTask<bool> RetryHomeAsync(CancellationToken token)
    {
        Task<bool> inGameTask = InitializeInGameAnnouncementAsync(token).AsTask();
        Task<bool> miHoYoTask = InitializeMiyoliveCodeAsync(token).AsTask();
        Task<bool> calendarTask = InitializeActivityCalendarAsync(token).AsTask();

        bool[] results = await Task.WhenAll(inGameTask, miHoYoTask, calendarTask).ConfigureAwait(false);
        bool inGameSuccess = results[0];
        bool miHoYoSuccess = results[1];
        bool calendarSuccess = results[2];

        bool success = inGameSuccess && miHoYoSuccess && calendarSuccess;
        if (success)
        {
            if (Interlocked.Exchange(ref shouldRefreshDashboardOnSuccess, 0) is not 0)
            {
                RefreshDashboard();
            }

            networkRetryCoordinator.ClearPending("AnnouncementViewModel.LoadHome");
        }

        return success;
    }

    private static bool IsNetworkRelatedException(Exception ex)
    {
        return ex switch
        {
            HttpRequestException httpRequestException => HttpRequestExceptionHandling.HttpRequestExceptionToNetworkError(httpRequestException) is not NetworkError.NULL,
            TimeoutException => true,
            TaskCanceledException => true,
            _ => false,
        };
    }

    [Command("RedeemCodeCommand")]
    private Task RedeemCodeAsync(LiveRedeemCode? entry)
    {
        return entry is null ? Task.CompletedTask : RedeemAsync(entry.Game, entry.Code);
    }

    [Command("RedeemGenshinManualCodeCommand")]
    private Task RedeemGenshinManualCodeAsync()
    {
        return RedeemAsync(SignInGame.Genshin, GenshinManualCode);
    }

    [Command("RedeemZenlessManualCodeCommand")]
    private Task RedeemZenlessManualCodeAsync()
    {
        return RedeemAsync(SignInGame.ZenlessZoneZero, ZenlessManualCode);
    }

    [SuppressMessage("", "SH003")]
    private async Task RedeemAsync(SignInGame game, string input)
    {
        await taskContext.SwitchToMainThreadAsync();
        if (IsRedeemingCode)
        {
            return;
        }

        using IServiceScope scope = serviceProvider.CreateScope();
        IMessenger messenger = scope.ServiceProvider.GetRequiredService<IMessenger>();
        if (!RedeemService.TryNormalizeCode(input, out string code))
        {
            messenger.Send(InfoBarMessage.Warning(SH.ViewRedeemManualCodeInvalid));
            return;
        }

        IsRedeemingCode = true;
        try
        {
            IUserService userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            var user = await userService.GetCurrentUserAsync().ConfigureAwait(false);
            if (user is null || user.IsOversea)
            {
                messenger.Send(InfoBarMessage.Warning(user is null ? SH.ViewRedeemNoUser : SH.ViewRedeemOverseaUnsupported));
                return;
            }

            IRedeemService redeemService = scope.ServiceProvider.GetRequiredService<IRedeemService>();
            ImmutableArray<SignInGameRole> roles = await redeemService.GetSupportedRolesAsync(user).ConfigureAwait(false);
            SignInGameRole[] gameRoles = [.. roles.Where(role => role.Game == game).DistinctBy(static role => (role.Uid, role.Region))];
            if (gameRoles.Length == 0)
            {
                messenger.Send(InfoBarMessage.Warning(SH.ViewRedeemNoMatchingRole));
                return;
            }

            SignInGameRole? role = gameRoles.Length == 1 ? gameRoles[0] : await ChooseRedeemRoleAsync(gameRoles, user.PreferredUid).ConfigureAwait(false);
            if (role is null)
            {
                return;
            }

            if ((await userService.GetCurrentUserAsync().ConfigureAwait(false))?.InnerId != user.InnerId)
            {
                messenger.Send(InfoBarMessage.Warning(SH.ViewRedeemAccountChanged));
                return;
            }

            RedeemResult result = await redeemService.RedeemAsync(user.Entity, role, code).ConfigureAwait(false);
            messenger.Send(result.IsSuccess ? InfoBarMessage.Success(result.Message)
                : result.IsAlreadyUsed ? InfoBarMessage.Warning(result.Message)
                : InfoBarMessage.Error(result.Message));
        }
        catch (Exception ex) when (IsNetworkRelatedException(ex))
        {
            messenger.Send(InfoBarMessage.Error(SH.ViewRedeemRoleLoadFailed));
        }
        finally
        {
            await taskContext.SwitchToMainThreadAsync();
            IsRedeemingCode = false;
        }
    }

    private async ValueTask<SignInGameRole?> ChooseRedeemRoleAsync(SignInGameRole[] roles, string? preferredUid)
    {
        IContentDialogFactory dialogFactory = serviceProvider.GetRequiredService<IContentDialogFactory>();
        ContentDialog dialog = await dialogFactory.CreateInstanceAsync<ContentDialog>(serviceProvider).ConfigureAwait(false);
        await taskContext.SwitchToMainThreadAsync();
        ComboBox picker = new()
        {
            ItemsSource = roles.Select(static role => $"{role.Nickname} · UID {role.Uid} · {role.Region}").ToArray(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedIndex = Array.FindIndex(roles, role => role.Uid == preferredUid),
        };
        dialog.Title = SH.ViewRedeemRoleRequired;
        dialog.Content = picker;
        dialog.PrimaryButtonText = SH.ViewRedeemSubmit;
        dialog.CloseButtonText = SH.ContentDialogCancelCloseButtonText;
        dialog.IsPrimaryButtonEnabled = picker.SelectedIndex >= 0;
        picker.SelectionChanged += (_, _) => dialog.IsPrimaryButtonEnabled = picker.SelectedIndex >= 0;
        ContentDialogResult result = await dialogFactory.EnqueueAndShowAsync(dialog).ShowTask.ConfigureAwait(false);
        await taskContext.SwitchToMainThreadAsync();
        return result == ContentDialogResult.Primary && picker.SelectedIndex >= 0 ? roles[picker.SelectedIndex] : null;
    }

    private void UpdateGreetingText()
    {
        taskContext.InvokeOnMainThread(UpdateGreetingTextCore);
    }

    private void UpdateGreetingTextCore()
    {
        // TODO avatar birthday override.
        int rand = Random.Shared.Next(0, 1000);

        if (rand is >= 0 and < 6)
        {
            GreetingText = SH.ViewPageHomeGreetingTextEasterEgg;
        }
        else if (rand is >= 6 and < 57)
        {
            // TODO: retrieve days
            // GreetingText = string.Format(SH.ViewPageHomeGreetingTextEpic1, 0);
        }
        else if (rand is >= 57 and < 1000)
        {
            rand = Random.Shared.Next(0, 2);
            if (rand is 0)
            {
                // TODO: impl game launch times
                // GreetingText = string.Format(SH.ViewPageHomeGreetingTextCommon1, 0);
            }
            else if (rand is 1)
            {
                GreetingText = SH.FormatViewPageHomeGreetingTextCommon2(LocalSetting.Get(SettingKeys.LaunchTimes, 0));
            }
        }
    }

    private void RefreshDashboard()
    {
        taskContext.InvokeOnMainThread(InitializeDashboard);
    }

    private void MarkHomePendingForRetry()
    {
        Interlocked.Exchange(ref shouldRefreshDashboardOnSuccess, 1);
        networkRetryCoordinator.MarkPending("AnnouncementViewModel.LoadHome", SH.ViewModelMainNetworkConnectionFailedWillAutoRetry);
    }

    private void UpdateDisplayedActivityCards()
    {
        if (ActivityCalendar is null)
        {
            DisplayedActivityCards = [];
            return;
        }

        bool includeUnscheduledActs = LocalSetting.Get(SettingKeys.HomeAnnouncementActPreviewEnabled, false);

        ImmutableArray<Act> filtered = includeUnscheduledActs
            ? ActivityCalendar.CompositeActs
            : ActivityCalendar.CompositeActs.Where(static act => act.StartTimestamp > 0 && act.EndTimestamp > 0).ToImmutableArray();

        DisplayedActivityCards = filtered;
    }

    private void InitializeDashboard()
    {
        List<CardReference> result = [];

        // 游戏启动卡片放在最前：首页最常用的操作是「打开游戏」
        if (LocalSetting.Get(SettingKeys.IsHomeCardLaunchGamePresented, true))
        {
            result.Add(CardReference.Create(new LaunchGameCard(serviceProvider), SettingKeys.HomeCardLaunchGameOrder));
        }

        if (LocalSetting.Get(SettingKeys.IsHomeCardGachaStatisticsPresented, true))
        {
            result.Add(CardReference.Create(new GachaStatisticsCard(serviceProvider), SettingKeys.HomeCardGachaStatisticsOrder));
        }

        if (LocalSetting.Get(SettingKeys.IsHomeCardAchievementPresented, true))
        {
            result.Add(CardReference.Create(new AchievementCard(serviceProvider), SettingKeys.HomeCardAchievementOrder));
        }

        if (LocalSetting.Get(SettingKeys.IsHomeCardDailyNotePresented, true))
        {
            result.Add(CardReference.Create(new DailyNoteCard(serviceProvider), SettingKeys.HomeCardDailyNoteOrder));
        }

        if (LocalSetting.Get(SettingKeys.IsHomeCardTravelersDiaryPresented, true))
        {
            result.Add(CardReference.Create(new TravelersDiaryCard(serviceProvider), SettingKeys.HomeCardTravelersDiaryOrder));
        }

        if (LocalSetting.Get(SettingKeys.IsHomeCardCalendarPresented, true))
        {
            result.Add(CardReference.Create(new CalendarCard(serviceProvider), SettingKeys.HomeCardCalendarOrder));
        }

        if (LocalSetting.Get(SettingKeys.IsHomeCardSignInPresented, true))
        {
            result.Add(CardReference.Create(new SignInCard(serviceProvider), SettingKeys.HomeCardSignInOrder));
        }

        Cards = result.SortBy(r => r.Order);
    }
}
