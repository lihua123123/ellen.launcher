// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Collections.Immutable;

namespace Ellen.Launcher.Model.Combat;

/// <summary>
/// 贴在某个技能 / 固有天赋 / 命座文本下方的备注表格。
/// 没有任何行时不应创建该对象，UI 以 <see langword="null"/> 表示不渲染。
/// </summary>
public sealed class CombatNoteTable
{
    public CombatNoteTable(ImmutableArray<CombatNoteRow> rows)
    {
        List<CombatNoteRowView> views = [.. rows.Select(static row => new CombatNoteRowView(row))];
        Rows = [.. views];
    }

    public ImmutableArray<CombatNoteRowView> Rows { get; }
}
