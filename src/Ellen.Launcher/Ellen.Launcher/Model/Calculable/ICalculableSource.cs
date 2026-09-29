// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model.Calculable;

public interface ICalculableSource<out TResult>
    where TResult : ICalculable
{
    public TResult ToCalculable();
}