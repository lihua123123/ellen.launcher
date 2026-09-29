// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Model;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Service;
using System.Collections.Immutable;

namespace Ellen.Launcher.UI.Xaml.View.Dialog;

[DependencyProperty<string>("Text")]
[DependencyProperty<NameValue<TimeSpan>>("SelectedServerTimeZoneOffset", CreateDefaultValueCallbackName = nameof(CreateSelectedServerTimeZoneOffsetDefaultValue))]
[DependencyProperty<bool>("IsUidAttached", NotNull = true)]
public sealed partial class CultivateProjectDialog : ContentDialog
{
    private readonly IContentDialogFactory contentDialogFactory;

    [GeneratedConstructor(InitializeComponent = true)]
    public partial CultivateProjectDialog(IServiceProvider serviceProvider);

    [SuppressMessage("", "SA1201")]
    [SuppressMessage("", "CA1822")]
    public ImmutableArray<NameValue<TimeSpan>> ServerTimeZoneOffsets { get => KnownServerRegionTimeZones.Value; }

    public async ValueTask<ValueResult<bool, CultivateProject>> CreateProjectAsync()
    {
        if (await contentDialogFactory.EnqueueAndShowAsync(this).ShowTask.ConfigureAwait(false) is not ContentDialogResult.Primary)
        {
            return new(false, default!);
        }

        await contentDialogFactory.TaskContext.SwitchToMainThreadAsync();
        if (string.IsNullOrWhiteSpace(Text) || SelectedServerTimeZoneOffset is null)
        {
            return new(false, default!);
        }

        return new(true, CultivateProject.From(Text, SelectedServerTimeZoneOffset.Value));
    }

    private static object CreateSelectedServerTimeZoneOffsetDefaultValue()
    {
        return KnownServerRegionTimeZones.Value.First();
    }
}