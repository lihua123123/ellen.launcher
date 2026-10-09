// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.
// Copyright (c) Millennium-Science-Technology-R-D-Inst. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Core.Setting;

public static class SettingKeys
{
    // Application
    public const string LowWindowsVersionWarningShown   = "Ellen::Application::LowWindowsVersionWarningShown";
    public const string DataDirectory                   = "Ellen::Application::DataFolderPath";
    public const string IsUnpackagedNotificationRegistered = "Ellen::Application::Notification::Unpackaged::Registered";
    public const string OverrideElevationRequirement    = "Ellen::Application::Elevation::Override";
    public const string AutoRestartAsAdmin              = "Ellen::Application::Elevation::AutoRestartAsAdmin";
    public const string StartupEnabled                  = "Ellen::Application::Startup::Enabled";
    public const string StartupAsAdminEnabled           = "Ellen::Application::Startup::AsAdmin::Enabled";
    public const string LaunchTimes                     = "Ellen::Application::LaunchTimes";
    public const string PreviousDataDirectoryToDelete   = "Ellen::Application::PreviousDataFolderToDelete";
    public const string LastVersion                     = "Ellen::Application::Update::LastVersion";
    public const string AlwaysIsFirstRunAfterUpdate     = "Ellen::Application::Update::LastVersion::TreatAsFirstRun";
    public const string OverrideUpdateVersionComparison = "Ellen::Application::Update::VersionComparison::Override";
    public const string MirrorChyanCdk = "Ellen::Application::Update::MirrorChyanCdk";

    // Globalization
    public const string FirstDayOfWeek               = "Ellen::Globalization::FirstDayOfWeek";
    public const string PrimaryLanguage              = "Ellen::Globalization::PrimaryLanguage";
    public const string AnnouncementRegion           = "Ellen::Globalization::Region::Announcement";

    // UI
    public const string BackgroundImageType          = "Ellen::UI::BackgroundImage::Type";
    public const string BackgroundImagePath          = "Ellen::UI::BackgroundImage::Path";
    public const string BackgroundMediaType          = "Ellen::UI::BackgroundMedia::Type";
    public const string BackgroundMediaPath          = "Ellen::UI::BackgroundMedia::Path";
    public const string IsBackgroundMediaMuted       = "Ellen::UI::BackgroundMedia::IsMuted";
    public const string IsBackgroundMediaLooping     = "Ellen::UI::BackgroundMedia::IsLooping";
    public const string ElementTheme                 = "Ellen::UI::ElementTheme";
    public const string SystemBackdropType           = "Ellen::UI::SystemBackdropType";
    public const string GuideState                   = "Ellen::UI::Windowing::GuideWindow::State::1.17";
    public const string IsNavPaneOpen                = "Ellen::UI::Windowing::MainWindow::NavigationView::IsPaneOpen";

    // HomeCard
    public const string HomeCardAchievementOrder           = "Ellen::UI::Home::Card::Achievement::Order";
    public const string IsHomeCardAchievementPresented     = "Ellen::UI::Home::Card::Achievement::Presented";
    public const string HomeCardCalendarOrder              = "Ellen::UI::Home::Card::Calendar::Order";
    public const string IsHomeCardCalendarPresented        = "Ellen::UI::Home::Card::Calendar::Presented";
    public const string CalendarServerTimeZoneOffset       = "Ellen::UI::Home::Card::Calendar::ServerTimeZoneOffset";
    public const string HomeCardDailyNoteOrder             = "Ellen::UI::Home::Card::DailyNote::Order";
    public const string IsHomeCardDailyNotePresented       = "Ellen::UI::Home::Card::DailyNote::Presented";
    public const string HomeCardGachaStatisticsOrder       = "Ellen::UI::Home::Card::GachaStatistics::Order";
    public const string IsHomeCardGachaStatisticsPresented = "Ellen::UI::Home::Card::GachaStatistics::Presented";
    public const string HomeCardSignInOrder                = "Ellen::UI::Home::Card::SignIn::Order";
    public const string IsHomeCardSignInPresented          = "Ellen::UI::Home::Card::SignIn::Presented";
    public const string HomeCardTravelersDiaryOrder        = "Ellen::UI::Home::Card::TravelersDiary::Order";
    public const string IsHomeCardTravelersDiaryPresented  = "Ellen::UI::Home::Card::TravelersDiary::Presented";
    public const string HomeAnnouncementActPreviewEnabled  = "Ellen::UI::Home::Announcement::ActPreviewEnabled";
    public const string IsHomeAnnouncementActivityCalendarPresented = "Ellen::UI::Home::Announcement::ActivityCalendar::Presented";

