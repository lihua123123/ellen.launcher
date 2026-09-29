// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Core.LifeCycle;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Service.DailyNote.NotifySuppression;
using Ellen.Launcher.Service.Game;
using Ellen.Launcher.Service.Notification;

namespace Ellen.Launcher.Service.DailyNote;

[Service(ServiceLifetime.Singleton)]
public sealed partial class DailyNoteNotificationOperation
{
    private const string ToastAttributionUnknown = "Unknown UID";

    private readonly ITaskContext taskContext;
    private readonly DailyNoteOptions options;
    private readonly IMessenger messenger;
    private readonly IToastNotificationService toastNotificationService;

    [GeneratedConstructor]
    public partial DailyNoteNotificationOperation(IServiceProvider serviceProvider);

    public async ValueTask SendAsync(DailyNoteEntry entry)
    {
        if (entry.DailyNote is null)
        {
            return;
        }

        // This must happen before checking IsAppNotificationEnabled.
        // Always perform check to update dot visibility.
        NotifySuppressionInvoker.Check(entry, out List<DailyNoteNotifyInfo> notifyInfos);

        if (notifyInfos.Count <= 0)
        {
            return;
        }

        if (!EllenRuntime.IsAppNotificationEnabled)
        {
            return;
        }

        string attribution = entry.UserGameRole?.ToString() ?? ToastAttributionUnknown;

        string reminder = options.IsReminderNotification.Value ? @"scenario=""reminder""" : string.Empty;
        string content;

        if (notifyInfos.Count > 2)
        {
            string adaptiveSubgroups = string.Join(string.Empty, notifyInfos.Select(info => $"""
                <subgroup>
                    <text hint-align="center">{info.AdaptiveHint}</text>
                    <text hint-style="captionSubtle" hint-align="center">{info.Title}</text>
                </subgroup>
            """));

            content = $"""
                <text>{SH.ServiceDailyNoteNotifierMultiValueReached}</text>
                <group>
                {adaptiveSubgroups}
                </group>
                """;
        }
        else
        {
            content = string.Join(string.Empty, notifyInfos.Select(info => $"""
                <text>{info.Hint}</text>
            """));
        }

        string rawXml = $"""
            <toast {reminder}>
                <header title="{SH.ServiceDailyNoteNotifierTitle}" id="DAILYNOTE" arguments="DAILYNOTE"/>

                <visual>
                    <binding template="ToastGeneric">
                        {content}
                        <text placement="attribution">{attribution}</text>
                    </binding>
                </visual>
                <actions>
                    <action activationType="background" content="{SH.ServiceDailyNoteNotifierActionLaunchGameButton}" arguments="{AppActivation.Action}={AppActivation.LaunchGame};{AppActivation.Uid}={entry.Uid}"/>
                    <action activationType="system" content="{SH.ServiceDailyNoteNotifierActionLaunchGameDismiss}" arguments="dismiss"/>
                </actions>
            </toast>
            """;
        bool suppressDisplay = options.IsSilentWhenPlayingGame.Value && await GameLifeCycle.IsGameRunningAsync(taskContext).ConfigureAwait(false);

        await taskContext.SwitchToMainThreadAsync();
        try
        {
            toastNotificationService.Show(rawXml, suppressDisplay);
        }
        catch (Exception ex)
        {
            ExceptionAttachment.SetAttachment(ex, "RawXml", rawXml);
            messenger.Send(InfoBarMessage.Error(SH.ServiceDailyNoteNotificationSendExceptionTitle, ex));
        }
    }
}