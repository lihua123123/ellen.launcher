// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Property;
using Ellen.Launcher.Core.Setting;
using Ellen.Launcher.Model;
using Ellen.Launcher.Service.Abstraction;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.DailyNote;

[Service(ServiceLifetime.Singleton)]
public sealed partial class DailyNoteOptions : DbStoreOptions
{
    private const int OneMinute = 60;

    [GeneratedConstructor(CallBaseConstructor = true)]
    public partial DailyNoteOptions(IServiceProvider serviceProvider);

    public ImmutableArray<NameValue<int>> RefreshTimes { get; } =
    [
        new(SH.ViewModelDailyNoteRefreshTime4, OneMinute * 4),
        new(SH.ViewModelDailyNoteRefreshTime8, OneMinute * 8),
        new(SH.ViewModelDailyNoteRefreshTime30, OneMinute * 30),
        new(SH.ViewModelDailyNoteRefreshTime40, OneMinute * 40),
        new(SH.ViewModelDailyNoteRefreshTime60, OneMinute * 60),
    ];

    [field: MaybeNull]
    public IObservableProperty<bool> IsAutoRefreshEnabled { get => field ??= CreateProperty(SettingKeys.DailyNoteIsAutoRefreshEnabled, false); }

    [field: MaybeNull]
    public IObservableProperty<NameValue<int>?> SelectedRefreshTime { get => field ??= CreateProperty(SettingKeys.DailyNoteRefreshSeconds, OneMinute * 30).AsNameValue(RefreshTimes); }

    [field: MaybeNull]
    public IObservableProperty<bool> IsReminderNotification { get => field ??= CreateProperty(SettingKeys.DailyNoteReminderNotify, false); }

    [field: MaybeNull]
    public IObservableProperty<bool> IsSilentWhenPlayingGame { get => field ??= CreateProperty(SettingKeys.DailyNoteSilentWhenPlayingGame, false); }

    [field: MaybeNull]
    public IObservableProperty<string?> WebhookUrl { get => field ??= CreateProperty(SettingKeys.DailyNoteWebhookUrl); }
}