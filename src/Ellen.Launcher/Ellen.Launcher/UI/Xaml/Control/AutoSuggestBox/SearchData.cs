// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Model.Metadata.Avatar;
using Ellen.Launcher.Model.Metadata.Monster;
using Ellen.Launcher.Model.Metadata.Weapon;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace Ellen.Launcher.UI.Xaml.Control.AutoSuggestBox;

public sealed partial class SearchData : ObservableObject
{
    private SearchData(FrozenDictionary<string, SearchToken> availableTokens)
    {
        AvailableTokens = availableTokens;
    }

    public FrozenDictionary<string, SearchToken> AvailableTokens { get; private set; }

    public ObservableCollection<SearchToken> FilterTokens { get; } = [];

    /// <summary>
    /// 切换游戏时原地替换候选 token，保持 <see cref="FilterTokens"/> 集合对象不变，
    /// 避免 AutoSuggestTokenBox 已经包装过 ItemsSource 后无法重新绑定。
    /// </summary>
    public void SetAvailableTokens(FrozenDictionary<string, SearchToken> availableTokens)
    {
        AvailableTokens = availableTokens;
        OnPropertyChanged(nameof(AvailableTokens));
    }

    [ObservableProperty]
    public partial string? FilterToken { get; set; }

    public static SearchData CreateForAvatarProperty()
    {
        return new(SearchTokens.GetForAvatarProperty());
    }

    public static SearchData CreateForZZZAvatarProperty()
    {
        return new(SearchTokens.GetForZZZAvatarProperty());
    }

    public static SearchData CreateForCultivation()
    {
        return new(SearchTokens.GetForCultivation());
    }

    public static SearchData CreateForWikiAvatar(ImmutableArray<Avatar> array)
    {
        return new(SearchTokens.GetForWikiAvatar(array));
    }

    public static SearchData CreateForWikiWeapon(ImmutableArray<Weapon> array)
    {
        return new(SearchTokens.GetForWikiWeapon(array));
    }

    public static SearchData CreateForWikiMonster(ImmutableArray<Monster> array)
    {
        return new(SearchTokens.GetForWikiMonster(array));
    }

    public static SearchData Create(FrozenDictionary<string, SearchToken> availableTokens)
    {
        return new(availableTokens);
    }
}