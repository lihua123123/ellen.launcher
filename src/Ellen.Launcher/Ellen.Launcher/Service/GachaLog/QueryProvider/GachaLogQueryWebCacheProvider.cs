// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.IO;
using Ellen.Launcher.Factory.IO;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Service.Game;
using Microsoft.Win32;
using System.Buffers;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Ellen.Launcher.Service.GachaLog.QueryProvider;

[Service(ServiceLifetime.Transient, typeof(IGachaLogQueryProvider), Key = RefreshOptionKind.WebCache)]
public sealed partial class GachaLogQueryWebCacheProvider : IGachaLogQueryProvider
{
    private readonly IMemoryStreamFactory memoryStreamFactory;
    private readonly IGameService gameService;
    private readonly CultureOptions cultureOptions;
    private readonly LaunchOptions launchOptions;

    [GeneratedConstructor]
    public partial GachaLogQueryWebCacheProvider(IServiceProvider serviceProvider);

    [GeneratedRegex("^[1-9]+?\\.[0-9]+?\\.[0-9]+?\\.[0-9]+?$")]
    private static partial Regex VersionRegex { get; }

    public static string GetCacheFile(string path)
    {
        string exeName = Path.GetFileName(path);
        string dataFolder = exeName is GameConstants.GenshinImpactFileName
            ? GameConstants.GenshinImpactData
            : GameConstants.YuanShenData;

        string? directory = Path.GetDirectoryName(path);
        ArgumentNullException.ThrowIfNull(directory);
        DirectoryInfo webCacheFolder = new(Path.Combine(directory, dataFolder, "webCaches"));
        if (webCacheFolder.Exists)
        {
            DirectoryInfo? latestVersionCacheFolder = webCacheFolder
                .EnumerateDirectories()
                .Where(dir => VersionRegex.IsMatch(dir.Name))
                .MaxBy(dir => new Version(dir.Name));

            latestVersionCacheFolder ??= webCacheFolder;
            return Path.Combine(latestVersionCacheFolder.FullName, @"Cache\Cache_Data\data_2");
        }

        return string.Empty;
    }

    public async ValueTask<ValueResult<bool, GachaLogQuery>> GetQueryAsync(GachaGameType game)
    {
        if (game is GachaGameType.ZZZ)
        {
            return await GetZZZQueryAsync().ConfigureAwait(false);
        }

        (bool isOk, string path) = await gameService.GetGamePathAsync().ConfigureAwait(false);
        if (!isOk || string.IsNullOrEmpty(path))
        {
            return new(false, GachaLogQuery.Invalid(SH.ServiceGachaLogUrlProviderCachePathInvalid));
        }

        string cacheFile = GetCacheFile(path);
        if (!File.Exists(cacheFile))
        {
            return new(false, GachaLogQuery.Invalid(SH.FormatServiceGachaLogUrlProviderCachePathNotFound(cacheFile)));
        }

        return await MatchQueryAsync(
            cacheFile,
            cacheFile.Contains(GameConstants.GenshinImpactData, StringComparison.OrdinalIgnoreCase)
                ? "https://gs.hoyoverse.com/genshin/event/e20190909gacha"
                : "https://webstatic.mihoyo.com/hk4e/event/e20190909gacha",
            string.Empty,
            GachaGameType.Genshin).ConfigureAwait(false);
    }

    [SupportedOSPlatform("windows")]
    private async ValueTask<ValueResult<bool, GachaLogQuery>> GetZZZQueryAsync()
    {
        // 优先使用首页设置的绝区零游戏路径（适配 Starward 等非官方启动器安装位置）
        string zzzExePath = launchOptions.ZZZGamePath.Value;
        string cacheFile = string.IsNullOrEmpty(zzzExePath) ? GetZZZCacheFileFromRegistry() : GetZZZCacheFile(zzzExePath);

        if (string.IsNullOrEmpty(cacheFile) || !File.Exists(cacheFile))
        {
            return new(false, string.IsNullOrEmpty(zzzExePath)
                ? GachaLogQuery.Invalid(SH.ServiceGachaLogUrlProviderZZZCachePathNotFound)
                : GachaLogQuery.Invalid(SH.FormatServiceGachaLogUrlProviderCachePathNotFound(cacheFile)));
        }

        // 绝区零：扫缓存里「带 authkey 的 API URL」。
        // 实测游戏实际请求的主机是 public-operation-common（非 public-operation-nap），
        // 页面事件 ID 已变为 e20230424gacha-v2。API URL 必然带 authkey，不会命中静态资源。
        // 参考 zzz-signal-search-export 与 HoYo.Gacha：两者都只取含 authkey 的 URL。
        return await MatchQueryAsync(
            cacheFile,
            "https://public-operation-common",
            string.Empty,
            GachaGameType.ZZZ).ConfigureAwait(false);
    }

