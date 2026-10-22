// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Core.Database;
using Ellen.Launcher.Core.DependencyInjection.Abstraction;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.Service.SignIn;
using Ellen.Launcher.Service.User;
using Ellen.Launcher.Web.Hoyolab.Takumi.Event.BbsSignReward;
using System.Collections.Immutable;
using BindingUser = Ellen.Launcher.ViewModel.User.User;
using EntityUser = Ellen.Launcher.Model.Entity.User;

namespace Ellen.Launcher.ViewModel.SignIn;

/// <summary>
/// 首页「每日签到」卡片：列出所有已登录账号下所有可签到游戏的今日签到状态，
/// 并用一个「一键全签」按钮签完所有未签的角色。
/// </summary>
[BindableCustomPropertyProvider]
[Service(ServiceLifetime.Transient)]
public sealed partial class SignInViewModelSlim : Abstraction.ViewModelSlim
{
    private readonly IServiceProvider serviceProvider;
    private readonly IUserService userService;
    private readonly IGameRoleCollectionService roleCollectionService;
    private readonly IMessenger messenger;
    private readonly ITaskContext taskContext;

    [GeneratedConstructor(CallBaseConstructor = true)]
    public partial SignInViewModelSlim(IServiceProvider serviceProvider);

    [ObservableProperty]
    public partial List<SignInEntry>? Entries { get; set; }

    [ObservableProperty]
    public partial string? Summary { get; set; }

    [ObservableProperty]
    public partial bool HasEntries { get; set; }

    /// <summary>
    /// 所有角色今天都已签到。
    /// </summary>
    [ObservableProperty]
    public partial bool AllSigned { get; set; }

    [ObservableProperty]
    public partial bool IsSigningAll { get; set; }

    /// <summary>
    /// 「一键全签」是否可点：有条目、还有没签的、且当前没在签。
    /// </summary>
    public bool CanSignAll
    {
        get => HasEntries && !AllSigned && !IsSigningAll;
    }

    /// <inheritdoc/>
    protected override async Task LoadAsync()
    {
        try
        {
            await taskContext.SwitchToBackgroundAsync();

            using IServiceScope scope = serviceProvider.CreateScope();
            IOverseaSupportFactory<ISignInClient> signInClientFactory = scope.ServiceProvider.GetRequiredService<IOverseaSupportFactory<ISignInClient>>();

            // GetUsersAsync 会恢复全部账号（含用 SToken 刷新 cookie_token）并载入所有绑定的游戏角色
            AdvancedDbCollectionView<BindingUser, EntityUser> users = await userService.GetUsersAsync().ConfigureAwait(false);

            List<SignInEntry> entries = [];
            foreach (BindingUser user in users.Source)
            {
                // 该账号下的所有已注册游戏：原神 / 崩坏：星穹铁道 / 绝区零
                ImmutableArray<SignInGameRole> roles = await roleCollectionService.GetRolesAsync(user).ConfigureAwait(false);
                if (roles.IsEmpty)
                {
                    continue;
                }

                ISignInClient signInClient = signInClientFactory.Create(user.IsOversea);
                foreach (SignInGameRole role in roles)
                {
                    entries.Add(new(signInClient, taskContext, messenger, user.Entity, role));
                }
            }

            await taskContext.SwitchToMainThreadAsync();
            Entries = entries;
            HasEntries = entries.Count > 0;
            UpdateSummary();

            await Task.WhenAll(entries.Select(static entry => entry.UpdateAsync())).ConfigureAwait(false);

            await taskContext.SwitchToMainThreadAsync();
            UpdateSummary();
        }
        finally
        {
            await taskContext.SwitchToMainThreadAsync();
            IsInitialized = true;
        }
    }

    /// <summary>
    /// 一键全签：把所有未签到的角色依次签一遍（顺序执行，避免触发风控）。
    /// </summary>
    [Command("SignAllCommand")]
    private async Task SignAllAsync()
    {
        if (IsSigningAll || Entries is not { Count: > 0 } entries)
        {
            return;
        }

        IsSigningAll = true;

        try
        {
            foreach (SignInEntry entry in entries.Where(static entry => !entry.IsSign).ToArray())
            {
                await entry.SignAsync().ConfigureAwait(false);

                await taskContext.SwitchToMainThreadAsync();
                UpdateSummary();
            }
        }
        finally
        {
            await taskContext.SwitchToMainThreadAsync();
            IsSigningAll = false;
            UpdateSummary();
        }
    }

    partial void OnIsSigningAllChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSignAll));
    }

    partial void OnHasEntriesChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSignAll));
    }

    private void UpdateSummary()
    {
        if (Entries is not { Count: > 0 } entries)
        {
            Summary = null;
            AllSigned = false;
            OnPropertyChanged(nameof(CanSignAll));
            return;
        }

        int signed = entries.Count(static entry => entry.IsSign);
        Summary = SH.FormatViewCardSignInSummary(signed, entries.Count);
        AllSigned = signed == entries.Count;
        OnPropertyChanged(nameof(CanSignAll));
    }
}