    // HotKey
    public const string LowLevelKeyboardWebView2VideoPlayPause   = "Ellen::HotKey::LowLevel::WebView2::Video::PlayPause";
    public const string LowLevelKeyboardWebView2VideoFastForward = "Ellen::HotKey::LowLevel::WebView2::Video::FastForward";
    public const string LowLevelKeyboardWebView2VideoRewind      = "Ellen::HotKey::LowLevel::WebView2::Video::Rewind";
    public const string LowLevelKeyboardWebView2Hide             = "Ellen::HotKey::LowLevel::WebView2::Hide";
    public const string LowLevelKeyboardOverlayHide              = "Ellen::HotKey::LowLevel::Overlay::Hide";

    // AvatarProperty
    public const string AvatarPropertySortDescriptionKind = "Ellen::AvatarProperty::SortDescriptionKind";

    // Cultivation
    public const string CultivationAvatarLevelCurrent           = "Ellen::Cultivation::Avatar::Level::Current";
    public const string CultivationAvatarLevelTarget            = "Ellen::Cultivation::Avatar::Level::Target";
    public const string CultivationAvatarSkillACurrent          = "Ellen::Cultivation::Avatar::SkillA::Current";
    public const string CultivationAvatarSkillATarget           = "Ellen::Cultivation::Avatar::SkillA::Target";
    public const string CultivationAvatarSkillECurrent          = "Ellen::Cultivation::Avatar::SkillE::Current";
    public const string CultivationAvatarSkillETarget           = "Ellen::Cultivation::Avatar::SkillE::Target";
    public const string CultivationAvatarSkillQCurrent          = "Ellen::Cultivation::Avatar::SkillQ::Current";
    public const string CultivationAvatarSkillQTarget           = "Ellen::Cultivation::Avatar::SkillQ::Target";
    public const string CultivationWeapon70LevelCurrent         = "Ellen::Cultivation::Weapon70::Level::Current";
    public const string CultivationWeapon70LevelTarget          = "Ellen::Cultivation::Weapon70::Level::Target";
    public const string CultivationWeapon90LevelCurrent         = "Ellen::Cultivation::Weapon90::Level::Current";
    public const string CultivationWeapon90LevelTarget          = "Ellen::Cultivation::Weapon90::Level::Target";
    public const string ResinStatisticsSelectedDropDistribution = "Ellen::Cultivation::ResinStatistics::DropDistribution";

    // GachaLog
    public const string IsEmptyOverviewVisible      = "Ellen::GachaLog::Overview::EmptyVisible";
    public const string IsEmptyHistoryWishVisible   = "Ellen::GachaLog::HistoryWish::EmptyVisible";
    public const string IsUnobtainedWishItemVisible = "Ellen::GachaLog::UnobtainedItem::Visible";

    // DailyNote
    public const string DailyNoteIsAutoRefreshEnabled  = "Ellen::DailyNote::AutoRefresh::Enabled";
    public const string DailyNoteRefreshSeconds        = "Ellen::DailyNote::RefreshSeconds";
    public const string DailyNoteReminderNotify        = "Ellen::DailyNote::ReminderNotify";
    public const string DailyNoteSilentWhenPlayingGame = "Ellen::DailyNote::SilentWhenPlayingGame";
    public const string DailyNoteWebhookUrl            = "Ellen::DailyNote::Webhook::Url";

