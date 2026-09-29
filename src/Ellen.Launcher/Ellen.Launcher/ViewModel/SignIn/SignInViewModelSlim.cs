// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Core.Database;
using Ellen.Launcher.Core.DependencyInjection.Abstraction;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.Service.User;
using Ellen.Launcher.ViewModel.User;
using Ellen.Launcher.Web.Hoyolab.Takumi.Binding;
using Ellen.Launcher.Web.Hoyolab.Takumi.Event.BbsSignReward;
using BindingUser = Ellen.Launcher.ViewModel.User.User;
using EntityUser = Ellen.Launcher.Model.Entity.User;

namespace Ellen.Launcher.ViewModel.SignIn;

/// <summary>
/// 首页「每日签到」卡片：列出所有已登录账号（角色）的今日签到状态。
/// </summary>
[BindableCustomPropertyProvider]
[Service(ServiceLifetime.Transient)]
public sealed partial class SignInViewModelSlim : Abstraction.ViewModelSlim
{
    private readonly IServiceProvider serviceProvider;
    private readonly IUserService userService;
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
                if (user.UserGameRoles.Count is 0)
                {
                    continue;
                }

                ISignInClient signInClient = signInClientFactory.Create(user.IsOversea);
                foreach (UserGameRole role in user.UserGameRoles)
                {
                    SignInEntry entry = new(signInClient, taskContext, messenger, UserAndUid.From(user.Entity, role), role);
                    entry.PropertyChanged += OnEntryPropertyChanged;
                    entries.Add(entry);
                }
            }

            await taskContext.SwitchToMainThreadAsync();
            Entries = entries;
            HasEntries = entries.Count > 0;
            UpdateSummary();

            await Task.WhenAll(entries.Select(static entry => entry.UpdateAsync())).ConfigureAwait(false);
        }
        finally
        {
            await taskContext.SwitchToMainThreadAsync();
            IsInitialized = true;
        }
    }

    [Command("RefreshCommand")]
    private async Task RefreshAsync()
    {
        if (Entries is not { Count: > 0 } entries)
        {
            await LoadAsync().ConfigureAwait(false);
            return;
        }

        await Task.WhenAll(entries.Select(static entry => entry.UpdateAsync())).ConfigureAwait(false);
    }

    private void OnEntryPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SignInEntry.IsSign))
        {
            UpdateSummary();
        }
    }

    private void UpdateSummary()
    {
        if (Entries is not { Count: > 0 } entries)
        {
            Summary = null;
            return;
        }

        Summary = SH.FormatViewCardSignInSummary(entries.Count(static entry => entry.IsSign), entries.Count);
    }
}
