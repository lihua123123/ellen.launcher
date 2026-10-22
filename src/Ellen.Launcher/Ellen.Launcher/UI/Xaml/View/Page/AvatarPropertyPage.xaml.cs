// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using Ellen.Launcher.UI.Xaml.Control;
using Ellen.Launcher.ViewModel.AvatarProperty;

namespace Ellen.Launcher.UI.Xaml.View.Page;

public sealed partial class AvatarPropertyPage : ScopedPage
{
    public AvatarPropertyPage()
    {
        InitializeComponent();
    }

    protected override void LoadingOverride()
    {
        InitializeDataContext<AvatarPropertyViewModel>();
    }

    private void AvatarPropertyToggle_Checked(object sender, RoutedEventArgs e)
    {
        AnimateAvatarPropertyArrow(180);
    }

    private void AvatarPropertyToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        AnimateAvatarPropertyArrow(0);
    }

    private void AnimateAvatarPropertyArrow(double angle)
    {
        if (AvatarPropertyArrowRotation is null)
        {
            return;
        }

        DoubleAnimation animation = new()
        {
            To = angle,
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
            EnableDependentAnimation = true,
        };

        Storyboard.SetTarget(animation, AvatarPropertyArrowRotation);
        Storyboard.SetTargetProperty(animation, nameof(Microsoft.UI.Xaml.Media.RotateTransform.Angle));

        Storyboard storyboard = new();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }
}