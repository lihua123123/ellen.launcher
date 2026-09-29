// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Response;

namespace Ellen.Launcher.Web.Upstream.Response;

public sealed class EllenResponse : Web.Response.Response, ILocalizableResponse, ICommonResponse<EllenResponse>
{
    [JsonConstructor]
    public EllenResponse(int returnCode, string message, string? localizationKey)
        : base(returnCode, message)
    {
        LocalizationKey = localizationKey;
    }

    [JsonPropertyName("l10nKey")]
    public string? LocalizationKey { get; set; }

    static EllenResponse ICommonResponse<EllenResponse>.CreateDefault(int returnCode, string message)
    {
        return new(returnCode, message, default);
    }

    public override string ToString()
    {
        if (this.GetLocalizationMessageOrDefault() == null)
        {
            return string.Format(SH.WebResponse, ReturnCode, Message);
        }
        return SH.FormatWebResponse(ReturnCode, this.GetLocalizationMessageOrDefault());
    }
}

[SuppressMessage("", "SA1402")]
public sealed class EllenResponse<TData> : Response<TData>, ILocalizableResponse, ICommonResponse<EllenResponse<TData>>
{
    [JsonConstructor]
    public EllenResponse(int returnCode, string message, TData? data, string? localizationKey)
        : base(returnCode, message, data)
    {
        LocalizationKey = localizationKey;
    }

    [JsonPropertyName("l10nKey")]
    public string? LocalizationKey { get; set; }

    static EllenResponse<TData> ICommonResponse<EllenResponse<TData>>.CreateDefault(int returnCode, string message)
    {
        return new(returnCode, message, default, default);
    }

    public override string ToString()
    {
        return SH.FormatWebResponse(ReturnCode, this.GetLocalizationMessageOrDefault());
    }
}