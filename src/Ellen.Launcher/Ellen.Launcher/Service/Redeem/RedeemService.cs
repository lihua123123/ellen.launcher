// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Service.SignIn;
using Ellen.Launcher.ViewModel.User;
using Ellen.Launcher.Web.Hoyolab.Takumi.Event.BbsSignReward;
using Ellen.Launcher.Web.Hoyolab.Takumi.Event.Cdkey;
using System.Collections.Immutable;
using BindingUser = Ellen.Launcher.ViewModel.User.User;
using EntityUser = Ellen.Launcher.Model.Entity.User;
using WebResponse = Ellen.Launcher.Web.Response.Response;

namespace Ellen.Launcher.Service.Redeem;

/// <summary>
/// 国服兑换码服务的单码结果。<see cref="ReturnCode"/> 为上游业务码，
/// <see cref="Message"/> 是已映射的安全提示，不直接回显外部响应文本。
/// </summary>
public sealed record RedeemResult(string Code, int ReturnCode, string Message)
{
    public bool IsSuccess => ReturnCode is 0;

    public bool IsAlreadyUsed => ReturnCode is -2017;
}

public interface IRedeemService
{
    ValueTask<ImmutableArray<SignInGameRole>> GetSupportedRolesAsync(BindingUser user, CancellationToken token = default);

    ValueTask<RedeemResult> RedeemAsync(EntityUser user, SignInGameRole role, string code, CancellationToken token = default);
}

[Service(ServiceLifetime.Singleton, typeof(IRedeemService))]
public sealed partial class RedeemService : IRedeemService
{
    private readonly IGameRoleCollectionService gameRoleCollectionService;
    private readonly RedeemClient redeemClient;

    [GeneratedConstructor]
    public partial RedeemService(IServiceProvider serviceProvider);

    public async ValueTask<ImmutableArray<SignInGameRole>> GetSupportedRolesAsync(BindingUser user, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.IsOversea)
        {
            return [];
        }

        ImmutableArray<SignInGameRole> roles = await gameRoleCollectionService.GetRolesAsync(user, token).ConfigureAwait(false);
        return roles.Where(IsSupportedRole).ToImmutableArray();
    }

    public static bool TryNormalizeCode(string? input, out string code)
    {
        code = input?.Trim() ?? string.Empty;
        return IsValidCode(code);
    }

    public async ValueTask<RedeemResult> RedeemAsync(EntityUser user, SignInGameRole role, string code, CancellationToken token = default)
    {
        try
        {
            WebResponse response = await redeemClient.ExchangeAsync(user, role, code, token).ConfigureAwait(false);
            return new(code, response.ReturnCode, GetMessage(response.ReturnCode));
        }
        catch (InvalidOperationException ex)
        {
            // 客户端侧的账号/区服校验失败，消息来自本地资源，可安全展示。
            return new(code, -3, ex.Message);
        }
        catch
        {
            // 不回显外部异常文本（可能包含 Cookie），避免凭据泄露。
            return new(code, -1, SH.ViewRedeemResultUnknown);
        }
    }

    private static bool IsSupportedRole(SignInGameRole role)
    {
        return role.GameBiz switch
        {
            "hk4e_cn" => role.Game is SignInGame.Genshin && role.Region is "cn_gf01" or "cn_qd01",
            "nap_cn" => role.Game is SignInGame.ZenlessZoneZero && role.Region is "prod_gf_cn" or "prod_qd_cn",
            _ => false,
        };
    }

    private static bool IsValidCode(string code)
    {
        if (code.Length is 0 or > 128)
        {
            return false;
        }

        foreach (char character in code)
        {
            if (!char.IsLetterOrDigit(character) && character is not ('_' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetMessage(int returnCode)
    {
        return returnCode switch
        {
            0 => SH.ViewRedeemSuccess,
            -2001 => SH.ViewRedeemExpired,
            -2003 => SH.ViewRedeemInvalid,
            -2016 => SH.ViewRedeemCooldown,
            -2017 => SH.ViewRedeemAlreadyUsed,
            -1071 => SH.ViewRedeemLoginExpired,
            -1033 => SH.ViewRedeemVerifyFailed,
            -1035 => SH.ViewRedeemNeedVerify,
            _ => SH.FormatViewRedeemUnknown(returnCode),
        };
    }
}
