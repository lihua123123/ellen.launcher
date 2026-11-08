// P5 启动探针：设置 ELLEN_STARTUP_PROBE=<json 路径> 后，把「进程启动 → Main → 窗口创建 → 主视图 Loaded → 首帧」
// 的时间点写到文件。只在显式开启时工作，不改变正常启动行为，也不上报任何用户数据。
// 帧级滚动/筛选仍由 UI 探针负责，这里只回答 M1/M7/M8（冷/热启动到首帧）。

using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Ellen.Launcher.Service.EnemyArchive;

public static class StartupProbe
{
    public const string EnvironmentVariable = "ELLEN_STARTUP_PROBE";

    private static readonly Stopwatch Watch = new();
    private static readonly List<MarkPoint> Marks = [];
    private static readonly object WriteGate = new();
    private static string? outputPath;
    private static bool frameHooked;
    private static bool written;
    private static string windowTitle = string.Empty;
    private static double processStartToMainMs;

    // 首帧时刻单独固化：后续 mark 触发的重写不得把「首帧」改成「最后一次 mark」的时刻。
    private static double? firstRenderWatchMs;

    public static bool Enabled => outputPath is not null;

    public static void Start()
    {
        outputPath = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = null;
            return;
        }

        using Process process = Process.GetCurrentProcess();
        processStartToMainMs = Math.Max(0, (DateTime.Now - process.StartTime).TotalMilliseconds);
        Watch.Restart();
        Marks.Clear();
        Mark("main-entry");
    }

    public static void Mark(string name)
    {
        if (!Enabled)
        {
            return;
        }

        lock (WriteGate)
        {
            Marks.Add(new MarkPoint { Name = name, Ms = Math.Round(Watch.Elapsed.TotalMilliseconds, 3) });

            // 首帧写入后仍有 mark（如 WikiUiBench 自动导航后的 enemy-web-loaded / source-read）：
            // 同步重写，保证聚合脚本拿到完整 marks，而不是只有首帧那一份。
            if (written)
            {
                Write();
            }
        }
    }

    /// <summary>主视图 Loaded 后挂首帧回调；只取第一次 Rendering，写完即摘钩子。</summary>
    public static void AttachFirstFrame(Window window)
    {
        if (!Enabled || frameHooked)
        {
            return;
        }

        frameHooked = true;
        windowTitle = window.Title; // UI 线程；Write 不再直接访问 Window（可能被后台 mark 调用）
        void OnRendering(object? sender, object e)
        {
            CompositionTarget.Rendering -= OnRendering;
            firstRenderWatchMs = Watch.Elapsed.TotalMilliseconds;
            Mark("first-render");
            lock (WriteGate)
            {
                written = true;
                Write();
            }
        }

        CompositionTarget.Rendering += OnRendering;
    }

    private static void Write()
    {
        try
        {
            if (outputPath is null)
            {
                return;
            }

            using Process process = Process.GetCurrentProcess();
            var payload = new
            {
                schema = "ellen-startup-probe-v1",
                generatedAt = DateTimeOffset.Now.ToString("O"),
                processStartToMainMs = Math.Round(processStartToMainMs, 3),
                processStartToFirstRenderMs = Math.Round(processStartToMainMs + (firstRenderWatchMs ?? Watch.Elapsed.TotalMilliseconds), 3),
                workingSetBytes = process.WorkingSet64,
                windowTitle,
                marks = Marks.Select(m => new { m.Name, m.Ms }),
            };

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
            File.WriteAllText(outputPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class MarkPoint
    {
        public string Name { get; init; } = string.Empty;
        public double Ms { get; init; }
    }
}
