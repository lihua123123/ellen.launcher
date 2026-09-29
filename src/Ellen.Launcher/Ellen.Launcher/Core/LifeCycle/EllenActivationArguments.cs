// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Windows.AppLifecycle;

namespace Ellen.Launcher.Core.LifeCycle;

public sealed class EllenActivationArguments
{
    public bool IsRedirectTo { get; set; }

    public EllenActivationKind Kind { get; set; }

    public Uri? ProtocolActivatedUri { get; set; }

    public string? LaunchActivatedArguments { get; set; }

    public IReadOnlyDictionary<string, string>? AppNotificationActivatedArguments { get; set; }

    public IReadOnlyDictionary<string, string>? AppNotificationActivatedUserInput { get; set; }

    public static EllenActivationArguments FromAppActivationArguments(AppActivationArguments args, bool isRedirected = false)
    {
        EllenActivationArguments result = new()
        {
            IsRedirectTo = isRedirected,
        };

        switch (args.Kind)
        {
            case ExtendedActivationKind.Launch:
                {
                    result.Kind = EllenActivationKind.Launch;
                    if (args.TryGetLaunchActivatedArguments(out string? arguments))
                    {
                        result.LaunchActivatedArguments = arguments;
                    }

                    break;
                }

            case ExtendedActivationKind.Protocol:
                {
                    result.Kind = EllenActivationKind.Protocol;
                    if (args.TryGetProtocolActivatedUri(out Uri? uri))
                    {
                        result.ProtocolActivatedUri = uri;
                    }

                    break;
                }

            case ExtendedActivationKind.AppNotification:
                {
                    result.Kind = EllenActivationKind.AppNotification;
                    if (args.TryGetAppNotificationActivatedArguments(out string? argument, out IDictionary<string, string>? arguments, out IDictionary<string, string>? userInput))
                    {
                        result.LaunchActivatedArguments = argument;
                        result.AppNotificationActivatedArguments = arguments.AsReadOnly();
                        result.AppNotificationActivatedUserInput = userInput.AsReadOnly();
                    }

                    break;
                }
        }

        return result;
    }
}