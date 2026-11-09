// P5 UI 性能探针：ELLEN_WIKI_UI_BENCH=<out.json>
// 真实 WinUI 窗口里导航到敌人图鉴，测：进入页面 → 数据就绪 → 首帧 → 列表滚动帧间隔 → 分类切换 → 深链。
// 只在显式开启时运行；失败会写 error 字段后退出，不伪造数字。

using System.Diagnostics;
using System.IO;
using Ellen.Launcher.Service.Navigation;
using Ellen.Launcher.UI.Xaml.View.Page;
using Ellen.Launcher.ViewModel.Wiki;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Ellen.Launcher.Service.EnemyArchive;

public static class WikiUiBench
{
    public const string EnvironmentVariable = "ELLEN_WIKI_UI_BENCH";

    /* 构建配置自证：Debug 与 Release 的帧率/首帧不能混着比。
       报告里没有这个字段，就等于默许「拿 Debug 数字当发版数字」。 */
#if DEBUG
    private const string BuildConfiguration = "debug";
#else
    private const string BuildConfiguration = "release";
#endif

    public static bool TryStart(Window window)
    {
        string? output = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(output))
        {
            return false;
        }

        _ = RunAsync(window, output);
        return true;
    }

    private static async Task RunAsync(Window window, string outputPath)
    {
        Stopwatch watch = Stopwatch.StartNew();
        List<(string Name, double Ms)> marks = [];
        try
        {
            IServiceProvider services = Ioc.Default;
            ITaskContext taskContext = services.GetRequiredService<ITaskContext>();
            INavigationService? navigation = services.GetService(typeof(INavigationService)) as INavigationService;

            await taskContext.SwitchToMainThreadAsync();
            marks.Add(("window-loaded", watch.Elapsed.TotalMilliseconds));

            /* 截图尺寸：默认跟窗口一致（4K 全屏截出来的图缩到聊天里看不清）。
               ELLEN_WIKI_UI_BENCH_SIZE=1440x920 可指定，仅影响取证图，不影响任何指标。 */
            if (Environment.GetEnvironmentVariable("ELLEN_WIKI_UI_BENCH_SIZE") is { Length: > 0 } sizeText)
            {
                string[] parts = sizeText.Split('x', StringSplitOptions.TrimEntries);
                if (parts.Length == 2
                    && int.TryParse(parts[0], out int shotWidth)
                    && int.TryParse(parts[1], out int shotHeight))
                {
                    window.AppWindow.Resize(new Windows.Graphics.SizeInt32(shotWidth, shotHeight));
                }
            }

            if (navigation is null)
            {
                throw new InvalidOperationException("INavigationService 不可用");
            }

            await navigation.NavigateAsync<WikiMonsterPage>(new NavigationExtraData(new DrillInNavigationTransitionInfo()), false);
            marks.Add(("navigated", watch.Elapsed.TotalMilliseconds));

            WikiMonsterPage? page = null;
            WikiMonsterViewModel? vm = null;
            for (int i = 0; i < 240; i++)
            {
                await Task.Delay(50);
                await taskContext.SwitchToMainThreadAsync();
                page = FindPage(window.Content as DependencyObject);
                vm = page?.DataContext as WikiMonsterViewModel;
                if (vm is not null && (vm.WebMonsters is { Count: > 0 } || (vm.IsLegacyMode && vm.Monsters is not null)))
                {
                    break;
                }
            }

            if (page is null || vm is null)
            {
                throw new InvalidOperationException("12s 内未找到 WikiMonsterPage / ViewModel");
            }

            marks.Add(("data-ready", watch.Elapsed.TotalMilliseconds));
            await WaitFramesAsync(taskContext, 2);
            marks.Add(("first-frame", watch.Elapsed.TotalMilliseconds));

            /* 取证用：先切到指定分类再截列表图。
               ELLEN_WIKI_UI_BENCH_CATEGORY=愚人众 → 用来验证「上游没有头像的条目直接留空」。 */
            string? pinnedCategory = Environment.GetEnvironmentVariable("ELLEN_WIKI_UI_BENCH_CATEGORY");
            if (pinnedCategory is { Length: > 0 } categoryName
                && vm.WebCategories is { Count: > 0 } benchCategories)
            {
                var target = benchCategories.FirstOrDefault(c => c.Name == categoryName);
                if (target is not null)
                {
                    vm.WebSelectedCategory = target;
                    await WaitFramesAsync(taskContext, 2);
                }
            }

            /* 取证用截图①：列表页（滚动/切分类之前，保证是 753 条全量列表顶部）。
               ELLEN_WIKI_UI_BENCH_SHOT=<png>；等 600ms 让图标解码完再截。 */
            string? shotListPath = Environment.GetEnvironmentVariable("ELLEN_WIKI_UI_BENCH_SHOT");
            if (!string.IsNullOrWhiteSpace(shotListPath))
            {
                try
                {
                    await Task.Delay(600);
                    await CaptureAsync(window, shotListPath).ConfigureAwait(true);
                    marks.Add(("shot-list", watch.Elapsed.TotalMilliseconds));
                }
                catch (Exception ex)
                {
                    marks.Add(($"shot-list-failed:{ex.GetType().Name}", watch.Elapsed.TotalMilliseconds));
                }
            }

            bool web = vm.IsWebMode && vm.WebMonsters is { Count: > 0 };
            ListView? list = page.FindName("WebMonsterList") as ListView;
            int itemCount = web ? vm.WebMonsters!.Count : vm.Monsters?.Count ?? 0;

            // M4：程序化滚动 30 次，记录 Rendering 帧间隔
            ScrollStats scroll = new();
            if (web && list is not null && itemCount > 0)
            {
                scroll = await MeasureScrollAsync(taskContext, list, itemCount, 30);
            }

            // M5（UI 层）：切换分类，等到列表内容变化 + 2 帧
            double categoryMs = 0;
            int categoryCount = 0;
            if (web && pinnedCategory is null && vm.WebCategories is { Count: > 1 } categories)
            {
                Stopwatch cw = Stopwatch.StartNew();
                vm.WebSelectedCategory = categories[Math.Min(5, categories.Count - 1)];
                await WaitFramesAsync(taskContext, 2);
                categoryMs = cw.Elapsed.TotalMilliseconds;
                categoryCount = vm.WebMonsters?.Count ?? 0;
            }

            // M6（UI 层）：切换选中项（顺带把等级滑块重算一次；深链调试框已移除）
            double deepLinkMs = 0;
            string deepLinkResult = string.Empty;
            if (web && vm.WebMonsters is { Count: > 1 } benchMonsters)
            {
                Stopwatch dw = Stopwatch.StartNew();
                vm.WebCurrentItem = benchMonsters[Math.Min(1, benchMonsters.Count - 1)];
                await WaitFramesAsync(taskContext, 2);
                deepLinkMs = dw.Elapsed.TotalMilliseconds;
                deepLinkResult = vm.WebBaseValueInfo?.CurrentLevelFormatted ?? string.Empty;
            }

            /* 取证用截图②-早：切完选中项立刻截（约 60ms），用来复现「点开敌人闪一下」。
               ELLEN_WIKI_UI_BENCH_SHOT_DETAIL_EARLY=<png>；和 1.6s 后那张对比就知道闪的是哪块。 */
            string? shotDetailEarlyPath = Environment.GetEnvironmentVariable("ELLEN_WIKI_UI_BENCH_SHOT_DETAIL_EARLY");
            if (!string.IsNullOrWhiteSpace(shotDetailEarlyPath))
            {
                try
                {
                    await Task.Delay(60);
                    await CaptureAsync(window, shotDetailEarlyPath).ConfigureAwait(true);
                    marks.Add(("shot-detail-early", watch.Elapsed.TotalMilliseconds));
                }
                catch (Exception ex)
                {
                    marks.Add(($"shot-detail-early-failed:{ex.GetType().Name}", watch.Elapsed.TotalMilliseconds));
                }
            }

            /* 取证用截图②：详情页（深链之后）。ELLEN_WIKI_UI_BENCH_SHOT_DETAIL=<png>。
               离线且未缓存时这里应是随包 256px 预览 + 一行「离线：显示随包 256px 预览」说明。 */
            string? shotDetailPath = Environment.GetEnvironmentVariable("ELLEN_WIKI_UI_BENCH_SHOT_DETAIL");
            if (!string.IsNullOrWhiteSpace(shotDetailPath))
            {
                try
                {
                    // 大图（reference*.png）解码比列表小图慢得多，400ms 常常只截到「有版位没画面」。
                    await Task.Delay(1600);
                    await CaptureAsync(window, shotDetailPath).ConfigureAwait(true);
                    marks.Add(("shot-detail", watch.Elapsed.TotalMilliseconds));
                }
                catch (Exception ex)
                {
                    marks.Add(($"shot-detail-failed:{ex.GetType().Name}", watch.Elapsed.TotalMilliseconds));
                }
            }

            /* 取证用截图③：详情页滚到底（一图流 + 技能栏）。
               ELLEN_WIKI_UI_BENCH_SHOT_DETAIL_BOTTOM=<png>；用来证明大图与技能图标真的渲染出来了，
               顶部那张看不到这两块。 */
            string? shotDetailBottomPath = Environment.GetEnvironmentVariable("ELLEN_WIKI_UI_BENCH_SHOT_DETAIL_BOTTOM");
            if (!string.IsNullOrWhiteSpace(shotDetailBottomPath))
            {
                try
                {
                    if (page.FindName("WebDetailScroll") is ScrollViewer detailScroll)
                    {
                        detailScroll.UpdateLayout();
                        detailScroll.ChangeView(null, detailScroll.ScrollableHeight, null, disableAnimation: true);
                    }

                    await Task.Delay(800);
                    await CaptureAsync(window, shotDetailBottomPath).ConfigureAwait(true);
                    marks.Add(("shot-detail-bottom", watch.Elapsed.TotalMilliseconds));
                }
                catch (Exception ex)
                {
                    marks.Add(($"shot-detail-bottom-failed:{ex.GetType().Name}", watch.Elapsed.TotalMilliseconds));
                }
            }

            /* 静置观察：ELLEN_WIKI_UI_BENCH_SETTLE_MS=6000
               用来确认「后台同步完成」不会再重建列表 —— 重建会让 ListView 重实现、当前项换实例，
               用户看到的就是点开敌人后闪一下。增量必须是 0；>0 说明防抖判据失效。 */
            int rebuildsBeforeSettle = vm.CatalogRebuilds;
            int constSettleMs = 0;
            if (int.TryParse(Environment.GetEnvironmentVariable("ELLEN_WIKI_UI_BENCH_SETTLE_MS"), out int parsedSettle) && parsedSettle > 0)
            {
                constSettleMs = parsedSettle;
                await Task.Delay(parsedSettle);
                await taskContext.SwitchToMainThreadAsync();
                marks.Add(("settled", watch.Elapsed.TotalMilliseconds));
            }

            int rebuildsAfterSettle = vm.CatalogRebuilds;

            var payload = new
            {
                schema = "wiki-ui-bench-v1",
                generatedAt = DateTimeOffset.Now.ToString("O"),
                build = BuildConfiguration,
                ok = true,
                webMode = vm.IsWebMode,
                itemCount,
                catalogRebuildsBeforeSettle = rebuildsBeforeSettle,
                catalogRebuildsAfterSettle = rebuildsAfterSettle,
                catalogRebuildsDelta = rebuildsAfterSettle - rebuildsBeforeSettle,
                settleMs = constSettleMs,
                // 详情取证：技能图标是否真的解析出 URI（{} 绑定错了不会报错，只能靠这个字段核对）
                currentItem = web ? vm.WebCurrentItem?.Name : null,
                catalogRebuilds = vm.CatalogRebuilds,
                skillCount = web ? vm.WebCurrentItem?.Skills.Count ?? 0 : 0,
                skillIconCount = web ? vm.WebCurrentItem?.Skills.Count(static s => s.HasIcon) ?? 0 : 0,
                skillIconSample = web ? vm.WebCurrentItem?.Skills.FirstOrDefault(static s => s.HasIcon)?.IconUri : null,
                artUri = web ? vm.WebCurrentItem?.ArtUri : null,
                marks = marks.Select(m => new { m.Name, ms = Math.Round(m.Ms, 3) }),
                // 门禁可复核字段：fpsSamples / droppedFrames / durationSeconds（仅 {ok:true} 不认）。
                fpsSamples = scroll.FpsSamples,
                droppedFrames = scroll.DroppedFrames,
                durationSeconds = Math.Round(scroll.DurationMs / 1000.0, 2),
                M4_scroll = new
                {
                    frames = scroll.Frames,
                    avgFrameMs = Math.Round(scroll.AvgMs, 3),
                    p95FrameMs = Math.Round(scroll.P95Ms, 3),
                    maxFrameMs = Math.Round(scroll.MaxMs, 3),
                    realizedContainers = scroll.Realized,
                    scrolls = scroll.Scrolls,
                    droppedFrameThresholdMs = 25.0,
                    note = scroll.Frames == 0 ? "未采到帧（列表未实例化或滚动无重绘）" : "CompositionTarget.Rendering 帧间隔",
                },
                M5_categorySwitch = new { ms = Math.Round(categoryMs, 3), items = categoryCount },
                M6_selectItem = new { ms = Math.Round(deepLinkMs, 3), level = deepLinkResult },
                totalMs = Math.Round(watch.Elapsed.TotalMilliseconds, 1),
            };

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
            File.WriteAllText(outputPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
                File.WriteAllText(outputPath, JsonSerializer.Serialize(new
                {
                    schema = "wiki-ui-bench-v1",
                    generatedAt = DateTimeOffset.Now.ToString("O"),
                    build = BuildConfiguration,
                    ok = false,
                    error = ex.ToString(),
                    marks = marks.Select(m => new { m.Name, ms = Math.Round(m.Ms, 3) }),
                    totalMs = Math.Round(watch.Elapsed.TotalMilliseconds, 1),
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (IOException)
            {
            }
        }
        finally
        {
            Environment.Exit(0);
        }
    }

    /// <summary>把窗口内容渲染成 PNG（WinRT RenderTargetBitmap + BitmapEncoder，进程内完成）。</summary>
    private static async Task CaptureAsync(Window window, string path)
    {
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap bitmap = new();
        await bitmap.RenderAsync((UIElement)window.Content);
        Windows.Storage.Streams.IBuffer buffer = await bitmap.GetPixelsAsync();
        byte[] pixels;
        Windows.Security.Cryptography.CryptographicBuffer.CopyToByteArray(buffer, out pixels!);

        Windows.Storage.StorageFolder folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(full)!);
        Windows.Storage.StorageFile file = await folder.CreateFileAsync(Path.GetFileName(full), Windows.Storage.CreationCollisionOption.ReplaceExisting);
        using Windows.Storage.Streams.IRandomAccessStream stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
        Windows.Graphics.Imaging.BitmapEncoder encoder =
            await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(
            Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
            /* 用 Ignore 而不是 Premultiplied：XAML 根没有不透明底色，预乘会让整页发灰、图片看着像占位图，
               截图就无法作为「缩略图真的渲染出来」的证据。 */
            Windows.Graphics.Imaging.BitmapAlphaMode.Ignore,
            (uint)bitmap.PixelWidth,
            (uint)bitmap.PixelHeight,
            96,
            96,
            pixels);
        await encoder.FlushAsync();
    }

    private static async Task<ScrollStats> MeasureScrollAsync(ITaskContext taskContext, ListView list, int itemCount, int scrolls)
    {
        List<double> deltas = [];
        double last = -1;
        void OnRendering(object? sender, object e)
        {
            double now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency * 1000;
            if (last >= 0)
            {
                deltas.Add(now - last);
            }

            last = now;
        }

        CompositionTarget.Rendering += OnRendering;
        for (int i = 0; i < scrolls; i++)
        {
            int index = (int)((long)i * itemCount / scrolls);
            list.ScrollIntoView(list.Items[index]);
            await Task.Delay(16);
            await taskContext.SwitchToMainThreadAsync();
        }

        await WaitFramesAsync(taskContext, 2);
        CompositionTarget.Rendering -= OnRendering;

        if (deltas.Count == 0)
        {
            return new ScrollStats { Scrolls = scrolls, Realized = list.ItemsPanelRoot?.Children.Count ?? 0 };
        }

        deltas.Sort();
        int p95Index = Math.Min(deltas.Count - 1, (int)Math.Ceiling(deltas.Count * 0.95) - 1);
        double durationMs = deltas.Sum();
        List<double> fpsSamples = [.. deltas.Where(static d => d > 0).Select(static d => Math.Round(1000.0 / d, 1))];
        int dropped = deltas.Count(static d => d > 25.0);
        return new ScrollStats
        {
            Frames = deltas.Count,
            AvgMs = deltas.Average(),
            P95Ms = deltas[p95Index],
            MaxMs = deltas[^1],
            Realized = list.ItemsPanelRoot?.Children.Count ?? 0,
            Scrolls = scrolls,
            FpsSamples = fpsSamples,
            DroppedFrames = dropped,
            DurationMs = durationMs,
        };
    }

    private static async Task WaitFramesAsync(ITaskContext taskContext, int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            TaskCompletionSource tick = new(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnRendering(object? sender, object e)
            {
                CompositionTarget.Rendering -= OnRendering;
                tick.TrySetResult();
            }

            CompositionTarget.Rendering += OnRendering;
            await Task.WhenAny(tick.Task, Task.Delay(500)).ConfigureAwait(false);
            await taskContext.SwitchToMainThreadAsync();
        }
    }

    private static WikiMonsterPage? FindPage(DependencyObject? root)
    {
        if (root is null)
        {
            return null;
        }

        if (root is WikiMonsterPage page)
        {
            return page;
        }

        if (root is Frame { Content: WikiMonsterPage framePage })
        {
            return framePage;
        }

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            WikiMonsterPage? found = FindPage(VisualTreeHelper.GetChild(root, i));
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private sealed class ScrollStats
    {
        public int Frames { get; init; }
        public double AvgMs { get; init; }
        public double P95Ms { get; init; }
        public double MaxMs { get; init; }
        public int Realized { get; init; }
        public int Scrolls { get; init; }
        public IReadOnlyList<double> FpsSamples { get; init; } = [];
        public int DroppedFrames { get; init; }
        public double DurationMs { get; init; }
    }
}
