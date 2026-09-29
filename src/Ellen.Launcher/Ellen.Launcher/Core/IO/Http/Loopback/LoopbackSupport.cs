// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Win32;

namespace Ellen.Launcher.Core.IO.Http.Loopback;

[Service(ServiceLifetime.Singleton)]
public sealed partial class LoopbackSupport : ObservableObject
{
    private readonly EllenNativeLoopbackSupport native;
    private readonly string ellenContainerStringSid;

    public LoopbackSupport(IServiceProvider serviceProvider)
    {
        native = EllenNative.Instance.MakeLoopbackSupport();
        try
        {
            if (!native.IsPublicFirewallEnabled)
            {
                IsLoopbackEnabled = false;
                ellenContainerStringSid = string.Empty;
                return;
            }

            IsLoopbackEnabled = native.IsEnabled(EllenRuntime.FamilyName, out string? sid);
            ellenContainerStringSid = sid ?? string.Empty;
        }
        catch
        {
            IsLoopbackEnabled = false;
            ellenContainerStringSid = string.Empty;
        }

#pragma warning disable SA1116, SA1117
        SentrySdk.ConfigureScope(static (scope, state) =>
        {
            Dictionary<string, object> loopback = new()
            {
                ["Enabled"] = state.Enabled,
                ["Sid"] = state.Sid,
            };

            scope.Contexts["Loopback"] = loopback;
        }, (Sid: ellenContainerStringSid, Enabled: IsLoopbackEnabled));
#pragma warning restore SA1116, SA1117
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEnableLoopback))]
    public partial bool IsLoopbackEnabled { get; private set; }

    public bool CanEnableLoopback
    {
        get => EllenRuntime.IsProcessElevated && !IsLoopbackEnabled && !string.IsNullOrEmpty(ellenContainerStringSid);
    }

    public void EnableLoopback()
    {
        if (!CanEnableLoopback)
        {
            return;
        }

        native.Enable(ellenContainerStringSid);
        IsLoopbackEnabled = true;
    }
}