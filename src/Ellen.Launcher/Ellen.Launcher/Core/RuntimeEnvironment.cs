// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Win32;
using System.Diagnostics;
using Windows.ApplicationModel;

namespace Ellen.Launcher.Core;

public static class RuntimeEnvironment
{
    public static bool IsPackaged { get; }
    public static bool IsUnpackaged => !IsPackaged;

    static RuntimeEnvironment()
    {
        try
        {
            _ = Package.Current;
            IsPackaged = true;
        }
        catch
        {
            IsPackaged = false;
        }
    }

    public static void TryRegisterProtocol()
    {
        if (IsPackaged)
        {
            // In packaged mode, protocol is registered via Package.appxmanifest
            return;
        }

        try
        {
            string? executablePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(executablePath))
            {
                return;
            }

            using (RegistryKey? key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\ellen"))
            {
                if (key is null)
                {
                    return;
                }

                key.SetValue(string.Empty, "URL:ellen");
                key.SetValue("URL Protocol", string.Empty);

                using (RegistryKey? commandKey = key.CreateSubKey(@"shell\open\command"))
                {
                    commandKey?.SetValue(string.Empty, $"\"{executablePath}\" \"%1\"");
                }
            }
        }
        catch
        {
            // Best effort registration
        }
    }
}
