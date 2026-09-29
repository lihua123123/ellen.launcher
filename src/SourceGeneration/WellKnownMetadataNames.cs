// Copyright (c) DGP Studio. All rights reserved.
// Modified by ky3-studio.
// Licensed under the MIT license.

namespace Ellen.Launcher.SourceGeneration;

internal static class WellKnownMetadataNames
{
    public const string CommandAttribute = "Ellen.Launcher.Core.Annotation.CommandAttribute";
    public const string GeneratedConstructorAttribute = "Ellen.Launcher.Core.Annotation.GeneratedConstructorAttribute";
    public const string BindableCustomPropertyProviderAttribute = "Ellen.Launcher.Core.Annotation.BindableCustomPropertyProviderAttribute";
    public const string DependencyPropertyAttributeT = "Ellen.Launcher.Core.Annotation.DependencyPropertyAttribute`1";
    public const string FieldAccessAttribute = "Ellen.Launcher.Core.Annotation.FieldAccessorAttribute";

    public const string HttpClient = "System.Net.Http.HttpClient";
    public const string HttpClientAttribute = "Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient.HttpClientAttribute";
    public const string PrimaryHttpMessageHandlerAttribute = "Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient.PrimaryHttpMessageHandlerAttribute";
    public const string HttpClientConfiguration = "Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient.HttpClientConfiguration.";

    public const string IServiceProvider = "System.IServiceProvider";
    public const string ServiceLifetimeSingleton = "Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton";
    public const string ServiceLifetimeScoped = "Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped";
    public const string ServiceLifetimeTransient = "Microsoft.Extensions.DependencyInjection.ServiceLifetime.Transient";

    public const string ServiceAttribute = "Ellen.Launcher.Core.DependencyInjection.Annotation.ServiceAttribute";
    public const string FromKeyedServicesAttribute = "Ellen.Launcher.Core.DependencyInjection.Annotation.FromKeyedServicesAttribute";

    public const string ExtendedEnumAttribute = "Ellen.Launcher.Resource.Localization.ExtendedEnumAttribute";
    public const string LocalizationKeyAttribute = "Ellen.Launcher.Resource.Localization.LocalizationKeyAttribute";
    public const string InterceptsLocationAttribute = "System.Runtime.CompilerServices.InterceptsLocationAttribute";
}