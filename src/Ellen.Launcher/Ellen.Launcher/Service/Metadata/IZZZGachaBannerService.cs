// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Metadata.ZZZ;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Metadata;

/// <summary>
/// 绝区零调频档期查询（随包快照，运行期不发起网络请求）。
/// </summary>
public interface IZZZGachaBannerService
{
    ImmutableArray<ZZZGachaBanner> Banners { get; }
}
