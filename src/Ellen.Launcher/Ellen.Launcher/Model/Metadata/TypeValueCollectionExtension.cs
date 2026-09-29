// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.ViewModel.Wiki;
using System.Collections.Immutable;

namespace Ellen.Launcher.Model.Metadata;

public static class TypeValueCollectionExtension
{
    extension(TypeValueCollection<FightProperty, GrowCurveType> collection)
    {
        public ImmutableArray<PropertyCurveValue> ToPropertyCurveValues(BaseValue baseValue)
        {
            return collection.Array.SelectAsArray(static (info, baseValue) => new PropertyCurveValue(info.Type, info.Value, baseValue), baseValue);
        }
    }
}