// UI 层图片「真实下载」计数（仅敌人图鉴图源）。
// 为什么需要它：WikiImageDecodeCounter 只覆盖「解码失败」，而 CachedImage 走的是 ImageCache 的下载管线，
// 下载失败会被 catch 后降级占位图 —— 旧实现里这件事既不进 metrics 也不进 Sentry（Sentry 默认未启用），
// 于是「图片失败率 0%」可能只是「没人量过」。这里把下载尝试拆成 成功 / 失败 两个计数，
// 由 WikiMonsterViewModel 写入 metrics（ImageUiDownloads / ImageUiDownloadFailures）。
// 口径不含 Wiki 服务层的 TryCacheImageAsync（那部分由 image-download-failure 诊断覆盖）。

namespace Ellen.Launcher.Service.EnemyArchive;

public static class WikiImageDownloadCounter
{
    private static int attempts;
    private static int successes;
    private static int failures;

    /// <summary>本进程累计的下载尝试数（只读快照）。</summary>
    public static int Attempts => Volatile.Read(ref attempts);

    /// <summary>本进程累计的下载成功数（落盘且文件存在）。</summary>
    public static int Successes => Volatile.Read(ref successes);

    /// <summary>本进程累计的下载失败数（catch 后仍无文件）。</summary>
    public static int Failures => Volatile.Read(ref failures);

    /// <summary>
    /// 只有 http/https 的图鉴图源才算一次「下载」。
    /// file://（随包 753 / Wiki 缓存本地文件）根本走不到 ImageCache 下载管线；
    /// 万一日后回归把它们送进来，HttpClient 不支持 file 会直接抛异常，
    /// 那就变成「每张随包图都记一次下载失败」——必须在计数前按 scheme 挡住。
    /// </summary>
    public static bool IsDownloadableWikiImage(Uri? uri)
        => uri is not null
            && uri.IsAbsoluteUri
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && WikiImageDecodeCounter.IsWikiImage(uri);

    internal static void ReportAttempt() => Interlocked.Increment(ref attempts);

    internal static void ReportSuccess() => Interlocked.Increment(ref successes);

    internal static void ReportFailure() => Interlocked.Increment(ref failures);
}
