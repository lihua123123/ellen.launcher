// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.InterChange.GachaLog;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Service.GachaLog;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using System.Collections.Immutable;
using System.IO;

namespace Ellen.Launcher.Service.UIGF;

public abstract partial class AbstractUIGF3ExportService : IUIGFExportService
{
    protected readonly JsonSerializerOptions jsonOptions;
    protected readonly IServiceProvider serviceProvider;
    protected readonly ITaskContext taskContext;
    protected readonly IMetadataService metadataService;

    [GeneratedConstructor]
    public partial AbstractUIGF3ExportService(IServiceProvider serviceProvider);

    protected abstract string Version { get; }

    public virtual async ValueTask ExportAsync(UIGFExportOptions exportOptions, CancellationToken token = default)
    {
        await taskContext.SwitchToBackgroundAsync();

        (GachaGameType game, ulong uid) = exportOptions.Accounts.Single();
        if (game is not GachaGameType.Genshin)
        {
            throw new InvalidOperationException(SH.ViewModelUIGFImportNoHk4eEntry);
        }

        IGachaLogRepository repository = serviceProvider.GetRequiredService<IGachaLogRepository>();
        GachaArchive? archive = repository.GetGachaArchiveByUidAndGame($"{uid}", game);
        ArgumentNullException.ThrowIfNull(archive);

        ImmutableArray<GachaItem> items = repository.GetGachaItemImmutableArrayByArchiveId(archive.InnerId);
        if (items.IsDefaultOrEmpty)
        {
            throw new InvalidOperationException(SH.ViewModelUIGFImportNoHk4eEntry);
        }

        int timezone = items[0].Time.Offset.Hours;
        GachaLogServiceMetadataContext metadata = await metadataService.GetContextAsync<GachaLogServiceMetadataContext>(token).ConfigureAwait(false);
        UIGF3 uigf = new()
        {
            Info = new()
            {
                Uid = uid,
                ExportApp = "Ellen Launcher",
                ExportAppVersion = $"{EllenRuntime.Version}",
                ExportTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
                ExportTime = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                UigfVersion = Version,
                RegionTimeZone = timezone,
            },
            List = items.SelectAsArray(item => AbstractUIGF40ExportService.ExportGenshinItem(item, metadata, timezone)),
        };

        using FileStream stream = File.Create(exportOptions.FilePath);
        await JsonSerializer.SerializeAsync(stream, uigf, jsonOptions, token).ConfigureAwait(false);
    }
}
