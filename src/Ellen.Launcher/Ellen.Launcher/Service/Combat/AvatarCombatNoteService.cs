// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Model.Combat;
using System.Collections.Frozen;
using System.IO;
using MetadataAvatar = Ellen.Launcher.Model.Metadata.Avatar.Avatar;
using MetadataSkill = Ellen.Launcher.Model.Metadata.Avatar.Skill;
using MetadataSkillDepot = Ellen.Launcher.Model.Metadata.Avatar.SkillDepot;

namespace Ellen.Launcher.Service.Combat;

/// <summary>
/// 从应用目录的 JSON 资源懒加载战斗备注。
/// <para>
/// 该功能是纯附加信息：资源缺失或格式错误时只记录日志并返回空表，
/// 不影响角色资料页的既有功能。
/// </para>
/// </summary>
[Service(ServiceLifetime.Singleton, typeof(IAvatarCombatNoteService))]
public sealed partial class AvatarCombatNoteService : IAvatarCombatNoteService
{
    private const string ResourceRelativePath = "Resource/CombatNotes/AvatarCombatNotes.json";

    private readonly ILogger<AvatarCombatNoteService> logger;

    private Lazy<FrozenDictionary<string, AvatarCombatNote>>? notes;

    [GeneratedConstructor]
    public partial AvatarCombatNoteService(IServiceProvider serviceProvider);

    public AvatarCombatNote? GetForAvatar(string avatarName)
    {
        return Notes.TryGetValue(avatarName, out AvatarCombatNote? note) ? note : null;
    }

    public void ApplyTo(MetadataAvatar avatar)
    {
        if (GetNoteFor(avatar) is not { } note)
        {
            return;
        }

        foreach (CombatNoteEntry entry in note.Entries)
        {
            ApplyEntry(avatar.SkillDepot, entry);
        }
    }

    /// <summary>
    /// 旅行者在游戏 metadata 中只有一个「旅行者」，而备注按其元素版本分开记录，
    /// 因此未命中名字时再按当前元素拼一次 key。
    /// </summary>
    private AvatarCombatNote? GetNoteFor(MetadataAvatar avatar)
    {
        if (GetForAvatar(avatar.Name) is { } note)
        {
            return note;
        }

        return avatar.Name is "旅行者"
            ? GetForAvatar($"旅行者（{avatar.FetterInfo.VisionBefore}）")
            : null;
    }

    private static void ApplyEntry(MetadataSkillDepot depot, CombatNoteEntry entry)
    {
        if (!Enum.TryParse(entry.Target, ignoreCase: true, out CombatNoteTarget target))
        {
            return;
        }

        MetadataSkill? skill = target switch
        {
            CombatNoteTarget.NormalAttack => depot.Skills.ElementAtOrDefault(0),
            CombatNoteTarget.ElementalSkill => depot.Skills.ElementAtOrDefault(1),
            CombatNoteTarget.ElementalBurst => depot.EnergySkill,
            CombatNoteTarget.Inherent1 => depot.Inherents.ElementAtOrDefault(0),
            CombatNoteTarget.Inherent2 => depot.Inherents.ElementAtOrDefault(1),
            CombatNoteTarget.Inherent3 => depot.Inherents.ElementAtOrDefault(2),
            CombatNoteTarget.Constellation1 => depot.Talents.ElementAtOrDefault(0),
            CombatNoteTarget.Constellation2 => depot.Talents.ElementAtOrDefault(1),
            CombatNoteTarget.Constellation3 => depot.Talents.ElementAtOrDefault(2),
            CombatNoteTarget.Constellation4 => depot.Talents.ElementAtOrDefault(3),
            CombatNoteTarget.Constellation5 => depot.Talents.ElementAtOrDefault(4),
            CombatNoteTarget.Constellation6 => depot.Talents.ElementAtOrDefault(5),
            _ => null,
        };

        if (skill is null || entry.Rows.Length is 0)
        {
            return;
        }

        skill.CombatNoteTable = new([.. entry.Rows]);
    }

    private FrozenDictionary<string, AvatarCombatNote> Notes
    {
        get => (notes ??= new(LoadNotes)).Value;
    }

    private FrozenDictionary<string, AvatarCombatNote> LoadNotes()
    {
        string path = InstalledLocation.GetAbsolutePath(ResourceRelativePath);
        if (!File.Exists(path))
        {
            logger.LogWarning("Combat notes file not found: {Path}", path);
            return FrozenDictionary<string, AvatarCombatNote>.Empty;
        }

        try
        {
            string json = File.ReadAllText(path);
            AvatarCombatNote[] parsed = JsonSerializer.Deserialize(json, AvatarCombatNoteJsonContext.Default.AvatarCombatNoteArray) ?? [];

            return parsed
                .Where(static note => !string.IsNullOrEmpty(note.Name))
                .ToFrozenDictionary(static note => note.Name);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Failed to load combat notes from {Path}", path);
            return FrozenDictionary<string, AvatarCombatNote>.Empty;
        }
    }
}
