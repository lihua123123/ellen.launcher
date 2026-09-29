// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Core.Property;
using Ellen.Launcher.Model.Entity.Database;
using System.Runtime.CompilerServices;

namespace Ellen.Launcher.Service.Abstraction.Property;

public abstract class DbProperty<T> : ObservableObject, IObservableProperty<T>
{
    private bool deferring;

    public abstract T Value { get; set; }

    protected ref bool Deferring
    {
        get => ref deferring;
    }

    public INotifyPropertyChangedDeferral GetDeferral()
    {
        if (Interlocked.Exchange(ref deferring, true))
        {
            throw EllenException.InvalidOperation("Already deferring");
        }

        return NotifyPropertyChangedDeferral.Create(this, static self =>
        {
            if (!Interlocked.Exchange(ref self.deferring, false))
            {
                throw EllenException.InvalidOperation("Not deferring");
            }

            self.OnPropertyChanged(nameof(Value));
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    protected static string? GetValue(AppDbContext appDbContext, string key)
    {
        // This method is separated to avoid implicit capture of the key
        return appDbContext.Settings.SingleOrDefault(e => e.Key == key)?.Value;
    }

    protected abstract void SetValue(T value);
}