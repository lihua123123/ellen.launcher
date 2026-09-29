// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model.InterChange.GachaLog;

public sealed class UIGFView
{
    public string? Version { get; private init; }

    public bool IsLegacy { get; private init; }

    public static UIGFView Create(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        if (document.RootElement.TryGetProperty("info"u8, out JsonElement info))
        {
            if (info.TryGetProperty("version"u8, out JsonElement versionElement))
            {
                return new()
                {
                    Version = versionElement.GetString(),
                    IsLegacy = false,
                };
            }

            if (info.TryGetProperty("uigf_version"u8, out JsonElement legacyVersionElement))
            {
                return new()
                {
                    Version = legacyVersionElement.GetString(),
                    IsLegacy = true,
                };
            }
        }

        return new();
    }
}