    // Geetest
    public const string GeetestCustomCompositeUrl = "Ellen::Geetest::CustomCompositeUrl";

    // Game
    public const string LaunchAspectRatios                               = "Ellen::Game::CommandLine::AspectRatios";
    public const string LaunchUsingHoyolabAccount                        = "Ellen::Game::CommandLine::AuthTicket";
    public const string LaunchIsBorderless                               = "Ellen::Game::CommandLine::Borderless";
    public const string LaunchAreCommandLineArgumentsEnabled             = "Ellen::Game::CommandLine::Enabled";
    public const string LaunchIsExclusive                                = "Ellen::Game::CommandLine::Exclusive";
    public const string LaunchIsFullScreen                               = "Ellen::Game::CommandLine::FullScreen";
    public const string LaunchMonitor                                    = "Ellen::Game::CommandLine::Monitor";
    public const string LaunchIsMonitorEnabled                           = "Ellen::Game::CommandLine::Monitor::Enabled";
    public const string LaunchPlatformType                               = "Ellen::Game::CommandLine::PlatformType";
    public const string LaunchIsPlatformTypeEnabled                      = "Ellen::Game::CommandLine::PlatformType::Enabled";
    public const string LaunchScreenHeight                               = "Ellen::Game::CommandLine::ScreenHeight";
    public const string LaunchIsScreenHeightEnabled                      = "Ellen::Game::CommandLine::ScreenHeight::Enabled";
    public const string LaunchScreenWidth                                = "Ellen::Game::CommandLine::ScreenWidth";
    public const string LaunchIsScreenWidthEnabled                       = "Ellen::Game::CommandLine::ScreenWidth::Enabled";
    public const string LaunchUsingStarwardPlayTimeStatistics            = "Ellen::Game::InterProcess::Starward::PlayTimeStatistics";
    public const string LaunchUsingBetterGenshinImpactAutomation         = "Ellen::Game::InterProcess::BetterGenshinImpact::Automation";
    public const string LaunchBetterGenshinImpactPath                   = "Ellen::Game::InterProcess::BetterGenshinImpact::Path";
    public const string LaunchDisableShowDamageText                      = "Ellen::Game::Island::DamageText::Show";
    public const string LaunchIsIslandEnabled                            = "Ellen::Game::Island::Enabled";
    public const string LaunchDisableEventCameraMove                     = "Ellen::Game::Island::Event::CameraMove::Disabled";
    public const string LaunchTargetFov                                  = "Ellen::Game::Island::FieldOfView";
    public const string LaunchIsSetFieldOfViewEnabled                    = "Ellen::Game::Island::FieldOfView::Enabled";
    public const string LaunchDisablePlayerPerspective                   = "Ellen::Game::Island::FieldOfView::DisablePlayerPerspective";
    public const string LaunchDisablePlayerDiveMosaic                    = "Ellen::Game::Island::FieldOfView::DisablePlayerDiveMosaic";
    public const string LaunchDisableFogRendering                        = "Ellen::Game::Island::FieldOfView::DisableFogRendering";
    public const string LaunchIsSetTargetFrameRateEnabled                = "Ellen::Game::Island::FrameRate::Enabled";
    public const string LaunchTargetFps                                  = "Ellen::Game::Island::FrameRate";
    public const string LaunchUsingTouchScreen                           = "Ellen::Game::Island::InputDevice::TouchScreen";
    public const string LaunchGamepadHotSwitchEnabled                    = "Ellen::Game::Island::InputDevice::GamepadHotSwitchEnabled";
    public const string LaunchRemoveOpenTeamProgress                     = "Ellen::Game::Island::OpenTeamProgress::Remove";
    public const string LaunchEnableInLevelClockPageSpeedUp              = "Ellen::Game::Island::InLevelClockPage::SpeedUp";
    public const string LaunchHideQuestBanner                            = "Ellen::Game::Island::QuestBanner::Hide";
    public const string LaunchResinListItemId000106Allowed               = "Ellen::Game::Island::Reward::000106";
    public const string LaunchResinListItemId000201Allowed               = "Ellen::Game::Island::Reward::000201";
    public const string LaunchResinListItemId107009Allowed               = "Ellen::Game::Island::Reward::107009";
    public const string LaunchResinListItemId107012Allowed               = "Ellen::Game::Island::Reward::107012";
    public const string LaunchResinListItemId220007Allowed               = "Ellen::Game::Island::Reward::220007";
    public const string LaunchDisplayPaimon                              = "Ellen::Game::Island::DisplayPaimon";
    public const string LaunchDebugMode                                  = "Ellen::Game::Island::DebugMode";
    public const string LaunchHidePlayerInfo                             = "Ellen::Game::Island::HidePlayerInfo";
    public const string LaunchHideGrass                                  = "Ellen::Game::Island::HideGrass";
    public const string LaunchRedirectCombineEntry                       = "Ellen::Game::Island::Synthesis::Redirect";
    public const string LaunchCombineMenuHotkey                          = "Ellen::Game::Island::Synthesis::Hotkey";
    public const string LaunchWeakMapCheck                               = "Ellen::Game::Island::OpenMap::WeakCheck";
    public const string LaunchFastSkipTalk                               = "Ellen::Game::Island::Dialogue::SkipCooldown::Remove";
    public const string LaunchUsingOverlay                               = "Ellen::Game::Overlay";
    public const string LaunchOverridePackageConvertDirectoryPermissions = "Ellen::Game::Package::Convert::Directory::Permissions::Override";
    public const string DownloadSpeedLimitPerSecondInKiloByte            = "Ellen::Game::Package::DownloadSpeedLimitPerSecondInKiloByte";
    public const string OverridePhysicalDriverType                       = "Ellen::Game::Package::PhysicalDriver::Type::Override";
    public const string PhysicalDriverIsAlwaysSolidState                 = "Ellen::Game::Package::PhysicalDriver::Type::IsSolidState";
    public const string LaunchGamePath                                   = "Ellen::Game::Path";
    public const string LaunchGamePathEntries                            = "Ellen::Game::Path::Entries";
    public const string LaunchIsWindowsHDREnabled                        = "Ellen::Game::Registry::WindowsHDR::Enabled";

