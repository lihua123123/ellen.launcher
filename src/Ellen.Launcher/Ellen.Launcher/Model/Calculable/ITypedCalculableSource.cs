// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model.Calculable;

public interface ITypedCalculableSource<out TResult, in TType>
    where TResult : ICalculable
{
    public TResult ToCalculable(TType param);
}