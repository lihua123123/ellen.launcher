// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

// CommunityToolkit
global using CommunityToolkit.Mvvm.DependencyInjection;
global using CommunityToolkit.Mvvm.Messaging;

// Microsoft
global using Microsoft;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;

// Sentry
global using Sentry;

// Ellen.Launcher
global using Ellen.Launcher.Core.Annotation;
global using Ellen.Launcher.Core.DependencyInjection;
global using Ellen.Launcher.Core.DependencyInjection.Annotation;
global using Ellen.Launcher.Core.Threading;
global using Ellen.Launcher.Extension;
global using Ellen.Launcher.Resource.Localization;

// Runtime
global using System;
global using System.Collections.Generic;
global using System.ComponentModel;
global using System.Diagnostics.CodeAnalysis;
global using System.Linq;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows.Input;

// Ellen.Launcher
global using FromKeyed = Ellen.Launcher.Core.DependencyInjection.Annotation.FromKeyedServicesAttribute;
global using Void = Ellen.Launcher.Core.Void;