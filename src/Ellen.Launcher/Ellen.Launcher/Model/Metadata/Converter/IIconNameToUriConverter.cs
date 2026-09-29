// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model.Metadata.Converter;

public interface IIconNameToUriConverter
{
    static abstract Uri IconNameToUri(string name);
}