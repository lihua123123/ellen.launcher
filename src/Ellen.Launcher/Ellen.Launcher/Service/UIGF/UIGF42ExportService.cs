// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.InterChange.GachaLog;
using Ellen.Launcher.Model.Metadata.Item;
using Ellen.Launcher.Service.GachaLog;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.UIGF;

[Service(ServiceLifetime.Transient, typeof(IUIGFExportService), Key = UIGFVersion.UIGF42)]
public sealed partial class UIGF42ExportService : AbstractUIGF40ExportService
{
    [GeneratedConstructor(CallBaseConstructor = true)]
    public partial UIGF42ExportService(IServiceProvider serviceProvider);

    protected override string Version { get; } = "v4.2";

    protected override UIGF4 CreateDocument(UIGFInfo info) => new UIGF42 { Info = info };

    protected override void ExportAdditionalGenshinData(UIGF4 uigf, IGachaLogRepository repository, GachaArchive archive, ulong uid, GachaLogServiceMetadataContext metadata)
    {
        ImmutableArray<BeyondGachaItem> items = repository.GetBeyondGachaItemImmutableArrayByArchiveId(archive.InnerId);
        if (items.IsDefaultOrEmpty)
        {
            return;
        }

        UIGF42 uigf42 = (UIGF42)uigf;
        int timezone = items[0].Time.Offset.Hours;
        UIGFEntry<Hk4eUGCItem> entry = new()
        {
            Uid = uid,
            TimeZone = timezone,
            List = items.SelectAsArray(item =>
            {
                metadata.IdBeyondItemMap.TryGetValue(item.ItemId, out BeyondItem? info);
                return Hk4eUGCItem.From(item, info?.Name ?? string.Empty, info?.TypeDescription ?? string.Empty, (info is null ? 0 : (int)info.RankLevel).ToString(), timezone);
            }),
        };
        uigf42.Hk4eUgc = uigf42.Hk4eUgc.IsDefault ? [entry] : uigf42.Hk4eUgc.Add(entry);
    }
}
