// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.
// Copyright (c) Snap Hutao RP. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Windows.AppLifecycle;
using Ellen.Launcher.Core;
using Ellen.Launcher.Core.Logging;
using Ellen.Launcher.Core.Setting;
using Ellen.Launcher.Core.Shell;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Factory.Picker;
using Ellen.Launcher.Model;
using Ellen.Launcher.Service;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.ViewModel.Setting;
using Ellen.Launcher.Web.Hoyolab;
using Ellen.Launcher.Web.Upstream;
using Ellen.Launcher.Web.Upstream.Response;
using Ellen.Launcher.Web.Response;
using System.Runtime.InteropServices;

namespace Ellen.Launcher.ViewModel.Guide;

[BindableCustomPropertyProvider]
[Service(ServiceLifetime.Singleton)]
public sealed partial class GuideViewModel : Abstraction.ViewModel
{
    private readonly IFileSystemPickerInteraction fileSystemPickerInteraction;
    private readonly IContentDialogFactory contentDialogFactory;
    private readonly IServiceProvider serviceProvider;
    private readonly IMessenger messenger;
    private readonly IShellLinkInterop shellLinkInterop;

    [GeneratedConstructor]
    public partial GuideViewModel(IServiceProvider serviceProvider);

    public static string AllCulturesWelcomeText
    {
        get => string.Join('+', CultureOptions.Cultures.Select(c => SH.GetString("GuideWindowTitle", c.Value)));
    }

