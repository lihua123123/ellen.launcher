// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Endpoint.Hoyolab;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord;

public sealed class CardVerificationHeaders
{
    public int ChallengeGame { get; private set; }

    public string ChallengePath { get; private set; } = string.Empty;

    public string Page { get; private set; } = string.Empty;

    public static CardVerificationHeaders CreateForActCalendar(IApiEndpoints apiEndpoints)
    {
        return Create(apiEndpoints.GameRecordHardChallengePath(), $"{HoyolabOptions.ToolVersion}_#/ys/calendar");
    }

    public static CardVerificationHeaders CreateForDailyNote(IApiEndpoints apiEndpoints)
    {
        return Create(apiEndpoints.GameRecordDailyNotePath());
    }

    /// <summary>
    /// 绝区零便笺的人机验证参数：挑战路径是 zzz/note，游戏编号为绝区零。
    /// </summary>
    public static CardVerificationHeaders CreateForZZZNote()
    {
        return Create("/event/game_record_zzz/api/zzz/note", $"{HoyolabOptions.ToolVersion}_#/zzz", 9);
    }

    public static CardVerificationHeaders CreateForIndex(IApiEndpoints apiEndpoints)
    {
        return Create(apiEndpoints.GameRecordIndexPath());
    }

    public static CardVerificationHeaders CreateForSpiralAbyss(IApiEndpoints apiEndpoints)
    {
        return Create(apiEndpoints.GameRecordSpiralAbyssPath());
    }

    public static CardVerificationHeaders CreateForCharacterAll(IApiEndpoints apiEndpoints)
    {
        return Create(apiEndpoints.GameRecordCharacterList(), $"{HoyolabOptions.ToolVersion}_#/ys/role/all");
    }

    public static CardVerificationHeaders CreateForCharacterDetail(IApiEndpoints apiEndpoints)
    {
        return Create(apiEndpoints.GameRecordCharacterList(), $"{HoyolabOptions.ToolVersion}_#/ys/role/detail");
    }

    public static CardVerificationHeaders CreateForRoleCombat(IApiEndpoints apiEndpoints)
    {
        return Create(apiEndpoints.GameRecordRoleCombatPath());
    }

    public static CardVerificationHeaders CreateForHardChallenge(IApiEndpoints apiEndpoints)
    {
        return Create(apiEndpoints.GameRecordHardChallengePath());
    }

    private static CardVerificationHeaders Create(string path, string page = $"{HoyolabOptions.ToolVersion}_#/ys", int challengeGame = 2)
    {
        return new()
        {
            ChallengeGame = challengeGame,
            ChallengePath = path,
            Page = page,
        };
    }
}