// UI 层图片解码/加载失败计数（仅敌人图鉴来源），供灰度指标「图片解析失败率」分子使用。
// 只统计 ellen.wiki 远程图与 Wiki 缓存本地文件，避免把全应用其它图片的失败混进灰度指标。
// 占位图（ms-appx:///Assets/Blank.png）不计：它本身是降级结果，已由解析层 image-fallback 计入。

namespace Ellen.Launcher.Service.EnemyArchive;

public static class WikiImageDecodeCounter
{
    private static int count;

    /// <summary>本进程累计的解码失败数（只读快照）。</summary>
    public static int Count => Volatile.Read(ref count);

    public static void Report() => Interlocked.Increment(ref count);

    /// <summary>判断该 URI 是否属于敌人图鉴图源（远程 ellen.wiki 或 Wiki 缓存本地文件）。</summary>
    public static bool IsWikiImage(Uri? uri)
    {
        if (uri is null || !uri.IsAbsoluteUri)
        {
            return false;
        }

        string value = uri.AbsoluteUri;
        return value.Contains("/Cache/Wiki/", StringComparison.OrdinalIgnoreCase)
            || value.Contains("ellen.wiki", StringComparison.OrdinalIgnoreCase)
            /* P6 随包图片：Web753/assets/orig|thumbs/…。不纳入的话，随包图解码失败会静默，
               「失败 0」就成了没人验证过的 0。 */
            || value.Contains("/Web753/assets/", StringComparison.OrdinalIgnoreCase);
    }
}
