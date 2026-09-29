// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.Avatar;

namespace Ellen.Launcher.Model.Entity.Configuration;

public sealed class AvatarInfoConfiguration : IEntityTypeConfiguration<AvatarInfo>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AvatarInfo> builder)
    {
        builder.Property(e => e.Info2)
            .HasColumnType(SqliteTypeNames.Text)
            .HasConversion<JsonTextValueConverter<DetailedCharacter>>();
    }
}