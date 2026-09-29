// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Windows.ApplicationModel;
using Windows.Storage;

namespace Ellen.Launcher.Core;

public static class InstalledLocation
{
    public static string GetAbsolutePath(string relativePath)
    {
        string basePath = RuntimeEnvironment.IsPackaged
            ? Package.Current.InstalledLocation.Path
            : AppContext.BaseDirectory;

        return Path.Combine(basePath, relativePath);
    }

    public static Uri ToAbsoluteUri(Uri msAppxUri)
    {
        if (RuntimeEnvironment.IsPackaged)
        {
            return msAppxUri;
        }

        // Convert ms-appx:///Resource/Icon/foo.png to absolute file path
        string relativePath = msAppxUri.OriginalString.Replace("ms-appx:///", string.Empty).Replace('/', Path.DirectorySeparatorChar);
        return new(Path.Combine(AppContext.BaseDirectory, relativePath));
    }

    public static Uri ToAbsoluteUri(string msAppxUriString)
    {
        return ToAbsoluteUri(new Uri(msAppxUriString));
    }

    public static void CopyFileFromApplicationUri(string url, string path)
    {
        if (RuntimeEnvironment.IsPackaged)
        {
            CopyApplicationUriFileCoreAsync(url, path).GetAwaiter().GetResult();
        }
        else
        {
            // In unpackaged mode, resolve ms-appx URI to file path relative to base directory
            CopyFileFromAppxPath(url, path);
        }

        static async Task CopyApplicationUriFileCoreAsync(string url, string path)
        {
            await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(url.ToUri());
            using (Stream outputStream = (await file.OpenReadAsync()).AsStreamForRead())
            {
                EnsureFileAccessControl(path);

                using (FileStream inputStream = File.Create(path))
                {
                    await outputStream.CopyToAsync(inputStream).ConfigureAwait(false);
                }
            }
        }

        static void CopyFileFromAppxPath(string url, string path)
        {
            // ms-appx:///Assets/Logo.ico -> Assets\Logo.ico relative to base directory
            string relativePath = url.Replace("ms-appx:///", string.Empty).Replace('/', Path.DirectorySeparatorChar);
            string sourcePath = Path.Combine(AppContext.BaseDirectory, relativePath);

            EnsureFileAccessControl(path);

            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, path, overwrite: true);
            }
        }

        static void EnsureFileAccessControl(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    FileInfo fileInfo = new(path);
                    FileSecurity fileSecurity = fileInfo.GetAccessControl();
                    SecurityIdentifier? user = WindowsIdentity.GetCurrent().User;

                    if (user is not null)
                    {
                        fileSecurity.AddAccessRule(new(user, FileSystemRights.FullControl, InheritanceFlags.None, PropagationFlags.NoPropagateInherit, AccessControlType.Allow));
                        fileInfo.SetAccessControl(fileSecurity);
                    }
                }
                catch
                {
                    // Ignore
                }
            }
        }
    }
}