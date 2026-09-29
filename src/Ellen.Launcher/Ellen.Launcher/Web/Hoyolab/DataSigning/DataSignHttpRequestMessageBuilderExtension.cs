// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Request.Builder;

namespace Ellen.Launcher.Web.Hoyolab.DataSigning;

public static class DataSignHttpRequestMessageBuilderExtension
{
    extension(HttpRequestMessageBuilder builder)
    {
        public async ValueTask SignDataAsync(DataSignAlgorithmVersion version, SaltType saltType, bool includeChars)
        {
            DataSignOptions options = await DataSignOptions.CreateFromHttpRequestMessageBuilderAsync(builder, saltType, includeChars, version).ConfigureAwait(false);
            builder.SetHeader("DS", DataSignAlgorithm.GetDataSign(options));
        }
    }
}