// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.IO.Pipes;

namespace Ellen.Launcher.Core.LifeCycle.InterProcess;

public static class NamedPipeClientStreamExtension
{
    extension(NamedPipeClientStream clientStream)
    {
        public bool TryConnectOnce()
        {
            try
            {
                clientStream.Connect(TimeSpan.Zero);
                return true;
            }
            catch (TimeoutException)
            {
                return false;
            }
        }
    }
}