// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.LifeCycle.InterProcess.Model;
using System.IO.Pipes;

namespace Ellen.Launcher.Core.LifeCycle.InterProcess;

public sealed partial class ToastNotificationPipeClient : IDisposable
{
    private readonly NamedPipeClientStream pipeClient = new(".", PrivateNamedPipe.ToastPipeName, PipeDirection.In);

    public void Dispose()
    {
        pipeClient.Dispose();
    }

    public ToastNotificationRequest? TryGetRequest()
    {
        try
        {
            pipeClient.Connect(100);

            ToastNotificationRequest? request = JsonSerializer.Deserialize<ToastNotificationRequest>(pipeClient);
            return request;
        }
        catch
        {
            return null;
        }
    }
}
