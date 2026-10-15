// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;

namespace Ellen.Launcher.Service.GachaLog.QueryProvider;

public interface IGachaLogQueryProvider
{
    ValueTask<ValueResult<bool, GachaLogQuery>> GetQueryAsync(GachaGameType game);
}