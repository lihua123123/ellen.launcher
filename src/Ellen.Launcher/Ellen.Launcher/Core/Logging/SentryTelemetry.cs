// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Setting;

namespace Ellen.Launcher.Core.Logging;

/// <summary>
/// 遥测（Sentry）配置。
/// <para>
/// 本项目为分支版本，默认**不向任何第三方上报**：只有用户在设置中显式填入自己的 DSN
/// 之后，才会开始上报。留空时 Sentry SDK 处于禁用状态，所有 <c>SentrySdk.*</c> 调用均为空操作。
/// </para>
/// </summary>
public static class SentryTelemetry
{
    /// <summary>
    /// 设置项名称（<c>settings.json</c>）。留空表示不上报。
    /// </summary>
    public const string DsnSettingKey = "Ellen::Telemetry::SentryDsn";

    public static string Dsn
    {
        get => LocalSetting.Get(DsnSettingKey, string.Empty);
    }

    public static bool IsEnabled
    {
        get => !string.IsNullOrEmpty(Dsn);
    }
}
