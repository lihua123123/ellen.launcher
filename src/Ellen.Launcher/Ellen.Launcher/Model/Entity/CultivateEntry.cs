// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity.Abstraction;
using Ellen.Launcher.Model.Entity.Primitive;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ellen.Launcher.Model.Entity;

[Table("cultivate_entries")]
public sealed class CultivateEntry : IAppDbEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid InnerId { get; set; }

    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public CultivateProject Project { get; set; } = default!;

    public CultivateEntryLevelInformation? LevelInformation { get; set; }

    public CultivateType Type { get; set; }

    public uint Id { get; set; }

    public static CultivateEntry From(Guid projectId, CultivateType type, uint id)
    {
        return new()
        {
            ProjectId = projectId,
            Type = type,
            Id = id,
        };
    }
}