// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model;

public sealed class NameStringValue : NameValue<string>
{
    public NameStringValue(string name, string value)
        : base(name, value)
    {
    }

    public static NameStringValue Default { get; } = new(SH.ModelNameValueDefaultName, SH.ModelNameValueDefaultDescription);
}