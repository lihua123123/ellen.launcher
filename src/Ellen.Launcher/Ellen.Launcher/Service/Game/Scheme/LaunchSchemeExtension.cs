// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity.Primitive;
using Ellen.Launcher.Model.Intrinsic;

namespace Ellen.Launcher.Service.Game.Scheme;

public static class LaunchSchemeExtension
{
    extension(LaunchScheme scheme)
    {
        public SchemeType SchemeType
        {
            get => (scheme.Channel, scheme.IsOversea) switch
            {
                (ChannelType.Bili, false) => SchemeType.ChineseBilibili,
                (_, false) => SchemeType.ChineseOfficial,
                (_, true) => SchemeType.Oversea,
            };
        }
    }
}