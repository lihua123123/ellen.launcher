// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Core.Property;

public interface IObservableProperty<T> : IProperty<T>, INotifyPropertyChanged
{
    INotifyPropertyChangedDeferral GetDeferral();
}