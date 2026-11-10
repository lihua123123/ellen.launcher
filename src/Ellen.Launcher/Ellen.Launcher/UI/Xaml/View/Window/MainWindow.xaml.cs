// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Ellen.Launcher.Core.Setting;
using Ellen.Launcher.Service;
using Ellen.Launcher.Service.BackgroundMediaPlayer;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.UI.Windowing;
using Ellen.Launcher.UI.Windowing.Abstraction;
using Ellen.Launcher.ViewModel;
using System.Collections.Immutable;
using Windows.Graphics;

namespace Ellen.Launcher.UI.Xaml.View.Window;

[Service(ServiceLifetime.Transient)]
public sealed partial class MainWindow : Microsoft.UI.Xaml.Window,
    IXamlWindowClosedHandler,
    IXamlWindowExtendContentIntoTitleBar,
    IXamlWindowHasInitSize
{
    private readonly App app;
    private readonly IBackgroundMediaPlayerService backgroundMediaPlayerService;

    public static MainWindow Instance { get; private set; } = null!;

    public MainWindow(IServiceProvider serviceProvider)
    {
        Service.EnemyArchive.StartupProbe.Mark("mainwindow-ctor-begin");
        Instance = this;
        InitializeComponent();
        Service.EnemyArchive.StartupProbe.Mark("mainwindow-xaml");

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            SizeInt32 minSize = ScaledSizeInt32.CreateForWindow(1000, 600, this);
            presenter.PreferredMinimumWidth = minSize.Width;
            presenter.PreferredMinimumHeight = minSize.Height;
        }
        Service.EnemyArchive.StartupProbe.Mark("mainwindow-presenter");

        IServiceScope scope = serviceProvider.CreateScope();
        this.InitializeController(scope.ServiceProvider);
        Service.EnemyArchive.StartupProbe.Mark("mainwindow-controller");

        MainView.InitializeDataContext<MainViewModel>(scope.ServiceProvider);
        Service.EnemyArchive.StartupProbe.Mark("mainwindow-datacontext");

        app = scope.ServiceProvider.GetRequiredService<App>();
        backgroundMediaPlayerService = serviceProvider.GetRequiredService<IBackgroundMediaPlayerService>();

        // P5 启动探针：只在 ELLEN_STARTUP_PROBE 设置时记录，正常启动无副作用。
        Service.EnemyArchive.StartupProbe.Mark("mainwindow-ctor");
        MainView.Loaded += OnMainViewLoaded;
    }

    private void OnMainViewLoaded(object sender, RoutedEventArgs e)
    {
        Service.EnemyArchive.StartupProbe.Mark("mainview-loaded");
        Service.EnemyArchive.StartupProbe.AttachFirstFrame(this);
        _ = Service.EnemyArchive.WikiUiBench.TryStart(this);
    }

    public SizeInt32 InitSize { get => ScaledSizeInt32.CreateForWindow(1200, 741, this); }

    public FrameworkElement TitleBarCaptionAccess { get => MainView.TitleBar; }

    public ImmutableArray<FrameworkElement> TitleBarPassthrough { get => []; }

    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        PointerPointProperties props = e.GetCurrentPoint(null).Properties;

        if (props.IsXButton1Pressed)
        {
            if (MainView.ContentFrame.CanGoBack)
            {
                MainView.ContentFrame.GoBack();
                e.Handled = true;
            }
        }
        else if (props.IsXButton2Pressed)
        {
            if (MainView.ContentFrame.CanGoForward)
            {
                MainView.ContentFrame.GoForward();
                e.Handled = true;
            }
        }
    }

    public void OnWindowClosing(out bool cancel)
    {
        if (XamlApplicationLifetime.Exiting)
        {
            cancel = false;
            return;
        }

         //Wait for view to be initialized (show update content webview window)
        if (MainView.IsLoaded && (MainView.DataContext is ViewModel.Abstraction.ViewModel { IsInitialized: false }))
        {
            cancel = true;
            return;
        }

        cancel = false;
    }

    public void OnWindowClosed()
    {
        backgroundMediaPlayerService.Stop();

        if (XamlApplicationLifetime.Exiting)
        {
            return;
        }

        if (XamlApplicationLifetime.WindowClosedForGameLaunch)
        {
            return;
        }

        app.Exit();
    }
}
