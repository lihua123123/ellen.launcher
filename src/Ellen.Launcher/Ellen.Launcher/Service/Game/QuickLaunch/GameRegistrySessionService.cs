// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Win32;
using Ellen.Launcher.Core.Setting;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ellen.Launcher.Service.Game.QuickLaunch;

/// <summary>
/// 游戏登录态存在注册表里（参考 Starward 的做法）：
/// 把各账号的 MIHOYOSDK 快照按 mid 存下来，启动前把目标账号的快照写回注册表，游戏就会直接登录该账号。
/// </summary>
public interface IGameRegistrySessionService
{
    /// <summary>
    /// 读取当前注册表里的登录账号 mid。
    /// </summary>
    string? TryGetCurrentMid(GameLaunchKind kind, bool isOversea);

    /// <summary>
    /// 把当前注册表里的登录态快照下来（按 mid 归档），用于以后切换回这个账号。
    /// </summary>
    void Capture(GameLaunchKind kind, bool isOversea);

    /// <summary>
    /// 把 <paramref name="mid"/> 对应的快照写回注册表。没有快照时返回 <see langword="false"/>。
    /// </summary>
    bool TryApply(GameLaunchKind kind, bool isOversea, string mid);
}

[Service(ServiceLifetime.Singleton, typeof(IGameRegistrySessionService))]
public sealed partial class GameRegistrySessionService : IGameRegistrySessionService
{
    private const string SdkChineseValueName = "MIHOYOSDK_ADL_PROD_CN_h3123967166";
    private const string SdkOverseaValueName = "MIHOYOSDK_ADL_PROD_OVERSEA_h1158948810";

    private static readonly byte[] InitializationVector = [0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF];

    public string? TryGetCurrentMid(GameLaunchKind kind, bool isOversea)
    {
        string? sdk = ReadSdk(kind, isOversea);
        return sdk is null ? null : ExtractMid(sdk);
    }

    public void Capture(GameLaunchKind kind, bool isOversea)
    {
        string? sdk = ReadSdk(kind, isOversea);
        if (string.IsNullOrEmpty(sdk))
        {
            return;
        }

        string? mid = ExtractMid(sdk);
        if (string.IsNullOrEmpty(mid))
        {
            return;
        }

        string storeKey = GetStoreKey(kind, isOversea);
        Dictionary<string, string> store = LoadStore(storeKey);

        if (store.TryGetValue(mid, out string? existing) && string.Equals(existing, sdk, StringComparison.Ordinal))
        {
            return;
        }

        store[mid] = sdk;
        SaveStore(storeKey, store);
    }

    public bool TryApply(GameLaunchKind kind, bool isOversea, string mid)
    {
        if (GetRegistryTarget(kind, isOversea) is not var (keyName, valueName))
        {
            return false;
        }

        Dictionary<string, string> store = LoadStore(GetStoreKey(kind, isOversea));
        if (!store.TryGetValue(mid, out string? sdk) || string.IsNullOrEmpty(sdk))
        {
            return false;
        }

        // 注册表里是 UTF-8 字节 + 结尾 0，和 RegistryInterop.Set 保持一致
        byte[] target = [.. Encoding.UTF8.GetBytes(sdk), 0];
        Registry.SetValue(keyName, valueName, target);

        return string.Equals(ReadSdk(kind, isOversea), sdk, StringComparison.Ordinal);
    }

    private static (string KeyName, string ValueName)? GetRegistryTarget(GameLaunchKind kind, bool isOversea)
    {
        return (kind, isOversea) switch
        {
            (GameLaunchKind.Genshin, false) => (@"HKEY_CURRENT_USER\Software\miHoYo\原神", SdkChineseValueName),
            (GameLaunchKind.Genshin, true) => (@"HKEY_CURRENT_USER\Software\miHoYo\Genshin Impact", SdkOverseaValueName),
            (GameLaunchKind.ZenlessZoneZero, false) => (@"HKEY_CURRENT_USER\Software\miHoYo\绝区零", SdkChineseValueName),
            (GameLaunchKind.ZenlessZoneZero, true) => (@"HKEY_CURRENT_USER\Software\Cognosphere\ZZZ", SdkOverseaValueName),
            _ => null,
        };
    }

