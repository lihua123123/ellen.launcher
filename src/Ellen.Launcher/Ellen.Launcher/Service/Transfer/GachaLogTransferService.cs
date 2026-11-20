// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Factory.Picker;
using Ellen.Launcher.Model.InterChange.GachaLog;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Service.GachaLog;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.Service.UIGF;
using Ellen.Launcher.UI.Xaml.View.Dialog;
using System.Collections.Immutable;
using System.IO;

namespace Ellen.Launcher.Service.Transfer;

[Service(ServiceLifetime.Scoped, typeof(IGachaLogTransferService))]
public sealed partial class GachaLogTransferService : IGachaLogTransferService
{
    private readonly IUIGFService uigfService;
    private readonly IGachaLogRepository gachaLogRepository;
    private readonly IGachaLogService gachaLogService;
    private readonly IFileSystemPickerInteraction fileSystemPickerInteraction;
    private readonly IContentDialogFactory contentDialogFactory;
    private readonly IMessenger messenger;
    private readonly IServiceProvider serviceProvider;

    [GeneratedConstructor]
    public partial GachaLogTransferService(IServiceProvider serviceProvider);

    public async ValueTask<bool> ImportAsync()
    {
        FileSystemPickerOptions pickerOptions = new()
        {
            Title = SH.ViewModelGachaUIGFImportPickerTitile,
            FilterName = SH.ViewModelGachaLogExportFileType,
            FilterType = "*.json",
        };

        if (fileSystemPickerInteraction.PickFile(pickerOptions) is not (true, { HasValue: true } file))
        {
            return false;
        }

        if (!uigfService.Parse(await File.ReadAllTextAsync(file).ConfigureAwait(false), out UIGF4? uigf) || uigf is null)
        {
            messenger.Send(InfoBarMessage.Error(SH.ViewModelImportWarningTitle, SH.ViewModelImportWarningMessage));
            return false;
        }

        ImmutableArray<UIGFEntry<Hk4eUGCItem>> ugc = uigf is UIGF42 modern ? modern.Hk4eUgc : [];
        if (ContainsDuplicateUids(uigf.Hk4e) || ContainsDuplicateUids(ugc) || ContainsDuplicateUids(uigf.Nap))
        {
            messenger.Send(InfoBarMessage.Warning(SH.ViewModelUIGFImportDuplicatedHk4eEntry));
            return false;
        }

        ImmutableArray<UIGFUidSelection> accounts =
        [
            .. GetUids(uigf.Hk4e).Union(GetUids(ugc)).Select(uid => new UIGFUidSelection(GachaGameType.Genshin, uid)),
            .. GetUids(uigf.Nap, item => ZZZGachaLog.QueryTypes.Contains(item.GachaType)).Select(uid => new UIGFUidSelection(GachaGameType.ZZZ, uid)),
        ];
        if (accounts.IsEmpty)
        {
            messenger.Send(InfoBarMessage.Warning(SH.ViewModelUIGFImportNoHk4eEntry));
            return false;
        }

        UIGFImportDialog importDialog = await contentDialogFactory.CreateInstanceAsync<UIGFImportDialog>(serviceProvider, uigf, accounts).ConfigureAwait(false);
        if (await importDialog.GetSelectedAccountsAsync().ConfigureAwait(false) is not (true, { Count: > 0 } selected))
        {
            return false;
        }

        UIGFImportOptions options = new() { UIGF = uigf, Accounts = selected };
        ContentDialog dialog = await contentDialogFactory
            .CreateForIndeterminateProgressAsync(SH.ViewModelUIGFImportingProgressTitle)
            .ConfigureAwait(false);

        using (await contentDialogFactory.BlockAsync(dialog).ConfigureAwait(false))
        {
            try
            {
                await uigfService.ImportAsync(options).ConfigureAwait(false);
                foreach (GachaGameType game in selected.Select(static account => account.Game).Distinct())
                {
                    await gachaLogService.ReloadArchiveCollectionAsync(game).ConfigureAwait(false);
                }

                messenger.Send(InfoBarMessage.Success(SH.ViewModelUIGFImportSuccess));
                return true;
            }
            catch (Exception ex)
            {
                messenger.Send(InfoBarMessage.Error(SH.ViewModelUIGFImportError, ex));
                return false;
            }
        }
    }

    public async ValueTask<bool> ExportAsync(UIGFVersion version)
    {
        bool isLegacyVersion = version is UIGFVersion.UIGF22 or UIGFVersion.UIGF23 or UIGFVersion.UIGF24 or UIGFVersion.UIGF30;
        ImmutableArray<UIGFUidSelection> accounts =
        [
            .. GetLocalAccounts(GachaGameType.Genshin),
            .. (isLegacyVersion ? Enumerable.Empty<UIGFUidSelection>() : GetLocalAccounts(GachaGameType.ZZZ)),
        ];
        if (accounts.IsEmpty)
        {
            messenger.Send(InfoBarMessage.Warning(SH.ViewModelUIGFImportNoHk4eEntry));
            return false;
        }

        UIGFExportDialog exportDialog = await contentDialogFactory.CreateInstanceAsync<UIGFExportDialog>(serviceProvider, accounts, isLegacyVersion).ConfigureAwait(false);
        if (await exportDialog.GetSelectedAccountsAsync().ConfigureAwait(false) is not (true, { IsDefaultOrEmpty: false } selected))
        {
            return false;
        }

        await contentDialogFactory.TaskContext.SwitchToMainThreadAsync();
        FileSystemPickerOptions pickerOptions = new()
        {
            Title = SH.ViewModelGachaLogUIGFExportPickerTitle,
            DefaultFileName = $"Ellen Launcher {version}.json",
            FilterName = SH.ViewModelGachaLogExportFileType,
            FilterType = "*.json",
        };

        if (fileSystemPickerInteraction.SaveFile(pickerOptions) is not (true, { HasValue: true } file))
        {
            return false;
        }

        UIGFExportOptions options = new() { FilePath = file, Accounts = selected, Version = version };
        try
        {
            await uigfService.ExportAsync(options).ConfigureAwait(false);
            messenger.Send(InfoBarMessage.Success(SH.ViewModelUIGFExportSuccess));
            return true;
        }
        catch (Exception ex)
        {
            messenger.Send(InfoBarMessage.Error(SH.ViewModelUIGFExportError, ex));
            return false;
        }
    }

    private IEnumerable<UIGFUidSelection> GetLocalAccounts(GachaGameType game)
    {
        foreach (string value in gachaLogRepository.GetGachaArchiveUidImmutableArray(game).Distinct())
        {
            if (ulong.TryParse(value, out ulong uid) && uid != 0)
            {
                yield return new(game, uid);
            }
        }
    }

    private static bool ContainsDuplicateUids<TItem>(ImmutableArray<UIGFEntry<TItem>> entries)
    {
        return !entries.IsDefaultOrEmpty && entries.DistinctBy(entry => entry.Uid).Count() != entries.Length;
    }

    private static IEnumerable<ulong> GetUids<TItem>(ImmutableArray<UIGFEntry<TItem>> entries, Func<TItem, bool>? predicate = null)
    {
        foreach (UIGFEntry<TItem> entry in entries.IsDefault ? [] : entries)
        {
            if (!entry.List.IsDefaultOrEmpty && (predicate is null || entry.List.Any(predicate)))
            {
                yield return entry.Uid;
            }
        }
    }
}
