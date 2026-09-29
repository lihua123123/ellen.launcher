// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Factory.QuickResponse;

public interface IQRCodeFactory
{
    byte[] Create(string source);
}