// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.ViewModel.AvatarProperty;

namespace Ellen.Launcher.Service.AvatarInfo.Factory.Builder;

public static class ConstellationViewBuilderExtension
{
    public static TBuilder SetDescription<TBuilder>(this TBuilder builder, string description)
        where TBuilder : class, IConstellationViewBuilder
    {
        return builder.SetDescription<TBuilder, ConstellationView>(description);
    }

    public static TBuilder SetIcon<TBuilder>(this TBuilder builder, Uri icon)
        where TBuilder : class, IConstellationViewBuilder
    {
        return builder.SetIcon<TBuilder, ConstellationView>(icon);
    }

    public static TBuilder SetIsActivated<TBuilder>(this TBuilder builder, bool isActivated)
        where TBuilder : IConstellationViewBuilder
    {
        builder.View.IsActivated = isActivated;
        builder.View.ShowLock = !isActivated;
        return builder;
    }

    public static TBuilder SetSpecialDescription<TBuilder>(this TBuilder builder, string? SpecialDescription)
        where TBuilder : IConstellationViewBuilder
    {
        builder.View.SpecialDescription = SpecialDescription;
        return builder;
    }

    public static TBuilder SetName<TBuilder>(this TBuilder builder, string name)
        where TBuilder : class, IConstellationViewBuilder
    {
        return builder.SetName<TBuilder, ConstellationView>(name);
    }
}