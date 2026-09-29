// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DataTransfer;
using Ellen.Launcher.Core.Logging;
using Ellen.Launcher.Service.Notification;

namespace Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect;

public partial class PackageSegment
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = default!;

    [JsonPropertyName("md5")]
    public string MD5 { get; set; } = default!;

    [JsonPropertyName("size")]
    public long Size { get; set; } = default!;

    [JsonPropertyName("decompressed_size")]
    public long DecompressedSize { get; set; } = default!;

    [JsonIgnore]
    public string DisplayName { get => System.IO.Path.GetFileName(Url); }

    [Command("CopyPathCommand")]
    private async Task CopyPathToClipboardAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Copy url to ClipBoard", "PackageSegment.Command"));

        IServiceProvider serviceProvider = Ioc.Default;
        await serviceProvider.GetRequiredService<IClipboardProvider>().SetTextAsync(Url).ConfigureAwait(false);
        serviceProvider.GetRequiredService<IMessenger>().Send(InfoBarMessage.Success(SH.WebGameResourcePathCopySucceed));
    }
}