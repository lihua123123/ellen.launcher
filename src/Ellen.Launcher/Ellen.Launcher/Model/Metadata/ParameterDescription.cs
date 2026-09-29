// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model.Metadata;

public sealed class ParameterDescription
{
    public ParameterDescription(string parameter, string description)
    {
        Parameter = parameter;
        Description = description;
    }

    public string Parameter { get; }

    public string Description { get; }

    public bool IsSpecial { get; set; }
}