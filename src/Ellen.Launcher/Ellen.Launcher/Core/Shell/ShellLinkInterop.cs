// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.IO;
using System.IO;

namespace Ellen.Launcher.Core.Shell;

[Service(ServiceLifetime.Transient, typeof(IShellLinkInterop))]
public sealed class ShellLinkInterop : IShellLinkInterop
{
    public bool TryCreateDesktopShortcut()
    {
        string targetLogoPath = EllenRuntime.GetDataDirectoryFile("ShellLinkLogo.ico");

        try
        {
            InstalledLocation.CopyFileFromApplicationUri("ms-appx:///Assets/Logo.ico", targetLogoPath);

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string target = Path.Combine(desktop, $"{SH.AppName}.lnk");

            if (RuntimeEnvironment.IsPackaged)
            {
                // In packaged mode, use AUMID to reference the app
                FileSystem.CreateLink($"shell:appsFolder\\{EllenRuntime.FamilyName}!App", "", targetLogoPath, target);
            }
            else
            {
                // In unpackaged mode, point to the executable directly
                string executablePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                    ?? Path.Combine(AppContext.BaseDirectory, $"{System.Reflection.Assembly.GetEntryAssembly()?.GetName()?.Name}.exe");
                FileSystem.CreateLink(executablePath, "", targetLogoPath, target);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool TryCreateGameLaunchShortcut()
    {
        string targetLogoPath = EllenRuntime.GetDataDirectoryFile("ShellLinkLogo.ico");

        try
        {
            InstalledLocation.CopyFileFromApplicationUri("ms-appx:///Assets/Logo.ico", targetLogoPath);

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string shortcutFileName = $"{SH.AppName} - {SH.ViewPageLaunchGameAction}.lnk";
            string target = Path.Combine(desktop, shortcutFileName);

            // Try creating a .lnk via the native COM CreateLink with ellen://launch URI as target
            try
            {
                FileSystem.CreateLink("ellen://launch", "", targetLogoPath, target);
                return true;
            }
            catch
            {
                // Fallback: create a .url file (Internet Shortcut) which natively supports protocol URIs
                string urlTarget = target.Replace(".lnk", ".url");
                string urlContent = $$"""
                    [InternetShortcut]
                    URL=ellen://launch
                    IconFile={{targetLogoPath}}
                    IconIndex=0
                    """;
                File.WriteAllText(urlTarget, urlContent);
                return true;
            }
        }
        catch
        {
            return false;
        }
    }
}
