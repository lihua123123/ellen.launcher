// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Factory.Picker;
using Ellen.Launcher.Model.InterChange.GachaLog;
using Ellen.Launcher.Service.GachaLog;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.Service.UIGF;
using Ellen.Launcher.UI.Xaml.View.Dialog;
using System.Collections.Immutable;
using System.IO;

namespace Ellen.Launcher.Service.Transfer;

/// <summary>
/// UIGF 导入导出的唯一实现。设置页与（保留的）命令都走这里。
/// </summary>
[Service(ServiceLifetime.Scoped, typeof(IGachaLogTransferService))]
public sealed partial class GachaLogTransferService : IGachaLogTransferService
{
    private readonly IUIGFService uigfService;
    private readonly IGachaLogRepository gachaLogRepository;
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

        if (!uigfService.Parse(await File.ReadAllTextAsync(file).ConfigureAwait(false), out UIGF4? uigf))
        {
            messenger.Send(InfoBarMessage.Error(SH.ViewModelImportWarningTitle, SH.ViewModelImportWarningMessage));
            return false;
        }

        bool hasGenshin = !uigf!.Hk4e.IsDefaultOrEmpty;
        bool hasZZZ = uigf is UIGF42 uigf42 && !uigf42.Nap.IsDefaultOrEmpty;

        // UIGF v4.2 的绝区零记录位于 nap 字段；原神记录位于 hk4e 字段。两者任一存在即可导入。
        if (!hasGenshin && !hasZZZ)
        {
            messenger.Send(InfoBarMessage.Warning(SH.ViewModelUIGFImportNoHk4eEntry));
            return false;
        }

        if (uigf.Hk4e.Select(entry => entry.Uid).ToHashSet().Count != uigf.Hk4e.Length)
        {
            messenger.Send(InfoBarMessage.Warning(SH.ViewModelUIGFImportDuplicatedHk4eEntry));
            return false;
        }

        UIGFImportDialog importDialog = await contentDialogFactory.CreateInstanceAsync<UIGFImportDialog>(serviceProvider, uigf).ConfigureAwait(false);
        if (await importDialog.GetSelectedUidsAsync().ConfigureAwait(false) is not (true, { } uids))
        {
            return false;
        }

        if (uids is null or { Count: 0 })
        {
            messenger.Send(InfoBarMessage.Warning(SH.ViewModelUIGFImportNoSelectedEntry));
            return false;
        }

        UIGFImportOptions options = new()
        {
            UIGF = uigf,
            GachaArchiveUids = uids,
        };

        ContentDialog dialog = await contentDialogFactory
            .CreateForIndeterminateProgressAsync(SH.ViewModelUIGFImportingProgressTitle)
            .ConfigureAwait(false);

        using (await contentDialogFactory.BlockAsync(dialog).ConfigureAwait(false))
        {
            try
            {
                await uigfService.ImportAsync(options).ConfigureAwait(false);
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

        ImmutableArray<uint> allUids = [.. gachaLogRepository.GetGachaArchiveUidImmutableArray().Select(uint.Parse)];
        bool isLegacyVersion = version is UIGFVersion.UIGF22 or UIGFVersion.UIGF23 or UIGFVersion.UIGF24 or UIGFVersion.UIGF30;
        UIGFExportDialog exportDialog = await contentDialogFactory.CreateInstanceAsync<UIGFExportDialog>(serviceProvider, allUids, isLegacyVersion).ConfigureAwait(false);
        if (await exportDialog.GetSelectedUidsAsync().ConfigureAwait(false) is not (true, { } uids))
        {
            return false;
        }

        UIGFExportOptions options = new()
        {
            FilePath = file,
            GachaArchiveUids = uids,
            Version = version,
        };

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
}
