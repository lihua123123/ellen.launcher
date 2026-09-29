// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Core;
using Ellen.Launcher.Core.Setting;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Factory.Picker;
using Ellen.Launcher.Service.Notification;
using System.IO;
using Windows.Storage;

namespace Ellen.Launcher.ViewModel.Setting;

public sealed class SettingStorageSetDataFolderOperation
{
    public required IFileSystemPickerInteraction FileSystemPickerInteraction { private get; init; }

    public required IContentDialogFactory ContentDialogFactory { private get; init; }

    public required IMessenger Messenger { get; init; }

    public async ValueTask<bool> TryExecuteAsync()
    {
        if (!FileSystemPickerInteraction.PickFolder().TryGetValue(out string? newFolderPath))
        {
            return false;
        }

        string oldFolderPath = EllenRuntime.DataDirectory;
        if (UrlPath.IsEqualOrSubdirectory(oldFolderPath, newFolderPath))
        {
            return false;
        }

        if (Path.GetDirectoryName(newFolderPath) is null)
        {
            await ContentDialogFactory.CreateForConfirmAsync(
                    SH.ViewModelSettingStorageSetDataFolderTitle,
                    SH.ViewModelSettingStorageSetDataFolderDescription2)
                .ConfigureAwait(false);

            return false;
        }

        Directory.CreateDirectory(newFolderPath);
        IEnumerable<string> entries;
        try
        {
            entries = Directory.EnumerateDirectories(newFolderPath);
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }

        if (entries.Any())
        {
            ContentDialogResult result = await ContentDialogFactory.CreateForConfirmCancelAsync(
                    SH.ViewModelSettingStorageSetDataFolderTitle,
                    SH.FormatViewModelSettingStorageSetDataFolderDescription3(newFolderPath))
                .ConfigureAwait(false);

            if (result is not ContentDialogResult.Primary)
            {
                return false;
            }
        }

        try
        {
            Directory.SetReadOnly(oldFolderPath, false);
            StorageFolder oldFolder = await StorageFolder.GetFolderFromPathAsync(oldFolderPath);
            await oldFolder.CopyAsync(newFolderPath).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Messenger.Send(InfoBarMessage.Error(ex));
            return false;
        }

        LocalSetting.Set(SettingKeys.PreviousDataDirectoryToDelete, oldFolderPath);
        LocalSetting.Set(SettingKeys.DataDirectory, newFolderPath);
        return true;
    }
}