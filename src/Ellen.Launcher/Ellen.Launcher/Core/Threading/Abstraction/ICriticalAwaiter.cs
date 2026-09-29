// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Runtime.CompilerServices;

namespace Ellen.Launcher.Core.Threading.Abstraction;

public interface ICriticalAwaiter : IAwaiter, ICriticalNotifyCompletion;

public interface ICriticalAwaiter<out TResult> : IAwaiter<TResult>, ICriticalNotifyCompletion;