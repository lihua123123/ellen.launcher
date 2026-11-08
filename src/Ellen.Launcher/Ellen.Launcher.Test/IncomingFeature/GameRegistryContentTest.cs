using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;

namespace Ellen.Launcher.Test.IncomingFeature;

[TestClass]
public class GameRegistryContentTest
{
    private static readonly JsonSerializerOptions RegistryContentSerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// 环境依赖测试：需要本机真实安装过原神并写入 HKCU 注册表项。
    /// 已登记为 known-env-failure（见 workspace/wiki-migration/known-env-failures.json），
    /// CI 使用 --filter "TestCategory!=KnownEnvFailure" 豁免，避免全量测试长期带红。
    /// </summary>
    [TestMethod]
    [TestCategory("KnownEnvFailure")]
    [SupportedOSPlatform("windows")]
    public void GetRegistryContent()
    {
        TestGetRegistryContent(@"Software\miHoYo\原神");
        TestGetRegistryContent(@"Software\miHoYo\Genshin Impact");
    }

    [SupportedOSPlatform("windows")]
    private static void TestGetRegistryContent(string subkey)
    {
        using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
        {
            RegistryKey? gameKey = key.OpenSubKey(subkey);
            Assert.IsNotNull(gameKey);

            Dictionary<string, object> data = [];
            foreach (string valueName in gameKey.GetValueNames())
            {
                data[valueName] = gameKey.GetValueKind(valueName) switch
                {
                    RegistryValueKind.DWord => (int)gameKey.GetValue(valueName)!,
                    RegistryValueKind.Binary => GetStringOrObject((byte[])gameKey.GetValue(valueName)!),
                    RegistryValueKind.String => (string)gameKey.GetValue(valueName)!,
                    _ => throw new ArgumentException($"Unsupported type: {gameKey.GetValueKind(valueName)}"),
                };
            }

            Console.WriteLine($"Subkey: {subkey}");
            Console.WriteLine(JsonSerializer.Serialize(data, RegistryContentSerializerOptions));
        }
    }

    private static unsafe object GetStringOrObject(byte[] bytes)
    {
        fixed (byte* pByte = bytes)
        {
            ReadOnlySpan<byte> span = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(pByte);
            string temp = Encoding.UTF8.GetString(span);

            if (temp.AsSpan()[0] is '{' or '[')
            {
                return JsonSerializer.Deserialize<JsonElement>(temp);
            }

            return temp;
        }
    }
}