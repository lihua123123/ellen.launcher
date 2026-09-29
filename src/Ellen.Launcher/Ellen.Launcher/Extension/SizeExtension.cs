// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Extension;

public static class SizeExtension
{
    public static double Size(this Windows.Foundation.Size size)
    {
        return size.Width * size.Height;
    }
}