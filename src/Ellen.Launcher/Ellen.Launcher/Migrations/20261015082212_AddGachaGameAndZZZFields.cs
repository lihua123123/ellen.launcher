using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ellen.Launcher.Migrations
{
    /// <inheritdoc />
    public partial class AddGachaGameAndZZZFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "gacha_items",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Quality",
                table: "gacha_items",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Game",
                table: "gacha_archives",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "gacha_items");

            migrationBuilder.DropColumn(
                name: "Quality",
                table: "gacha_items");

            migrationBuilder.DropColumn(
                name: "Game",
                table: "gacha_archives");
        }
    }
}
