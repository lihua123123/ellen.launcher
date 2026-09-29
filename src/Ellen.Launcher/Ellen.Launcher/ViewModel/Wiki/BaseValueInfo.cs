// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Model;
using Ellen.Launcher.Model.Metadata.Converter;
using Ellen.Launcher.Model.Primitive;
using System.Collections.Immutable;

namespace Ellen.Launcher.ViewModel.Wiki;

public sealed partial class BaseValueInfo : ObservableObject
{
    private readonly ImmutableArray<PropertyCurveValue> propValues;
    private readonly BaseValueInfoMetadataContext metadataContext;

    public BaseValueInfo(uint maxLevel, ImmutableArray<PropertyCurveValue> propValues, BaseValueInfoMetadataContext metadataContext)
    {
        this.propValues = propValues;
        this.metadataContext = metadataContext;

        MaxLevel = maxLevel;
        CurrentLevel = maxLevel;
    }

    public uint MaxLevel { get; }

    [ObservableProperty]
    public partial ImmutableArray<NameValue<string>> Values { get; set; } = [];

    public uint CurrentLevel
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(CurrentLevelFormatted));
                UpdateValues(value, Promoted);
            }
        }
    }

    public string CurrentLevelFormatted { get => LevelFormat.Format(CurrentLevel); }

    public bool Promoted
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                UpdateValues(CurrentLevel, value);
            }
        }
    }

    private void UpdateValues(Level level, bool promoted)
    {
        Values = BaseValueInfoConverter.ToNameValues(propValues, level, MaxLevel, promoted, metadataContext);
    }
}