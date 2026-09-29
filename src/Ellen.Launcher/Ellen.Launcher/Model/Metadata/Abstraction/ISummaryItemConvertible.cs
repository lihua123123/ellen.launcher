// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.ViewModel.GachaLog;

namespace Ellen.Launcher.Model.Metadata.Abstraction;

public interface ISummaryItemConvertible
{
    QualityType Quality { get; }

    SummaryItem ToSummaryItem(int lastPull, in DateTimeOffset time, bool isUp);
}