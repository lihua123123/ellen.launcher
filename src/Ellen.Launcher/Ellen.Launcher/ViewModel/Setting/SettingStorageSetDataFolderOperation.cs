// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Core;
using Ellen.Launcher.Core.Setting;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Factory.Picker;
using System.IO;

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

        /* 只登记目标目录，真正的搬运放到下一次启动、数据库还没打开的时候做。
           原因：这里复制时 SQLite 已经把 Userdata.db 打开了，复制出来的是个空壳；
           更糟的是搬完就切目录，用户看到的是「记录全没了」。 */
        LocalSetting.Set(SettingKeys.PendingDataDirectory, newFolderPath);

        /* 不再设置 PreviousDataDirectoryToDelete：默认布局下旧目录就是
           %LOCALAPPDATA%\EllenLauncher，settings.json 也在里面，下次启动把它删掉
           会连配置一起清空（欢迎页从此每次重启都回到第一步）。
           旧目录原样保留，当作备份，由用户自己决定什么时候删。 */
        return true;
    }
}