    private static string GetZZZCacheFile(string zzzExePath)
    {
        string? directory = Path.GetDirectoryName(zzzExePath);
        if (string.IsNullOrEmpty(directory))
        {
            return string.Empty;
        }

        DirectoryInfo webCacheFolder = new(Path.Combine(directory, GameConstants.ZenlessZoneZeroData, "webCaches"));
        return ResolveData2(webCacheFolder);
    }

    [SupportedOSPlatform("windows")]
    private static string GetZZZCacheFileFromRegistry()
    {
        string installPath = GetZZZInstallPath();
        if (string.IsNullOrEmpty(installPath))
        {
            return string.Empty;
        }

        DirectoryInfo webCacheFolder = new(Path.Combine(installPath, GameConstants.ZenlessZoneZeroData, "webCaches"));
        return ResolveData2(webCacheFolder);
    }

    private static string ResolveData2(DirectoryInfo webCacheFolder)
    {
        if (!webCacheFolder.Exists)
        {
            return string.Empty;
        }

        DirectoryInfo? latestVersionCacheFolder = webCacheFolder
            .EnumerateDirectories()
            .Where(dir => VersionRegex.IsMatch(dir.Name))
            .MaxBy(dir => new Version(dir.Name));

        latestVersionCacheFolder ??= webCacheFolder;
        return Path.Combine(latestVersionCacheFolder.FullName, @"Cache\Cache_Data\data_2");
    }

    /// <summary>
    /// 从绝区零官方启动器的卸载表项中获取安装路径
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static string GetZZZInstallPath()
    {
        try
        {
            if (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\绝区零", "InstallPath", null) is not string launcherPath)
            {
                return string.Empty;
            }

            string configPath = Path.Combine(launcherPath, "config.ini");
            if (!File.Exists(configPath))
            {
                return string.Empty;
            }

            Match match = Regex.Match(File.ReadAllText(configPath), @"game_install_path=(.+)");
            if (!match.Success)
            {
                return string.Empty;
            }

            string installPath = Path.GetFullPath(match.Groups[1].Value.Trim());
            return Directory.Exists(installPath) ? installPath : string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private async ValueTask<ValueResult<bool, GachaLogQuery>> MatchQueryAsync(string cacheFile, string matchPrefix, string fallbackPrefix, GachaGameType game)
    {
        // Must copy the file to avoid the following exception:
        // System.IO.IOException: The process cannot access the file
        TempFileStream fileStream;
        try
        {
            fileStream = TempFileStream.CopyFrom(cacheFile, FileMode.Open, FileAccess.Read);
        }
        catch (UnauthorizedAccessException)
        {
            return new(false, GachaLogQuery.Invalid(SH.ServiceGachaLogUrlProviderCopyWebCacheUnauthorizedAccess));
        }

        using (fileStream)
        {
            using (MemoryStream memoryStream = await memoryStreamFactory.GetStreamAsync(fileStream).ConfigureAwait(false))
            {
                string? result = Match(memoryStream, Encoding.UTF8.GetBytes(matchPrefix));
                if (string.IsNullOrEmpty(result) && !string.IsNullOrEmpty(fallbackPrefix))
                {
                    result = Match(memoryStream, Encoding.UTF8.GetBytes(fallbackPrefix));
                }

                if (string.IsNullOrEmpty(result))
                {
                    return new(false, GachaLogQuery.Invalid(SH.ServiceGachaLogUrlProviderCacheUrlNotFound));
                }

                NameValueCollection query = HttpUtility.ParseQueryString(result.TrimEnd("#/log"));
                string? queryLanguageCode = query["lang"];

                if (!LocaleNames.LanguageCodeFitsCurrentLocale(queryLanguageCode, cultureOptions.LocaleName))
                {
                    string message = SH.FormatServiceGachaLogUrlProviderUrlLanguageNotMatchCurrentLocale(queryLanguageCode, cultureOptions.LanguageCode);
                    return new(false, GachaLogQuery.Invalid(message));
                }

                return new(true, new(result, game));
            }
        }
    }

    private static string? Match(MemoryStream stream, ReadOnlySpan<byte> match)
    {
        using (IMemoryOwner<byte> memoryOwner = MemoryPool<byte>.Shared.RentExactly((int)stream.Length))
        {
            Span<byte> span = memoryOwner.Memory.Span;
            stream.ReadExactly(span);

            int index = span.LastIndexOf(match);
            if (index < 0)
            {
                return null;
            }

            // 与 Starward 一致：从命中位置读到首个 null 或双引号为止，
            // 避免缓存条目后面紧跟二进制元数据时把垃圾读进来。
            // 返回包含前缀的完整 URL，HttpUtility.ParseQueryString 会自动取 '?' 之后的部分。
            int length = span[index..].IndexOfAny("\0\""u8);
            if (length < 0)
            {
                length = span.Length - index;
            }

            return Encoding.UTF8.GetString(span.Slice(index, length));
        }
    }
}
