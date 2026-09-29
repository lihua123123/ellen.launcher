// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Enka.Model;
using Ellen.Launcher.Web.Hoyolab;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ellen.Launcher.Model.Entity;

[Table("uid_profile_pictures")]
public sealed class UidProfilePicture
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid InnerId { get; set; }

    public string Uid { get; set; } = default!;

    public uint ProfilePictureId { get; set; }

    public uint AvatarId { get; set; }

    public uint CostumeId { get; set; }

    public DateTimeOffset RefreshTime { get; set; }

    public static UidProfilePicture From(PlayerUid uid, ProfilePicture profilePicture)
    {
        return new()
        {
            Uid = uid.ToString(),
            ProfilePictureId = profilePicture.Id,
            AvatarId = profilePicture.AvatarId,
            CostumeId = profilePicture.CostumeId,
            RefreshTime = DateTimeOffset.Now,
        };
    }
}
