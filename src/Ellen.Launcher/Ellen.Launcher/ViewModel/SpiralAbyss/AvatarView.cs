// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata.Avatar;

namespace Ellen.Launcher.ViewModel.SpiralAbyss;

public class AvatarView : INameIconSide<Uri>
{
    protected AvatarView(Avatar metaAvatar)
    {
        Name = metaAvatar.Name;
        Icon = Model.Metadata.Converter.AvatarIconConverter.IconNameToUri(metaAvatar.Icon);
        SideIcon = Model.Metadata.Converter.AvatarIconConverter.IconNameToUri(metaAvatar.SideIcon);
        Quality = metaAvatar.Quality;
    }

    public string Name { get; }

    public Uri Icon { get; }

    public Uri SideIcon { get; }

    public QualityType Quality { get; }

    public static AvatarView From(Avatar source)
    {
        return new(source);
    }
}