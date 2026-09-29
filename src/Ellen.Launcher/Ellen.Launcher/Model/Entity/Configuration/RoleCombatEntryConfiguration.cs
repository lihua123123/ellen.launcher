// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.RoleCombat;

namespace Ellen.Launcher.Model.Entity.Configuration;

public sealed class RoleCombatEntryConfiguration : IEntityTypeConfiguration<RoleCombatEntry>
{
    public void Configure(EntityTypeBuilder<RoleCombatEntry> builder)
    {
        builder.Property(e => e.RoleCombatData)
            .HasColumnType(SqliteTypeNames.Text)
            .HasConversion<JsonTextValueConverter<RoleCombatData>>();
    }
}