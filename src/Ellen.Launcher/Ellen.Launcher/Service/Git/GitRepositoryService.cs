// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Common;
using LibGit2Sharp;
using Ellen.Launcher.Core;
using Ellen.Launcher.Core.IO;
using Ellen.Launcher.Core.IO.Http.Proxy;
using Ellen.Launcher.Core.Logging;
using Ellen.Launcher.Core.Setting;
using Ellen.Launcher.Service.BackgroundActivity;
using Ellen.Launcher.Web.Upstream;
using Ellen.Launcher.Web.Upstream.Response;
using Ellen.Launcher.Service.FileUnlocker;
using Ellen.Launcher.Web.Response;
using System.Collections.Immutable;
using System.IO;

namespace Ellen.Launcher.Service.Git;

[Service(ServiceLifetime.Singleton, typeof(IGitRepositoryService))]
public sealed partial class GitRepositoryService : IGitRepositoryService
{
    private const string RepositoryUrlSettingKey = "Ellen::Metadata::RepositoryUrl";
    private const string DefaultMetadataRepositoryUrl = "https://github.com/DGP-Studio/Snap.Metadata.git";

    private readonly AsyncKeyedLock<string> repoLock = new();
    private readonly BackgroundActivityOptions backgroundActivityOptions;
    private readonly IFileUnlockerService fileUnlockerService;
    private readonly IServiceProvider serviceProvider;
    private readonly ITaskContext taskContext;
    private readonly ILogger<GitRepositoryService> logger;

    [GeneratedConstructor]
    public partial GitRepositoryService(IServiceProvider serviceProvider);

    static GitRepositoryService()
    {
        GlobalSettings.SetConfigSearchPaths(ConfigurationLevel.ProgramData, string.Empty);
        GlobalSettings.SetConfigSearchPaths(ConfigurationLevel.Global, string.Empty);
        GlobalSettings.SetConfigSearchPaths(ConfigurationLevel.System, string.Empty);
        GlobalSettings.SetConfigSearchPaths(ConfigurationLevel.Xdg, string.Empty);
        GlobalSettings.SetOwnerValidation(false);
    }

    private static GitRepository ResolveRepositoryInfo(string name)
    {
        string url = LocalSetting.Get(RepositoryUrlSettingKey, DefaultMetadataRepositoryUrl);
        return new()
        {
            Name = name,
            WebUrl = new Uri(url),
            HttpsUrl = new Uri(url),
            Type = GitRepositoryType.Public,
        };
    }

    private static string GetRepositoryDirectory(string name)
    {
        return Path.GetFullPath(Path.Combine(EllenRuntime.GetDataRepositoryDirectory(), name));
    }

