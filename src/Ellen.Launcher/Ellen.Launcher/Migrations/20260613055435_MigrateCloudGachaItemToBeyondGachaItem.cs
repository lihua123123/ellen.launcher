// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ellen.Launcher.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 修复从胡桃云下载祈愿记录时，BeyondGachaItem（千星奇域/UGC）被错误地存入 gacha_items 表的问题。
    /// 将现有的 UGC 记录从 gacha_items 表迁移到 beyond_gacha_items 表。
    /// </summary>
    public partial class MigrateCloudGachaItemToBeyondGachaItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO beyond_gacha_items (InnerId, ArchiveId, GachaType, QueryType, ItemId, Time, Id, ScheduleId, RankType, Uid, IsUp)
                SELECT InnerId, ArchiveId, GachaType, QueryType, ItemId, Time, Id, 0, 0, '', 0
                FROM gacha_items
                WHERE QueryType IN (1000, 2000)
                """);

            migrationBuilder.Sql("DELETE FROM gacha_items WHERE QueryType IN (1000, 2000)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO gacha_items (InnerId, ArchiveId, GachaType, QueryType, ItemId, Time, Id)
                SELECT InnerId, ArchiveId, GachaType, QueryType, ItemId, Time, Id
                FROM beyond_gacha_items
                WHERE QueryType IN (1000, 2000)
                """);

            migrationBuilder.Sql("DELETE FROM beyond_gacha_items WHERE QueryType IN (1000, 2000)");
        }
    }
}
