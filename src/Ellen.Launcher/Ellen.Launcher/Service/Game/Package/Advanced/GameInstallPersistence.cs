// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.IO.Ini;
using Ellen.Launcher.Service.Game.Configuration;
using Ellen.Launcher.Service.Game.FileSystem;
using Ellen.Launcher.Service.Game.Scheme;
using System.Collections.Immutable;
using System.IO;

namespace Ellen.Launcher.Service.Game.Package.Advanced;

public sealed class GameInstallPersistence
{
    private const string InstallingName = "snap_ellen_installing";
    private readonly IGameFileSystem gameFileSystem;

    private GameInstallPersistence(IGameFileSystem gameFileSystem)
    {
        this.gameFileSystem = gameFileSystem;
    }

    public static bool TryAcquire(IGameFileSystem gameFileSystem, string version, LaunchScheme launchScheme, [NotNullWhen(true)] out GameInstallPersistence? locker)
    {
        string gameDirectory = gameFileSystem.GameDirectory;
        Directory.CreateDirectory(gameDirectory);

        // If the directory is not empty
        if (Directory.EnumerateFileSystemEntries(gameDirectory).Any())
        {
            // we need to make sure config file exists and has our installing mark
            // Otherwise, we should prevent installation from proceeding
            if (!File.Exists(gameFileSystem.GameConfigurationFilePath) || !GameConfiguration.Read(gameFileSystem, InstallingName))
            {
                locker = default;
                return false;
            }

            // If the mark exists, we can proceed
            locker = new(gameFileSystem);
            return true;
        }

        // Directory is empty, create config file with our installing mark
        GameConfiguration.Create(launchScheme, version, gameFileSystem.GameConfigurationFilePath);
        ImmutableArray<IniElement> elements = IniSerializer.DeserializeFromFile(gameFileSystem.GameConfigurationFilePath);
        IniSerializer.SerializeToFile(gameFileSystem.GameConfigurationFilePath, elements.Add(new IniParameter(InstallingName, string.Empty)));

        locker = new(gameFileSystem);
        return true;
    }

    public void Release()
    {
        ImmutableArray<IniElement> elements = IniSerializer.DeserializeFromFile(gameFileSystem.GameConfigurationFilePath);
        IniSerializer.SerializeToFile(gameFileSystem.GameConfigurationFilePath, elements.Remove(elements.Single(e => e is IniParameter { Key: InstallingName })));
    }
}