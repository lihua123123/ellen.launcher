// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.ExceptionService;

namespace Ellen.Launcher.Core.Threading;

public static class SemaphoreSlimExtension
{
    extension(SemaphoreSlim semaphoreSlim)
    {
        public async ValueTask<SemaphoreSlimToken> EnterAsync(CancellationToken token = default)
        {
            try
            {
                await semaphoreSlim.WaitAsync(token).ConfigureAwait(false);
            }
            catch (ObjectDisposedException ex)
            {
                EllenException.OperationCanceled(SH.CoreThreadingSemaphoreSlimDisposed, ex);
            }

            return new(semaphoreSlim);
        }

        public SemaphoreSlimToken Enter()
        {
            try
            {
                semaphoreSlim.Wait();
            }
            catch (ObjectDisposedException ex)
            {
                EllenException.OperationCanceled(SH.CoreThreadingSemaphoreSlimDisposed, ex);
            }

            return new(semaphoreSlim);
        }
    }
}