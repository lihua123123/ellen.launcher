// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.EntityFrameworkCore;
using Ellen.Launcher.Model.Entity.Database;
using System.Security.Cryptography;
using System.Text;
using Windows.Storage;

namespace Ellen.Launcher.Core.Diagnostics;

[Service(ServiceLifetime.Singleton, typeof(IEllenDiagnostics))]
public sealed partial class EllenDiagnostics : IEllenDiagnostics
{
    private readonly IServiceProvider serviceProvider;

    [GeneratedConstructor]
    public partial EllenDiagnostics(IServiceProvider serviceProvider);

    public DiagnosticsSettingsProvider LocalSettings { get; } = new();

    public async ValueTask<int> ExecuteSqlAsync(string sql)
    {
        using (IServiceScope scope = serviceProvider.CreateScope())
        {
            AppDbContext appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await appDbContext.Database.ExecuteSqlRawAsync(sql).ConfigureAwait(false);
        }
    }

    public ApplicationDataCompositeValue MakeApplicationDataCompositeValue(params string[] values)
    {
        ApplicationDataCompositeValue compositeValue = [];
        foreach (string value in values)
        {
            compositeValue.Add($"{CryptographicOperations.HashData(HashAlgorithmName.MD5, Encoding.UTF8.GetBytes(value)):X}", value);
        }

        return compositeValue;
    }
}
