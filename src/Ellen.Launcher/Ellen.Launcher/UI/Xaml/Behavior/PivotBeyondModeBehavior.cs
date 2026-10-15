// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.WinUI.Behaviors;
using JetBrains.Annotations;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT;

namespace Ellen.Launcher.UI.Xaml.Behavior;

[UsedImplicitly]
[DependencyProperty<bool>("IsBeyondMode", PropertyChangedCallbackName = nameof(OnIsBeyondModeChanged))]
[DependencyProperty<bool>("IsGameZZZMode", PropertyChangedCallbackName = nameof(OnIsGameZZZModeChanged))]
public sealed partial class PivotBeyondModeBehavior : BehaviorBase<Pivot>
{
    private PivotItem? overviewItem;
    private readonly List<PivotItem> normalOnlyItems = [];
    private bool isBeyondModeInitialized;

    protected override void OnAssociatedObjectLoaded()
    {
        if (overviewItem is not null)
        {
            return;
        }

        if (AssociatedObject.Items.Count == 0)
        {
            return;
        }

        overviewItem = (PivotItem)AssociatedObject.Items[0];
        for (int i = 1; i < AssociatedObject.Items.Count; i++)
        {
            normalOnlyItems.Add((PivotItem)AssociatedObject.Items[i]);
        }

        isBeyondModeInitialized = true;
    }

    private static void OnIsBeyondModeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs _)
    {
        PivotBeyondModeBehavior behavior = sender.As<PivotBeyondModeBehavior>();
        if (behavior.AssociatedObject is null || behavior.overviewItem is null || !behavior.isBeyondModeInitialized)
        {
            return;
        }

        behavior.UpdatePivotItems();
    }

    private static void OnIsGameZZZModeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs _)
    {
        PivotBeyondModeBehavior behavior = sender.As<PivotBeyondModeBehavior>();
        if (behavior.AssociatedObject is null || behavior.overviewItem is null || !behavior.isBeyondModeInitialized)
        {
            return;
        }

        behavior.UpdatePivotItems();
    }

    private void UpdatePivotItems()
    {
        if (AssociatedObject is null || overviewItem is null)
        {
            return;
        }

        // 千星奇域（UGC）模式与绝区零模式都只保留「总览」页签
        bool hideNormalOnlyItems = IsBeyondMode.GetValueOrDefault() || IsGameZZZMode.GetValueOrDefault();

        AssociatedObject.Items.Clear();
        AssociatedObject.Items.Add(overviewItem);

        if (!hideNormalOnlyItems)
        {
            foreach (PivotItem item in normalOnlyItems)
            {
                AssociatedObject.Items.Add(item);
            }
        }
    }
}
