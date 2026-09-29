// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Database.Abstraction;
using Ellen.Launcher.Model;
using Ellen.Launcher.UI.Xaml.Data;
using System.Runtime.CompilerServices;

namespace Ellen.Launcher.Core.Database;

public static class AdvancedDbCollectionViewExtension
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IAdvancedDbCollectionView<TEntity> ToAdvancedDbCollectionView<TEntity>(this IList<TEntity> source, IServiceProvider serviceProvider)
        where TEntity : class, IPropertyValuesProvider, ISelectable
    {
        return new AdvancedDbCollectionView<TEntity>(source, serviceProvider);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IAdvancedDbCollectionView<TEntityAccess> ToAdvancedDbCollectionView<TEntityAccess, TEntity>(this IList<TEntityAccess> source, IServiceProvider serviceProvider)
        where TEntityAccess : class, IEntityAccess<TEntity>, IPropertyValuesProvider
        where TEntity : class, ISelectable
    {
        return new AdvancedDbCollectionView<TEntityAccess, TEntity>(source, serviceProvider);
    }
}