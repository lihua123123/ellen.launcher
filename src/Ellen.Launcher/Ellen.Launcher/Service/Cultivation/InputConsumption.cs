// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity.Primitive;
using Ellen.Launcher.Service.Cultivation.Consumption;
using System.Collections.Immutable;
using CalculateItem = Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate.Item;

namespace Ellen.Launcher.Service.Cultivation;

public sealed class InputConsumption
{
    public required CultivateType Type { get; init; }

    public required uint ItemId { get; init; }

    public required ImmutableArray<CalculateItem> Items { get; init; }

    public required LevelInformation LevelInformation { get; init; }

    public required ConsumptionSaveStrategyKind Strategy { get; init; }
}