// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.ViewModel.GachaLog;

namespace Ellen.Launcher.Service.GachaLog;

public interface IGachaLogWishCountdownService
{
    ValueTask<WishCountdownBundle> GetWishCountdownBundleAsync(GachaLogWishCountdownServiceMetadataContext context);
}