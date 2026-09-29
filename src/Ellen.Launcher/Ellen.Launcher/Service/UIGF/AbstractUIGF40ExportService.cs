// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.InterChange.GachaLog;
using Ellen.Launcher.Model.Metadata.Abstraction;
using Ellen.Launcher.Service.GachaLog;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using System.Collections.Immutable;
using System.IO;

namespace Ellen.Launcher.Service.UIGF;

public abstract partial class AbstractUIGF40ExportService : IUIGFExportService
{
    protected readonly JsonSerializerOptions jsonOptions;
    protected readonly IServiceProvider serviceProvider;
    protected readonly ITaskContext taskContext;
    protected readonly IMetadataService metadataService;

    [GeneratedConstructor]
    public partial AbstractUIGF40ExportService(IServiceProvider serviceProvider);

    protected abstract string Version { get; }

    public virtual async ValueTask ExportAsync(UIGFExportOptions exportOptions, CancellationToken token = default)
    {
        await taskContext.SwitchToBackgroundAsync();

        GachaLogServiceMetadataContext metadataContext = await metadataService.GetContextAsync<GachaLogServiceMetadataContext>(token).ConfigureAwait(false);

        Model.InterChange.GachaLog.UIGF4 uigf = new()
        {
            Info = new()
            {
                ExportApp = "Ellen Launcher",
                ExportAppVersion = $"{EllenRuntime.Version}",
                ExportTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
                Version = Version,
            },
        };

        ExportGachaArchives(uigf, exportOptions.GachaArchiveUids, metadataContext);

        using (FileStream stream = File.Create(exportOptions.FilePath))
        {
            await JsonSerializer.SerializeAsync(stream, uigf, jsonOptions, token).ConfigureAwait(false);
        }
    }

    protected virtual void ExportGachaArchives(Model.InterChange.GachaLog.UIGF4 uigf, ImmutableArray<uint> uids, GachaLogServiceMetadataContext metadataContext)
    {
        if (uids.Length <= 0)
        {
            return;
        }

        IGachaLogRepository gachaLogRepository = serviceProvider.GetRequiredService<IGachaLogRepository>();

        ImmutableArray<UIGFEntry<Hk4eItem>>.Builder hk4eResults = ImmutableArray.CreateBuilder<UIGFEntry<Hk4eItem>>(uids.Length);

        foreach (ref readonly uint uid in uids.AsSpan())
        {
            GachaArchive? archive = gachaLogRepository.GetGachaArchiveByUid($"{uid}");
            ArgumentNullException.ThrowIfNull(archive);

            // Export standard gacha items
            ImmutableArray<GachaItem> dbItems = gachaLogRepository.GetGachaItemImmutableArrayByArchiveId(archive.InnerId);
            int timezone = InferRegionTimeZone(uid);

            UIGFEntry<Hk4eItem> hk4eEntry = new()
            {
                Uid = uid,
                TimeZone = timezone,
                List = dbItems.SelectAsArray(item =>
                {
                    INameQualityAccess nameQuality = metadataContext.GetNameQualityByItemId(item.ItemId);
                    string itemType = item.ItemId.StringLength switch
                    {
                        8U => SH.ModelInterchangeUIGFItemTypeAvatar,
                        5U => SH.ModelInterchangeUIGFItemTypeWeapon,
                        _ => string.Empty,
                    };
                    return Hk4eItem.From(item, nameQuality.Name, itemType, ((int)nameQuality.Quality).ToString(), timezone);
                }),
            };
            hk4eResults.Add(hk4eEntry);
        }

        uigf.Hk4e = hk4eResults.ToImmutable();
    }

    protected static int InferRegionTimeZone(uint uid)
    {
        char first = $"{uid}"[0];
        return first switch
        {
            '6' => -5,
            '7' => 1,
            _ => 8,
        };
    }
}
