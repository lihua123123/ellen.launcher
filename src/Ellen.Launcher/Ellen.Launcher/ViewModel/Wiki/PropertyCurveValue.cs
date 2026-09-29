// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata;

namespace Ellen.Launcher.ViewModel.Wiki;

public sealed class PropertyCurveValue
{
    public PropertyCurveValue(FightProperty property, GrowCurveType type, float value)
    {
        Property = property;
        Type = type;
        Value = value;
    }

    public PropertyCurveValue(FightProperty property, GrowCurveType type, BaseValue baseValue)
        : this(property, type, baseValue.GetValue(property))
    {
    }

    public FightProperty Property { get; }

    public GrowCurveType Type { get; }

    public float Value { get; }
}