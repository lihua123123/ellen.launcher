// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Model.Combat;
using Ellen.Launcher.Service.EllenWiki;
using System.Collections.Frozen;
using System.IO;
using MetadataAvatar = Ellen.Launcher.Model.Metadata.Avatar.Avatar;
using MetadataSkill = Ellen.Launcher.Model.Metadata.Avatar.Skill;
using MetadataSkillDepot = Ellen.Launcher.Model.Metadata.Avatar.SkillDepot;

namespace Ellen.Launcher.Service.Combat;

/// <summary>
/// 「元素附着及产球」战斗备注的只读查询。
/// <para>
/// 数据来自 ellen.wiki 的 <c>GET /api/attachment</c>：优先使用
/// <see cref="EllenWikiDataService"/> 运行期同步的快照，缺失时回退到随应用发布的
/// <c>Resource/CombatNotes/AvatarCombatNotes.json</c>。该功能是纯附加信息：资源缺失或
/// 格式错误时只记录日志并返回空表，不影响角色资料页的既有功能。
/// </para>
/// </summary>
[Service(ServiceLifetime.Singleton, typeof(IAvatarCombatNoteService))]
public sealed partial class AvatarCombatNoteService : IAvatarCombatNoteService
{
    private const string ResourceRelativePath = "Resource/CombatNotes/AvatarCombatNotes.json";

    private readonly ILogger<AvatarCombatNoteService> logger;
    private readonly IEllenWikiDataService ellenWikiDataService;

    private readonly Lock gate = new();

    private FrozenDictionary<string, AvatarCombatNote>? notes;

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
    /// 运行期同步写入新快照后，丢弃已反序列化的结果，下次查询时重新加载。
    /// </summary>
    partial void PostConstruct(IServiceProvider serviceProvider)
    {
        ellenWikiDataService.AttachmentUpdated += OnAttachmentUpdated;
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
            CombatNoteTarget.Sprint => depot.Skills.ElementAtOrDefault(2),
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

    private void OnAttachmentUpdated(object? sender, EventArgs e)
    {
        Volatile.Write(ref notes, null);
    }

    private FrozenDictionary<string, AvatarCombatNote> Notes
    {
        get
        {
            FrozenDictionary<string, AvatarCombatNote>? current = Volatile.Read(ref notes);
            if (current is not null)
            {
                return current;
            }

            lock (gate)
            {
                current = notes;
                if (current is not null)
                {
                    return current;
                }

                current = LoadNotes();
                Volatile.Write(ref notes, current);
                return current;
            }
        }
    }

    private FrozenDictionary<string, AvatarCombatNote> LoadNotes()
    {
        // 运行期同步的快照优先；缺失或损坏时回退到随包快照。
        if (TryLoad(ellenWikiDataService.GetAttachmentSnapshotPath(), out FrozenDictionary<string, AvatarCombatNote> synced))
        {
            return synced;
        }

        if (TryLoad(InstalledLocation.GetAbsolutePath(ResourceRelativePath), out FrozenDictionary<string, AvatarCombatNote> bundled))
        {
            return bundled;
        }

        return FrozenDictionary<string, AvatarCombatNote>.Empty;
    }

    private bool TryLoad(string? path, out FrozenDictionary<string, AvatarCombatNote> notes)
    {
        notes = FrozenDictionary<string, AvatarCombatNote>.Empty;

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            EllenWikiAttachmentDataset? dataset = JsonSerializer.Deserialize(json, AvatarCombatNoteJsonContext.Default.EllenWikiAttachmentDataset);

            if (dataset is null)
            {
                logger.LogWarning("Combat notes file is empty: {Path}", path);
                return false;
            }

            notes = AvatarCombatNoteConverter.Convert(dataset)
                .Where(static note => !string.IsNullOrEmpty(note.Name))
                .ToFrozenDictionary(static note => note.Name);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Failed to load combat notes from {Path}", path);
            return false;
        }
    }
}
