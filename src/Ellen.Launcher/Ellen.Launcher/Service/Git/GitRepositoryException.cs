// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.Git;

public sealed class GitRepositoryException : AggregateException
{
    public GitRepositoryException(string message, IEnumerable<Exception> innerExceptions)
        : base(message, innerExceptions)
    {
    }
}