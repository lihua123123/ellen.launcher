// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Globalization;

namespace Ellen.Launcher.Model;

public sealed class NameCultureInfoValue : NameValue<CultureInfo>
{
    public NameCultureInfoValue(string name, CultureInfo value, LocalizationSource localizationSource)
        : base(name, value)
    {
        IsMaintainedByEllen = localizationSource.HasFlag(LocalizationSource.Ellen);
        IsMaintainedByCrowdin = localizationSource.HasFlag(LocalizationSource.Crowdin);
        IsMaintainedByGemini = localizationSource.HasFlag(LocalizationSource.Gemini);
    }

    public bool IsMaintainedByEllen { get; }

    public bool IsMaintainedByCrowdin { get; }

    public bool IsMaintainedByGemini { get; }
}