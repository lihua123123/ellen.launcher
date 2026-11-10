using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ellen.Launcher.Test.IncomingFeature;

[TestClass]
public partial class UnUsedResourceStringTest
{
    [TestMethod]
    public void GetAllUnUsedResourceStrings()
    {
        /* 不按固定层数上溯：构建输出布局会变 —— `-p:Platform=x64` 是
           `bin\x64\Debug\net10.0`，默认是 `bin\Debug\net10.0-windows…\win-x64`，层数差一层，
           数错就 DirectoryNotFoundException（不是产品回归，是跑法差异）。
           改成按标志文件定位，两种跑法都能找到 `src\Ellen.Launcher`。 */
        string? path = null;
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, @"Ellen.Launcher\Resource\Localization\SH.resx")))
            {
                path = dir.FullName;
                break;
            }
        }

        if (path is null)
        {
            throw new DirectoryNotFoundException(
                $"找不到 Ellen.Launcher\\Resource\\Localization\\SH.resx 所在目录（起点 {AppContext.BaseDirectory}）");
        }

        string resxPath = Path.Combine(path, @"Ellen.Launcher\Resource\Localization\SH.resx");
        MatchCollection matches = DataRegex.Matches(File.ReadAllText(resxPath));
        HashSet<string> strings = matches.Select(m => m.Groups[1].Value).ToHashSet();


        foreach (ref string file in Directory.GetFiles(path,"*", SearchOption.AllDirectories).AsSpan())
        {
            if (Path.GetExtension(file) is not (".cs" or ".xaml"))
            {
                continue;
            }

            if (file.Contains("Ellen.Launcher.Test", StringComparison.OrdinalIgnoreCase) ||
                file.Contains("Ellen.Launcher\\obj", StringComparison.OrdinalIgnoreCase) ||
                file.Contains("Ellen.Launcher\\bin", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string content = File.ReadAllText(file);
            foreach (string str in strings)
            {
                if (content.Contains(str, StringComparison.Ordinal))
                {
                    strings.Remove(str);
                }
            }
        }

        foreach (string str in strings)
        {
            if (str.StartsWith("Server", StringComparison.Ordinal))
            {
                continue;
            }

            Console.WriteLine(str);
        }
    }

    [GeneratedRegex("""
        data name="(.*?)" xml:space="preserve"
        """)]
    private partial Regex DataRegex { get; }
}