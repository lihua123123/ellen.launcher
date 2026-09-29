// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using System.IO;

namespace Ellen.Launcher.Service.Metadata;

[Service(ServiceLifetime.Singleton)]
public sealed partial class MetadataOptions
{
    private const string MetadataFileName = "Meta.json";
    private const string RepositoryName = "Snap.Metadata";

    private readonly CultureOptions cultureOptions;

    [GeneratedConstructor]
    public partial MetadataOptions(IServiceProvider serviceProvider);

    /// <summary>
    /// 当前语言的可读元数据目录。优先使用 git 仓库（可被后台更新），
    /// 仓库尚未就绪时退回随安装包内置的快照，最后才指向待初始化的仓库目录。
    /// </summary>
    [field: MaybeNull]
    public string LocalizedDataFolder
    {
        get => field ??= ResolveLocalizedDataFolder();
    }

    /// <summary>
    /// 当前语言是否已有可直接读取的元数据（仓库或内置快照）。
    /// 用于启动快路径：命中时无需等待任何网络操作。
    /// </summary>
    public bool HasUsableLocalizedData
    {
        get => File.Exists(Path.Combine(RepositoryLocalizedDataFolder, MetadataFileName))
            || File.Exists(Path.Combine(BundledLocalizedDataFolder, MetadataFileName));
    }

    public string GetLocalizedLocalPath(string fileNameWithExtension)
    {
        return Path.Combine(LocalizedDataFolder, fileNameWithExtension);
    }

    private string RepositoryLocalizedDataFolder
    {
        get => Path.Combine(EllenRuntime.GetDataRepositoryDirectory(), RepositoryName, "Genshin", cultureOptions.LocaleName);
    }

    private string BundledLocalizedDataFolder
    {
        get => InstalledLocation.GetAbsolutePath(Path.Combine("Resource", "Metadata", "Genshin", cultureOptions.LocaleName));
    }

    private string ResolveLocalizedDataFolder()
    {
        string repositoryFolder = RepositoryLocalizedDataFolder;
        if (File.Exists(Path.Combine(repositoryFolder, MetadataFileName)))
        {
            return repositoryFolder;
        }

        string bundledFolder = BundledLocalizedDataFolder;
        if (File.Exists(Path.Combine(bundledFolder, MetadataFileName)))
        {
            return bundledFolder;
        }

        Directory.CreateDirectory(repositoryFolder);
        return repositoryFolder;
    }
}