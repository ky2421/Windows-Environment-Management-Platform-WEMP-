using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WEMP.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "custom_games",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessName = table.Column<string>(type: "TEXT", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_games", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_custom_games_Name",
                table: "custom_games",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_custom_games_ProcessName",
                table: "custom_games",
                column: "ProcessName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "custom_games");
        }
    }
}
