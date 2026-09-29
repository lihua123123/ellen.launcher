// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Navigation;
using Ellen.Launcher.Service.Navigation;
using Ellen.Launcher.UI.Content;
using Ellen.Launcher.ViewModel.Abstraction;
using Ellen.Launcher.Win32.Foundation;

namespace Ellen.Launcher.UI.Xaml.Control;

[SuppressMessage("", "CA1001")]
public partial class ScopedPage : Page
{
    private readonly CancellationTokenSource viewCts = new();
    private IServiceScope? scope;
    private bool loadingOverrideRan;

    protected ScopedPage()
    {
        // Events/Override Methods order
        // ----------------------------------------------------------------------
        // Page Navigation methods:
        // - OnNavigatedTo
        // FrameworkElement events:
        // - Loading (args: null)
        // - EffectiveViewportChanged
        // - SizeChanged
        // - LayoutUpdated (sender & args always null)
        // - Loaded
        // - LayoutUpdated (sender & args always null) (Can trigger multiple times)
        // OnNavigatedTo -> Loading -> Loaded
        // ----------------------------------------------------------------------
        // Page Navigation methods:
        // - OnNavigatingFrom
        // - OnNavigatedFrom
        // FrameworkElement events:
        // - LayoutUpdated (sender & args always null)
        // - Unloaded
        // - LayoutUpdated (might or might not be called)
        // OnNavigatingFrom -> OnNavigatedFrom -> Unloaded
        // ----------------------------------------------------------------------
        Loading += OnLoading;
        Unloaded += OnUnloaded;
    }

    public CancellationToken CancellationToken { get => viewCts.Token; }

    /// <summary>
    /// 该页面是否命中 Frame 的 NavigationCacheMode 缓存。为 true 时，离开视觉树**不**拆掉
    /// ViewModel / DI 作用域 / CancellationToken —— 否则缓存回来的页面会带着一个已 Cancel+Dispose
    /// 的令牌、被清空的 DataContext 和已销毁的作用域，必然坏。默认 false，其余页面行为逐字节不变。
    /// </summary>
    protected virtual bool PreserveOnUnload => false;

    public virtual void UnloadObjectOverride(DependencyObject unloadableObject)
    {
        XamlMarkupHelper.UnloadObject(unloadableObject);
    }

    /// <summary>
    /// Override this method to implement the loading logic.
    /// The page is not attached to the visual tree yet when this method is called.
    /// </summary>
    protected virtual void LoadingOverride()
    {
    }

    /// <summary>
    /// Set <see cref="FrameworkElement.DataContext"/> to an instance of <typeparamref name="TViewModel"/>, which will be retrieved from ServiceProvider
    /// </summary>
    /// <typeparam name="TViewModel">The type of ViewModel</typeparam>
    protected void InitializeDataContext<TViewModel>()
        where TViewModel : class, IViewModel
    {
        ArgumentNullException.ThrowIfNull(scope);

        TViewModel viewModel = scope.ServiceProvider.GetRequiredService<TViewModel>();
        using (viewModel.CriticalSection.Enter())
        {
            viewModel.Resurrect();
            viewModel.CancellationToken = CancellationToken;
            viewModel.DeferContentLoader = new DeferContentLoader(this);

            DataContext = viewModel;
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        // OnNavigatedTo is called before any FrameworkElement event on the page.
        if (e.Parameter is INavigationCompletionSource data)
        {
            NavigationExtraDataSupport.NotifyRecipientAsync(this, data, CancellationToken).SafeForget();
        }
    }

    private void OnLoading(FrameworkElement element, object e)
    {
        // 不再用 `Loading -= OnLoading` 来保证只跑一次（那样就数不出重触发了）。
        // 命中缓存的页面返回时 Loading 会再次触发，此时作用域与 ViewModel 都还活着，
        // 再跑一次 LoadingOverride 会造出第二个 ViewModel，所以用守卫拦住。
        if (loadingOverrideRan)
        {
            return;
        }

        loadingOverrideRan = true;

        XamlContext? context = element.XamlRoot.XamlContext();
        ArgumentNullException.ThrowIfNull(context);
        scope = context.ServiceProvider.CreateScope();

        LoadingOverride();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (PreserveOnUnload)
        {
            // 缓存页：离开视觉树时让 ViewModel / 作用域 / 令牌活着，返回时列表容器才能原样还在。
            return;
        }

        // Cancel all tasks executed by the view model
        viewCts.Cancel();

        if (DataContext is IViewModel viewModel)
        {
            // Wait to ensure critical viewmodel operation is completed
            using (viewModel.CriticalSection.Enter())
            {
                viewModel.Uninitialize();
            }

            try
            {
                DataContext = default;
            }
            catch (Exception ex)
            {
                if (ex.HResult is not HRESULT.E_UNEXPECTED)
                {
                    throw;
                }
            }
        }

        viewCts.Dispose();

        // Dispose the scope
        scope?.Dispose();
        scope = default;

        if (this.IsDisposed)
        {
            return;
        }

        Unloaded -= OnUnloaded;
    }
}