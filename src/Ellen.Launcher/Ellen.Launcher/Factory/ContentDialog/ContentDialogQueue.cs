// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Core.LifeCycle;
using Windows.Foundation;

namespace Ellen.Launcher.Factory.ContentDialog;

[Service(ServiceLifetime.Singleton, typeof(IContentDialogQueue))]
[SuppressMessage("", "SH003")]
[SuppressMessage("", "SH100")]
[SuppressMessage("", "RS0030")]
public sealed partial class ContentDialogQueue : IContentDialogQueue
{
    private readonly AsyncLock dialogShowLock = new();

    private readonly ICurrentXamlWindowReference currentWindowReference;
    private readonly ITaskContext taskContext;

    [GeneratedConstructor]
    public partial ContentDialogQueue(IServiceProvider serviceProvider);

    public bool IsDialogShowing
    {
        get
        {
            if (currentWindowReference.Window is null)
            {
                return false;
            }

            if (dialogShowLock.TryLock(out AsyncLock.Releaser releaser))
            {
                using (releaser)
                {
                }

                return false;
            }

            return true;
        }
    }

    public ValueContentDialogTask EnqueueAndShowAsync(Microsoft.UI.Xaml.Controls.ContentDialog contentDialog)
    {
        TaskCompletionSource queueSource = new();
        TaskCompletionSource<ContentDialogResult> resultSource = new();

        PrivateEnqueueAndShowAsync(contentDialog, queueSource, resultSource).SafeForget();
        return new(queueSource.Task, resultSource.Task);
    }

    private async Task PrivateEnqueueAndShowAsync(Microsoft.UI.Xaml.Controls.ContentDialog contentDialog, TaskCompletionSource queueSource, TaskCompletionSource<ContentDialogResult> resultSource)
    {
        using (await dialogShowLock.LockAsync().ConfigureAwait(false))
        {
            await taskContext.SwitchToMainThreadAsync();
            queueSource.TrySetResult();

            if (contentDialog.XamlRoot is null)
            {
                EllenException.NotSupported("Dialog created without XamlRoot");
            }

            if (contentDialog.XamlRoot != currentWindowReference.XamlRoot)
            {
                // User close the window on previous dialog, and this dialog still using old XamlRoot.
                // And that's why we didn't use dialog's DispatcherQueue to switch thread either.
                EllenException.NotSupported("Dialog using different XamlRoot");
            }

            IAsyncOperation<ContentDialogResult> operation = contentDialog.ShowAsync();
            contentDialog.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            resultSource.SetResult(await operation);
        }
    }
}