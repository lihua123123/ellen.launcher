using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ellen.Launcher.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUidInBeyondGachaItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RankType",
                table: "beyond_gacha_items");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "beyond_gacha_items");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RankType",
                table: "beyond_gacha_items",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Uid",
                table: "beyond_gacha_items",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
