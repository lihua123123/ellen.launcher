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

    private void UpdatePivotItems()
    {
        if (AssociatedObject is null || overviewItem is null)
        {
            return;
        }

        // 千星奇域（UGC）模式只保留「总览」页签；
        // 绝区零的统计与计时页签已按调频记录适配，不再隐藏。
        AssociatedObject.Items.Clear();
        AssociatedObject.Items.Add(overviewItem);

        if (!IsBeyondMode.GetValueOrDefault())
        {
            foreach (PivotItem item in normalOnlyItems)
            {
                AssociatedObject.Items.Add(item);
            }
        }
    }
}
