// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.EntityFrameworkCore.Design;

namespace Ellen.Launcher.Model.Entity.Database;

/// <summary>
/// 此类只用于在生成迁移时提供数据库上下文
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <inheritdoc/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public AppDbContext CreateDbContext(string[] args)
    {
#if DEBUG
        // TODO: replace with your own database file path.
        string userdataDbName = @"D:\Ellen\Userdata.db";
        return AppDbContext.Create(default!, $"Data Source={userdataDbName}");
#else
        throw Core.ExceptionService.EllenException.NotSupported();
#endif
    }
}