    private static string? ReadSdk(GameLaunchKind kind, bool isOversea)
    {
        if (GetRegistryTarget(kind, isOversea) is not var (keyName, valueName))
        {
            return null;
        }

        try
        {
            if (Registry.GetValue(keyName, valueName, null) is not byte[] bytes || bytes.Length is 0)
            {
                return null;
            }

            int length = Array.IndexOf(bytes, (byte)0);
            if (length < 0)
            {
                length = bytes.Length;
            }

            return Encoding.UTF8.GetString(bytes, 0, length);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// SDK 快照是 base64(DES-CBC(账号列表 JSON))，解出来取当前登录账号的 mid
    /// （mid 形如 0xn1hcp5do_mhy；有多个账号时取 is_login 为 true 的那条）。
    /// </summary>
    private static string? ExtractMid(string sdk)
    {
        try
        {
            if (TryDecodeSdk(sdk) is not { } decoded)
            {
                return null;
            }

            MatchCollection matches = Regex.Matches(decoded, "\"mid\"\\s*:\\s*\"(?<mid>[^\"]+)\"");
            if (matches.Count is 0)
            {
                return null;
            }

            if (matches.Count is 1)
            {
                return matches[0].Groups["mid"].Value;
            }

            // 多个账号时优先 is_login:true 的那条（mid 字段在 is_login 之前）
            int loginIndex = decoded.IndexOf("\"is_login\":true", StringComparison.Ordinal);
            if (loginIndex >= 0)
            {
                string? candidate = null;
                foreach (Match match in matches)
                {
                    if (match.Index < loginIndex)
                    {
                        candidate = match.Groups["mid"].Value;
                    }
                }

                if (candidate is not null)
                {
                    return candidate;
                }
            }

            return matches[0].Groups["mid"].Value;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 快照用「网卡 MAC 前 8 位」做 DES 密钥加密；机器上可能有多张网卡，
    /// 这里把每张网卡的密钥都试一遍（原解码器只用第一张，多网卡时会解不出来）。
    /// </summary>
    private static string? TryDecodeSdk(string sdk)
    {
        try
        {
            byte[] cipher = Convert.FromBase64String(sdk);
            foreach (string key in EnumerateCandidateKeys())
            {
                try
                {
                    using DES des = DES.Create();
                    des.Key = Encoding.UTF8.GetBytes(key);
                    string text = Encoding.UTF8.GetString(des.DecryptCbc(cipher, InitializationVector));
                    if (text.Contains("\"mid\"", StringComparison.Ordinal))
                    {
                        return text;
                    }
                }
                catch (CryptographicException)
                {
                    // 这张网卡的密钥不对，换下一张
                }
            }
        }
        catch (FormatException)
        {
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCandidateKeys()
    {
        HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase);
        foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            string address = networkInterface.GetPhysicalAddress().ToString();
            if (address.Length >= 8 && keys.Add(address[..8]))
            {
                yield return address[..8];
            }
        }

        // 没有可用网卡时原解码器的兜底
        if (keys.Add("FFFFFFFF"))
        {
            yield return "FFFFFFFF";
        }
    }

    private static string GetStoreKey(GameLaunchKind kind, bool isOversea)
    {
        return $"Ellen::Game::RegistrySession::{kind}:{(isOversea ? "os" : "cn")}";
    }

    private static Dictionary<string, string> LoadStore(string key)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(LocalSetting.Get(key, "{}")) ?? [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static void SaveStore(string key, Dictionary<string, string> store)
    {
        LocalSetting.Set(key, JsonSerializer.Serialize(store));
    }
}