    public void EnsureRepositoryInBackground(string name)
    {
        Task.Run(async () =>
        {
            try
            {
                // The local repository is already usable: report the initialization activity as
                // completed right away so the startup UI can finish, then refresh it later.
                BackgroundActivity.BackgroundActivity activity = GetActivityByName(name);
                await activity.NotifyAsync(taskContext).ConfigureAwait(false);
                await activity.UpdateAsync(taskContext, SH.ServiceGitRepositoryOperationCompleted, true, false, false, false).ConfigureAwait(false);

                // Never start a first-time clone in the background: that would surprise the user with a
                // large download and a network call that may fail. Only keep an existing copy fresh.
                if (!Repository.IsValid(GetRepositoryDirectory(name)))
                {
                    return;
                }

                if (!ShouldRefresh(name))
                {
                    return;
                }

                await EnsureRepositoryAsync(name).ConfigureAwait(false);
                LocalSetting.Set($"Ellen::Git::Repository::LastRefresh::{name}", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
            catch (Exception ex)
            {
                // A background refresh must never affect the running application.
                logger.LogWarning(ex, "Background repository refresh failed for {Name}", name);
            }
        });
    }

    private static bool ShouldRefresh(string name)
    {
        const long RefreshIntervalSeconds = 12 * 60 * 60;
        long lastRefresh = LocalSetting.Get($"Ellen::Git::Repository::LastRefresh::{name}", 0L);
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds() - lastRefresh >= RefreshIntervalSeconds;
    }

    public async ValueTask<ValueResult<bool, ValueDirectory>> EnsureRepositoryAsync(string name)
    {
        if (LocalSetting.Get("Ellen::Git::Repository::Override", false))
        {
            return new(true, Path.GetFullPath(Path.Combine(EllenRuntime.GetDataRepositoryDirectory(), name)));
        }

        using (await repoLock.LockAsync(name).ConfigureAwait(false))
        {
            // The repository location is configured locally instead of being looked up from the
            // upstream Hutao infrastructure API, which no longer applies to this fork.
            // Set "Ellen::Metadata::RepositoryUrl" to point at your own mirror.
            ImmutableArray<GitRepository> infos = [ResolveRepositoryInfo(name)];

            string directory = Path.GetFullPath(Path.Combine(EllenRuntime.GetDataRepositoryDirectory(), name));

            BackgroundActivity.BackgroundActivity activity = GetActivityByName(name);

            if (activity == backgroundActivityOptions.FullTrustInitialization)
            {
                // Unlock files in the target directory before git operations to prevent
                // file-in-use errors during delete/overwrite.
                // https://github.com/SnapHutaoRemasteringProject/Snap.Hutao.FileUnlocker
                // await fileUnlockerService.UnlockAsync(directory).ConfigureAwait(false);
            }

            bool failed = false;
            bool succeeded = false;
            List<Exception> exceptions = [];
            try
            {
                await activity.NotifyAsync(taskContext).ConfigureAwait(false);
                await activity.UpdateAsync(taskContext, SH.ServiceBackgroundActivityDefaultDescription, false, false, false, false).ConfigureAwait(false);

                foreach (GitRepository info in infos)
                {
                    try
                    {
                        try
                        {
                            ValueResult<bool, ValueDirectory> result = EnsureRepository(activity, directory, info, false);
                            succeeded = true;
                            return result;
                        }
                        catch (Exception first)
                        {
                            exceptions.Add(first);
                            ValueResult<bool, ValueDirectory> result = EnsureRepository(activity, directory, info, true);
                            succeeded = true;
                            return result;
                        }
                    }
                    catch (Exception second)
                    {
                        exceptions.Add(second);
                    }
                }
            }
            catch (Exception)
            {
                failed = true;
                throw;
            }
            finally
            {
                if (!failed && succeeded)
                {
                    await activity.NotifyAsync(taskContext).ConfigureAwait(false);
                    await activity.UpdateAsync(taskContext, SH.ServiceGitRepositoryOperationCompleted, true, false, false, false).ConfigureAwait(false);
                }
            }

            await activity.NotifyAsync(taskContext).ConfigureAwait(false);
            await activity.UpdateAsync(taskContext, SH.ServiceGitRepositoryOperationFailed, false, true, false, false).ConfigureAwait(false);
            throw new GitRepositoryException(SH.ServiceGitRepositoryOperationFailed, exceptions);
        }
    }

    private ValueResult<bool, ValueDirectory> EnsureRepository(BackgroundActivity.BackgroundActivity activity, string directory, GitRepository info, bool forceInvalid)
    {
        FetchOptions fetchOptions = new()
        {
            Depth = 1,
            Prune = true,
            TagFetchMode = TagFetchMode.None,
            ProxyOptions =
            {
                ProxyType = ProxyType.Auto,
                Url = HttpProxyUsingSystemProxy.Instance.CurrentProxyUri,
            },
            CredentialsProvider = (url, user, types) => string.IsNullOrEmpty(info.Token)
                ? default
                : new UsernamePasswordCredentials
                {
                    Username = info.Username,
                    Password = info.Token,
                },
            OnProgress = output =>
            {
                int idx = output.AsSpan().IndexOfAny("\r\n");
                activity.Update(taskContext, idx > 0 ? output.Substring(0, idx) : output, false, false, false, false);
                return true;
            },
            OnTransferProgress = progress =>
            {
                double progressValue = progress.TotalObjects == 0 ? 0 : (double)progress.ReceivedObjects / progress.TotalObjects;
                activity.Update(taskContext, $"{progress.ReceivedObjects}/{progress.TotalObjects}, {Converters.ToFileSizeString(progress.ReceivedBytes)}", false, false, true, false, progressValue);
                return true;
            },
            CertificateCheck = static (cert, valid, host) => true,
        };

        if (forceInvalid || !Repository.IsValid(directory))
        {
            if (Directory.Exists(directory))
            {
                Directory.SetReadOnly(directory, false);
                Directory.Delete(directory, true);
            }

            Repository.AdvancedClone(info.HttpsUrl.OriginalString, directory, new(fetchOptions)
            {
                Checkout = true,
            });
        }
        else
        {
            // We need to ensure local repo is up to date
            using (Repository repo = new(directory))
            {
                Configuration config = repo.Config;
                config.Set("core.longpaths", true);
                config.Set("safe.directory", true);
                if (string.IsNullOrEmpty(fetchOptions.ProxyOptions.Url))
                {
                    config.Unset("http.proxy");
                    config.Unset("https.proxy");
                }
                else
                {
                    config.Set("http.proxy", fetchOptions.ProxyOptions.Url);
                    config.Set("https.proxy", fetchOptions.ProxyOptions.Url);
                }

                repo.Network.Remotes.Update("origin", remote => remote.Url = info.HttpsUrl.OriginalString);
                repo.RemoveUntrackedFiles();
                fetchOptions.UpdateFetchHead = false;
                Commands.Fetch(repo, repo.Head.RemoteName, Array.Empty<string>(), fetchOptions, default);

                // Manually patch .git/shallow file
                File.WriteAllText(Path.Combine(directory, ".git//shallow"), string.Join("", repo.Branches.Where(static branch => branch.IsRemote).Select(static branch => $"{branch.Tip.Sha}\n")));

                Branch remoteBranch = repo.Branches["origin/main"];
                Branch localBranch = repo.Branches["main"] ?? repo.CreateBranch("main", remoteBranch.Tip);
                repo.Branches.Update(localBranch, b => b.TrackedBranch = remoteBranch.CanonicalName);
                repo.Reset(ResetMode.Hard, remoteBranch.Tip);
                repo.RemoveUntrackedFiles();
            }
        }

        return new(true, directory);
    }

    private BackgroundActivity.BackgroundActivity GetActivityByName(string name)
    {
        return name switch
        {
            "Snap.Metadata" => backgroundActivityOptions.MetadataInitialization,
            "Snap.ContentDelivery" => backgroundActivityOptions.FullTrustInitialization,
            _ => backgroundActivityOptions.Default,
        };
    }
}
