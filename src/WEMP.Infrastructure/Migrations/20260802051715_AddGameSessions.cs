using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WEMP.Migrations
{
    /// <inheritdoc />
    public partial class AddGameSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_sessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameName = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessName = table.Column<string>(type: "TEXT", nullable: true),
                    ProcessId = table.Column<int>(type: "INTEGER", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DurationSeconds = table.Column<long>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_sessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_game_sessions_ProcessName",
                table: "game_sessions",
                column: "ProcessName");

            migrationBuilder.CreateIndex(
                name: "IX_game_sessions_StartedAt",
                table: "game_sessions",
                column: "StartedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_sessions");
        }
    }
}
