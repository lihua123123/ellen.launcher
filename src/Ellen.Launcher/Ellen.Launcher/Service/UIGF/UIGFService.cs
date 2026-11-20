// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.InterChange.GachaLog;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.Web.Hoyolab;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.UIGF;

[Service(ServiceLifetime.Singleton, typeof(IUIGFService))]
public sealed partial class UIGFService : IUIGFService
{
    private readonly IServiceProvider serviceProvider;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly IMessenger messenger;

    [GeneratedConstructor]
    public partial UIGFService(IServiceProvider serviceProvider);

    public ValueTask ExportAsync(UIGFExportOptions exportOptions, CancellationToken token = default)
    {
        IUIGFExportService exportService = serviceProvider.GetRequiredKeyedService<IUIGFExportService>(exportOptions.Version);
        return exportService.ExportAsync(exportOptions, token);
    }

    public ValueTask ImportAsync(UIGFImportOptions importOptions, CancellationToken token = default)
    {
        UIGFVersion version = GetImportVersion(importOptions.UIGF.Info.Version);

        IUIGFImportService importService = serviceProvider.GetRequiredKeyedService<IUIGFImportService>(version);
        return importService.ImportAsync(importOptions, token);
    }

    public bool Parse(string json, out UIGF4? uigf)
    {
        uigf = null;
        try
        {
            UIGFView view = UIGFView.Create(json);
            uigf = (GetImportVersion(view.Version), view.IsLegacy) switch
            {
                (UIGFVersion.UIGF30, true) => ConvertFromLegacy(JsonSerializer.Deserialize<UIGF3>(json, jsonOptions)!),
                (UIGFVersion.UIGF40 or UIGFVersion.UIGF41, false) => JsonSerializer.Deserialize<UIGF4>(json, jsonOptions),
                (UIGFVersion.UIGF42, false) => JsonSerializer.Deserialize<UIGF42>(json, jsonOptions),
                _ => null,
            };

            if (uigf is not null)
            {
                ValidateAccounts(uigf.Hk4e);
                ValidateAccounts(uigf.Nap);
                if (uigf is UIGF42 modern)
                {
                    ValidateAccounts(modern.Hk4eUgc);
                }
            }
        }
        catch (Exception ex)
        {
            uigf = null;
            messenger.Send(InfoBarMessage.Error(SH.ViewModelImportWarningTitle, ex));
        }
        return uigf is not null;
    }

    private static UIGFVersion GetImportVersion(string? version)
    {
        return version switch
        {
            "v2.0" or "v2.1" or "v2.2" or "v2.3" or "v2.4" or "v3.0" => UIGFVersion.UIGF30,
            "v4.0" => UIGFVersion.UIGF40,
            "v4.1" => UIGFVersion.UIGF41,
            "v4.2" => UIGFVersion.UIGF42,
            _ => UIGFVersion.None,
        };
    }

    private static void ValidateAccounts<TItem>(ImmutableArray<UIGFEntry<TItem>> entries)
    {
        foreach (UIGFEntry<TItem> entry in entries.AsSpan())
        {
            if (entry.Uid is 0 || entry.TimeZone is < -14 or > 14 || entry.List.IsDefault)
            {
                throw new JsonException(SH.ViewModelImportWarningMessage);
            }
        }
    }

    private static UIGF4 ConvertFromLegacy(UIGF3 legacy)
    {
        UIGF4 result = new()
        {
            Info = new()
            {
                ExportApp = legacy.Info.ExportApp ?? string.Empty,
                ExportAppVersion = legacy.Info.ExportAppVersion ?? string.Empty,
                ExportTimestamp = legacy.Info.ExportTimestamp,
                Version = legacy.Info.UigfVersion,
            },
        };

        if (!legacy.List.IsDefaultOrEmpty)
        {
            UIGFEntry<Hk4eItem> entry = new()
            {
                Uid = legacy.Info.Uid,
                TimeZone = legacy.Info.RegionTimeZone ?? InferLegacyTimeZone(legacy.Info.Uid),
                Language = legacy.Info.Lang,
                List = legacy.List,
            };

            result.Hk4e = [entry];
        }

        return result;
    }

    private static int InferLegacyTimeZone(ulong uid)
    {
        string playerUid = $"{uid}";
        return HoyolabRegex.UidRegex.IsMatch(playerUid) ? PlayerUid.GetRegionTimeZoneUtcOffsetForUid(playerUid).Hours : 8;
    }
}
