// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.UI.Xaml.Data;

namespace Ellen.Launcher.Core.Database;

public interface IAdvancedDbCollectionView<TEntity> : IAdvancedCollectionView<TEntity>
    where TEntity : class, IPropertyValuesProvider
{
    IDisposable SuppressChangeCurrentItem();
}