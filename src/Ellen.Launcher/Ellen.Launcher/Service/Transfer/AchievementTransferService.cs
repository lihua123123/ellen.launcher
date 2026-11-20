// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.IO;
using Ellen.Launcher.Factory.Picker;
using Ellen.Launcher.Model.InterChange.Achievement;
using Ellen.Launcher.Service.Game.Island;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.ViewModel.Achievement;

namespace Ellen.Launcher.Service.Transfer;

[Service(ServiceLifetime.Scoped, typeof(IAchievementTransferService))]
public sealed partial class AchievementTransferService : IAchievementTransferService
{
    private readonly AchievementViewModelScopeContext scopeContext;
    private readonly IEmbeddedYaeGate embeddedYaeGate;

    [GeneratedConstructor]
    public partial AchievementTransferService(IServiceProvider serviceProvider);

    public ValueTask<bool> ImportFromFileAsync()
    {
        return scopeContext.AchievementImporter.FromFileAsync(scopeContext);
    }

    public ValueTask<bool> ImportFromClipboardAsync()
    {
        return scopeContext.AchievementImporter.FromClipboardAsync(scopeContext);
    }

    public async ValueTask<bool> ImportFromEmbeddedYaeAsync()
    {
        if (!await embeddedYaeGate.EnsureElevatedAsync().ConfigureAwait(false))
        {
            return false;
        }

        if (!await embeddedYaeGate.EnsureConfirmedAsync().ConfigureAwait(false))
        {
            return false;
        }

        return await scopeContext.AchievementImporter.FromEmbeddedYaeAsync(scopeContext).ConfigureAwait(false);
    }

    public async ValueTask<bool> ExportAsync()
    {
        if (await scopeContext.AchievementService.GetArchiveCollectionAsync().ConfigureAwait(false) is not { CurrentItem: { } archive })
        {
            scopeContext.Messenger.Send(InfoBarMessage.Warning(SH.ViewModelImportWarningTitle, SH.ViewModelImportWarningMessage2));
            return false;
        }

        FileSystemPickerOptions pickerOptions = new()
        {
            Title = SH.ViewModelAchievementUIAFExportPickerTitle,
            DefaultFileName = $"{archive.Name}.json",
            FilterName = SH.ViewModelAchievementExportFileType,
            FilterType = "*.json",
        };

        if (scopeContext.FileSystemPickerInteraction.SaveFile(pickerOptions) is not (true, { HasValue: true } file))
        {
            return false;
        }

        UIAF uiaf = await scopeContext.AchievementService.ExportToUIAFAsync(archive).ConfigureAwait(false);

        InfoBarMessage message = await file.SerializeToJsonNoThrowAsync(uiaf, scopeContext.JsonSerializerOptions).ConfigureAwait(false)
            ? InfoBarMessage.Success(SH.ViewModelExportSuccessTitle, SH.ViewModelExportSuccessMessage)
            : InfoBarMessage.Warning(SH.ViewModelExportWarningTitle, SH.ViewModelExportWarningMessage);

        scopeContext.Messenger.Send(message);
        return true;
    }
}
