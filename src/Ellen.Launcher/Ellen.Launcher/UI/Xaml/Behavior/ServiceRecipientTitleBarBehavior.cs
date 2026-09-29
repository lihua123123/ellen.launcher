// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.
using CommunityToolkit.WinUI.Behaviors;
using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Service.Navigation.Message;
using Ellen.Launcher.UI.Content;

namespace Ellen.Launcher.UI.Xaml.Behavior;

public sealed class ServiceRecipientTitleBarBehavior : BehaviorBase<TitleBar>
{
    protected override void OnAssociatedObjectLoaded()
    {
        AssociatedObject.BackRequested += OnBackRequested;
        AssociatedObject.PaneToggleRequested += OnPaneToggleRequested;
    }

    protected override bool Uninitialize()
    {
        AssociatedObject.BackRequested -= OnBackRequested;
        AssociatedObject.PaneToggleRequested -= OnPaneToggleRequested;
        return base.Uninitialize();
    }

    private static void OnBackRequested(TitleBar sender, object args)
    {
        sender.XamlRoot.XamlContext()?.ServiceProvider.GetRequiredService<IMessenger>().Send(new NavigationGoBackMessage());
    }

    private static void OnPaneToggleRequested(TitleBar sender, object args)
    {
        sender.XamlRoot.XamlContext()?.ServiceProvider.GetRequiredService<IMessenger>().Send(new NavigationPaneToggleMessage());
    }
}