    // Advanced Start
    public const string LaunchAdvancedStartProgramPath                   = "Ellen::Game::AdvancedStart::ProgramPath";
    public const string LaunchAdvancedStartDelayedOnAdvancedStart        = "Ellen::Game::AdvancedStart::DelayedOnAdvancedStart";
    public const string LaunchAdvancedStartDelayedOnGameLaunch           = "Ellen::Game::AdvancedStart::DelayedOnGameLaunch";

    // Web
    public const string AlphaBuildUseCnPatchEndpoint            = "Ellen::Web::AlphaBuild::Endpoint::UseCNPatch";
    public const string AlphaBuildUseFjPatchEndpoint            = "Ellen::Web::AlphaBuild::Endpoint::UseFJPatch";
    public const string StaticResourceImageQuality              = "Ellen::Web::StaticResource::ImageQuality";
    public const string StaticResourceImageArchive              = "Ellen::Web::StaticResource::ImageArchive";
    public const string BridgeShareSaveType                     = "Ellen::Web::WebView::BridgeShare::SaveType";
    public const string CompactWebView2WindowInactiveOpacity    = "Ellen::Web::WebView::Compact::InactiveOpacity";
    public const string CompactWebView2WindowPreviousSourceUrl  = "Ellen::Web::WebView::Compact::PreviousSourceUrl";
    public const string WebView2VideoFastForwardOrRewindSeconds = "Ellen::Web::WebView::Video::FastForwardOrRewind::Seconds";
    public const string ServerDomainMode                        = "Ellen::Web::ServerDomainMode";

    // Game - Launch
    public const string LaunchIsCloseWhenGameStarted            = "Ellen::Game::Launch::CloseWhenGameStarted";

    // Auto Sign-In
}