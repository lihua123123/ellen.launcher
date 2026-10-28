// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.ViewModel.User;
using Ellen.Launcher.Web.Hoyolab;
using Ellen.Launcher.Web.Hoyolab.PublicData.DeviceFp;
using Ellen.Launcher.Web.Response;

namespace Ellen.Launcher.Service.User;

[Service(ServiceLifetime.Singleton, typeof(IUserFingerprintService))]
public sealed partial class UserFingerprintService : IUserFingerprintService
{
    private readonly IServiceScopeFactory serviceScopeFactory;

    [GeneratedConstructor]
    public partial UserFingerprintService(IServiceProvider serviceProvider);

    public async ValueTask TryInitializeAsync(ViewModel.User.User user, CancellationToken token = default, bool forceRefresh = false)
    {
        if (user.IsOversea)
        {
            // Disable HoYoLAB fp approach
            return;
        }

        // 实测（2026-10-29）：getFp 新签发的指纹有一段「成熟期」（分钟级～小时级），
        // 期间战绩接口（ZZZ avatar/basic|info 返回 10041、原神 index 返回 5003）会拒绝它；
        // 而已注册且用过的旧指纹始终可用。因此绝不能按 7 天定时轮换——那会用未成熟的新
        // 指纹顶掉可用的旧指纹，导致「我的角色 / 实时便笺」在此期间全部获取失败。
        // 只在指纹为空（新账号 / 从未签发）时才申请。
        if (!forceRefresh && !string.IsNullOrEmpty(user.Fingerprint))
        {
            return;
        }

        string device = Core.Random.GetUpperAndNumberString(12);
        string product = Core.Random.GetUpperAndNumberString(6);

        // To anyone who finds this code, please don't fully copy this.
        Dictionary<string, object> extendProperties = new()
        {
            { "proxyStatus", 0 },
            { "isRoot", 0 },
            { "romCapacity", "512" },
            { "deviceName", device },
            { "productName", product },
            { "romRemain", "512" },
            { "hostname", "dg02-pool03-kvm87" },
            { "screenSize", "1440x2905" },
            { "isTablet", 0 },
            { "aaid", string.Empty },
            { "model", device },
            { "brand", "XiaoMi" },
            { "hardware", "qcom" },
            { "deviceType", "OP5913L1" },
            { "devId", "REL" },
            { "serialNumber", "unknown" },
            { "sdCapacity", 512215 },
            { "buildTime", "1693626947000" },
            { "buildUser", "android-build" },
            { "simState", 5 },
            { "ramRemain", "239814" },
            { "appUpdateTimeDiff", 1702604034482 },
            { "deviceInfo", $@"XiaoMi\/{product}\/OP5913L1:13\/SKQ1.221119.001\/T.118e6c7-5aa23-73911:user\/release-keys" },
            { "vaid", string.Empty },
            { "buildType", "user" },
            { "sdkVersion", "34" },
            { "ui_mode", "UI_MODE_TYPE_NORMAL" },
            { "isMockLocation", 0 },
            { "cpuType", "arm64-v8a" },
            { "isAirMode", 0 },
            { "ringMode", 2 },
            { "chargeStatus", 1 },
            { "manufacturer", "XiaoMi" },
            { "emulatorStatus", 0 },
            { "appMemory", "512" },
            { "osVersion", "14" },
            { "vendor", "unknown" },
            { "accelerometer", "1.4883357x7.1712894x6.2847486" },
            { "sdRemain",  239600 },
            { "buildTags", "release-keys" },
            { "packageName", "com.mihoyo.hyperion" },
            { "networkType", "WiFi" },
            { "oaid", string.Empty },
            { "debugStatus", 1 },
            { "ramCapacity", "469679" },
            { "magnetometer", "20.081251x-27.487501x2.1937501" },
            { "display", $"{product}_13.1.0.181(CN01)" },
            { "appInstallTimeDiff", 1688455751496 },
            { "packageVersion", "2.20.1" },
            { "gyroscope", "0.030226856x0.014647375x0.010652636" },
            { "batteryStatus", 100 },
            { "hasKeyboard", 0 },
            { "board", "taro" },
        };

        DeviceFpData data = new()
        {
            DeviceId = Core.Random.GetLowerHexString(16),
            SeedId = $"{Guid.NewGuid()}",
            Platform = "2",
            SeedTime = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            ExtFields = JsonSerializer.Serialize(extendProperties),
            AppName = "bbs_cn",
            BbsDeviceId = HoyolabOptions.DeviceId36,
            DeviceFp = string.IsNullOrEmpty(user.Fingerprint) ? Core.Random.GetLowerHexString(13) : user.Fingerprint,
        };

        using (IServiceScope scope = serviceScopeFactory.CreateScope())
        {
            DeviceFpClient deviceFpClient = scope.ServiceProvider.GetRequiredService<DeviceFpClient>();
            Response<DeviceFpWrapper> response = await deviceFpClient.GetFingerprintAsync(data, token).ConfigureAwait(false);

            ResponseValidator.TryValidate(response, scope.ServiceProvider, out DeviceFpWrapper? wrapper);

            // 刷新失败时保留旧指纹（TryUpdateFingerprint 对空值是 no-op）：
            // ZZZ 战绩接口强依赖 x-rpc-device_fp，缺失或未注册一律 10041。
            if (user.TryUpdateFingerprint(wrapper?.DeviceFp))
            {
                user.NeedDbUpdateAfterResume = true;
            }
        }
    }
}