    public uint State
    {
        get
        {
            GuideState state = UnsafeLocalSetting.Get(SettingKeys.GuideState, GuideState.Language);

            // 图像资源的两步（选质量/档案、按分类下载）已经随「图片逐张走 R2」下线：
            // 老版本可能正好停在这两个状态，读到时直接推进到 Completed，
            // 否则 SwitchPresenter 找不到对应分支，欢迎页会是一片空白。
            if (state is GuideState.StaticResourceSetting or GuideState.StaticResourceBegin)
            {
                state = GuideState.Completed;
                LocalSetting.Set(SettingKeys.GuideState, (uint)state);
            }

            switch (state)
            {
                case GuideState.Document:
                    IsTermOfServiceAgreed = false;
                    IsPrivacyPolicyAgreed = false;
                    IsIssueReportAgreed = false;
                    IsOpenSourceLicenseAgreed = false;
                    IsDownStreamAgreed = false;
                    (NextOrCompleteButtonText, IsNextOrCompleteButtonEnabled) = (SH.ViewModelGuideActionNext, false);
                    break;
                case GuideState.Completed:
                    (NextOrCompleteButtonText, IsNextOrCompleteButtonEnabled) = (SH.ViewModelGuideActionComplete, true);
                    break;
                default:
                    (NextOrCompleteButtonText, IsNextOrCompleteButtonEnabled) = (SH.ViewModelGuideActionNext, true);
                    break;
            }

            return (uint)state;
        }

        set
        {
            value = Math.Clamp(value, 0, (uint)GuideState.Completed);
            LocalSetting.Set(SettingKeys.GuideState, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProgressIndex));
        }
    }

    /// <summary>
    /// 底部步骤指示器用：可见的只有 5 步（语言/文档/环境/数据文件夹/常用设置），
    /// 完成后停在最后一步。直接绑 <see cref="State"/> 会在 Completed 时越界。
    /// </summary>
    public int ProgressIndex => (int)Math.Min(State, (uint)GuideState.CommonSetting);

    public string NextOrCompleteButtonText { get; set => SetProperty(ref field, value); } = SH.ViewModelGuideActionNext;

    public bool IsNextOrCompleteButtonEnabled { get; set => SetProperty(ref field, value); } = true;

    /// <summary>
    /// 便携模式下数据目录跟着安装位置走（<c>InitializeDataDirectory</c> 优先认 exe 旁的 portable.json，
    /// 会忽略这里选定的目录），所以直接把入口关掉 —— 否则用户选完目录、程序重启后路径没变，
    /// 看起来像「设置没生效」。
    /// </summary>
    public bool IsSetDataFolderEnabled { get; set => SetProperty(ref field, value); } = !PortableLayout.IsEnabled;

    public partial CultureOptions CultureOptions { get; }

    public partial RuntimeOptions RuntimeOptions { get; }

    public partial AppOptions AppOptions { get; }

    // TODO: Replace with IObservableProperty
    public NameCultureInfoValue? SelectedCulture
    {
        get => field ??= Selection.Initialize(CultureOptions.Cultures, CultureOptions.CurrentCulture.Value);
        set
        {
            if (SetProperty(ref field, value) && value is not null)
            {
                CultureOptions.CurrentCulture.Value = value.Value;
                AppInstance.Restart(string.Empty);
            }
        }
    }

    // TODO: Replace with IObservableProperty
    public NameValue<Region>? SelectedRegion
    {
        get => field ??= Selection.Initialize(AppOptions.LazyRegions, AppOptions.Region.Value);
        set
        {
            if (SetProperty(ref field, value) && value is not null)
            {
                AppOptions.Region.Value = value.Value;
            }
        }
    }

    #region Agreement

    public bool IsTermOfServiceAgreed
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnAgreementStateChanged();
            }
        }
    }

    public bool IsPrivacyPolicyAgreed
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnAgreementStateChanged();
            }
        }
    }

    public bool IsIssueReportAgreed
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnAgreementStateChanged();
            }
        }
    }

    public bool IsOpenSourceLicenseAgreed
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnAgreementStateChanged();
            }
        }
    }

    public bool IsDownStreamAgreed
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnAgreementStateChanged();
            }
        }
    }

    #endregion

    /// <summary>
    /// 条款页的一次性勾选：一项等于下面五项。
    /// 逐条属性保留 —— 历史上「复制声明」那一项在中文布局里绑的是不存在的
    /// <c>IsAgreementCopyAgreed</c>，永远勾不上 <see cref="IsDownStreamAgreed"/>，
    /// 中文用户会被卡在条款页。合并成一项后这条死路一并消失。
    /// </summary>
    public bool IsAllAgreementsAccepted
    {
        get => IsTermOfServiceAgreed
            && IsPrivacyPolicyAgreed
            && IsIssueReportAgreed
            && IsOpenSourceLicenseAgreed
            && IsDownStreamAgreed;
        set
        {
            IsTermOfServiceAgreed = value;
            IsPrivacyPolicyAgreed = value;
            IsIssueReportAgreed = value;
            IsOpenSourceLicenseAgreed = value;
            IsDownStreamAgreed = value;
            OnPropertyChanged();
        }
    }

    private void OnAgreementStateChanged()
    {
        // 使用底层存储获取状态，避免访问 State getter 导致的副作用（State getter 会重置同意项）
        GuideState current = UnsafeLocalSetting.Get(SettingKeys.GuideState, GuideState.Language);
        if (current == GuideState.Document)
        {
            IsNextOrCompleteButtonEnabled = IsTermOfServiceAgreed
                && IsPrivacyPolicyAgreed
                && IsIssueReportAgreed
                && IsOpenSourceLicenseAgreed
                && IsDownStreamAgreed;
        }
        else
        {
            IsNextOrCompleteButtonEnabled = IsTermOfServiceAgreed && IsPrivacyPolicyAgreed && IsIssueReportAgreed && IsOpenSourceLicenseAgreed;
        }

        // 总勾选框是计算属性，子项被 State getter 重置时不会自动通知。
        OnPropertyChanged(nameof(IsAllAgreementsAccepted));
    }

    [Command("NextOrCompleteCommand")]
    private void NextOrComplete()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Increase guide state", "GuideViewModel.Command"));

        // 常用设置是最后一屏。再点一步就完成：Completed 本身是空分屏（没有界面），
        // 和旧版下载完成后的行为一致 —— 直接重启，让 AppActivation 走主界面。
        if (State >= (uint)GuideState.CommonSetting)
        {
            State = (uint)GuideState.Completed;

            try
            {
                AppInstance.Restart(string.Empty);
            }
            catch (COMException ex)
            {
                messenger.Send(InfoBarMessage.Error(ex));
            }

            return;
        }

        State += 1;
    }

    [Command("CreateGameLaunchShortcutCommand")]
    private void CreateGameLaunchShortcut()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Create game launch shortcut", "GuideViewModel.Command"));

        _ = shellLinkInterop.TryCreateGameLaunchShortcut()
            ? messenger.Send(InfoBarMessage.Success(SH.ViewModelSettingActionComplete))
            : messenger.Send(InfoBarMessage.Warning(SH.ViewModelSettingCreateDesktopShortcutFailed));
    }

    [Command("CreateDesktopShortcutCommand")]
    private void CreateDesktopShortcutForElevatedLaunchAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Create desktop shortcut for elevated launch", "SettingViewModel.Command"));

        _ = shellLinkInterop.TryCreateDesktopShortcut()
            ? messenger.Send(InfoBarMessage.Success(SH.ViewModelSettingActionComplete))
            : messenger.Send(InfoBarMessage.Warning(SH.ViewModelSettingCreateDesktopShortcutFailed));
    }

    [Command("SetDataFolderCommand")]
    private async Task SetDataFolderAsync()
    {
        // prevent reentrancy
        if (!IsSetDataFolderEnabled)
        {
            return;
        }

        IsSetDataFolderEnabled = false;
        try
        {
            SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Set data folder path", "GuideViewModel.Command"));

            SettingStorageSetDataFolderOperation operation = new()
            {
                FileSystemPickerInteraction = fileSystemPickerInteraction,
                ContentDialogFactory = contentDialogFactory,
                Messenger = messenger,
            };

            if (await operation.TryExecuteAsync().ConfigureAwait(false))
            {
                try
                {
                    AppInstance.Restart(string.Empty);
                }
                catch (COMException ex)
                {
                    messenger.Send(InfoBarMessage.Error(ex));
                }
            }
        }
        finally
        {
            IsSetDataFolderEnabled = true;
        }
    }

    protected override ValueTask<bool> LoadOverrideAsync(CancellationToken token)
    {
        // The upstream static resource size report is a cloud feature that does not exist for this
        // fork; the guide simply skips it instead of depending on an unavailable API.
        return ValueTask.FromResult(true);
    }
}