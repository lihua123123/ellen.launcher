// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Core.Threading.Abstraction;

public interface IAwaitable<out TAwaiter>
    where TAwaiter : IAwaiter
{
    TAwaiter GetAwaiter();
}

public interface IAwaitable<out TAwaiter, out TResult>
    where TAwaiter : IAwaiter<TResult>
{
    TAwaiter GetAwaiter();
}