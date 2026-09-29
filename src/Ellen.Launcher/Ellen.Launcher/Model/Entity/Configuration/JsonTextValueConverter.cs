// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Ellen.Launcher.Core.Text.Json;

namespace Ellen.Launcher.Model.Entity.Configuration;

public sealed class JsonTextValueConverter<TPropertyType> : ValueConverter<TPropertyType, string>
{
    [SuppressMessage("", "SH007")]
    public JsonTextValueConverter()
        : base(
            static obj => JsonSerializer.Serialize(obj, JsonOptions.Default),
            static str => string.IsNullOrEmpty(str) ? default! : JsonSerializer.Deserialize<TPropertyType>(str, JsonOptions.Default)!)
    {
    }
}