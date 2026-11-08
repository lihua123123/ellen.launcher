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
    private readonly IWikiMetadataSyncService wikiMetadataSyncService;

    [GeneratedConstructor]
    public partial MetadataOptions(IServiceProvider serviceProvider);

    /// <summary>
    /// 当前语言的可读元数据目录。优先使用从自建数据站同步下来的快照（可后台更新、无需仓库授权），
    /// 其次 git 仓库，仓库尚未就绪时退回随安装包内置的快照，最后才指向待初始化的仓库目录。
    /// </summary>
    [field: MaybeNull]
    public string LocalizedDataFolder
    {
        get => field ??= ResolveLocalizedDataFolder();
    }

    /// <summary>
    /// 当前语言是否已有可直接读取的元数据（同步快照 / 仓库 / 内置快照）。
    /// 用于启动快路径：命中时无需等待任何网络操作。
    /// </summary>
    public bool HasUsableLocalizedData
    {
        get => wikiMetadataSyncService.HasSyncedData
            || File.Exists(Path.Combine(RepositoryLocalizedDataFolder, MetadataFileName))
            || File.Exists(Path.Combine(BundledLocalizedDataFolder, MetadataFileName));
    }

    public string GetLocalizedLocalPath(string fileNameWithExtension)
    {
        /* 同步快照未必包含全部数据集（例如只落了角色与武器），按「单文件 / 目录」逐项回退：
           同步目录 → git 仓库 → 内置快照。避免整页只因缺一个 json 就抛 FileNotFound。 */
        foreach (string folder in EnumerateDataFolders())
        {
            string candidate = Path.Combine(folder, fileNameWithExtension);
            if (File.Exists(candidate) || Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(LocalizedDataFolder, fileNameWithExtension);
    }

    private IEnumerable<string> EnumerateDataFolders()
    {
        if (wikiMetadataSyncService.HasSyncedData)
        {
            yield return wikiMetadataSyncService.SyncedLocalizedDataFolder;
        }

        if (File.Exists(Path.Combine(RepositoryLocalizedDataFolder, MetadataFileName)))
        {
            yield return RepositoryLocalizedDataFolder;
        }

        if (File.Exists(Path.Combine(BundledLocalizedDataFolder, MetadataFileName)))
        {
            yield return BundledLocalizedDataFolder;
        }
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
        /* 1. 自建数据站同步快照（更新最快，不依赖任何仓库授权） */
        if (wikiMetadataSyncService.HasSyncedData)
        {
            return wikiMetadataSyncService.SyncedLocalizedDataFolder;
        }

        /* 2. 旧的 Snap.Metadata git 仓库：保留为兜底，不再主动更新 */
        string repositoryFolder = RepositoryLocalizedDataFolder;
        if (File.Exists(Path.Combine(repositoryFolder, MetadataFileName)))
        {
            return repositoryFolder;
        }

        /* 3. 随安装包内置的快照 */
        string bundledFolder = BundledLocalizedDataFolder;
        if (File.Exists(Path.Combine(bundledFolder, MetadataFileName)))
        {
            return bundledFolder;
        }

        Directory.CreateDirectory(repositoryFolder);
        return repositoryFolder;
    }
}