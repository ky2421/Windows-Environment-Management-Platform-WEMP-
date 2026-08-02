using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WEMP.Migrations
{
    /// <inheritdoc />
    public partial class AddBackup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "backup_tasks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    SourcePath = table.Column<string>(type: "TEXT", nullable: false),
                    DestinationPath = table.Column<string>(type: "TEXT", nullable: false),
                    Mode = table.Column<string>(type: "TEXT", nullable: false),
                    IncludePatterns = table.Column<string>(type: "TEXT", nullable: true),
                    ExcludePatterns = table.Column<string>(type: "TEXT", nullable: true),
                    AutoBackup = table.Column<bool>(type: "INTEGER", nullable: false),
                    AutoIntervalHours = table.Column<int>(type: "INTEGER", nullable: false),
                    LastBackupAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_tasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "backup_records",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TaskId = table.Column<long>(type: "INTEGER", nullable: false),
                    BackupType = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    FileCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: true),
                    Error = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_backup_records_backup_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "backup_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "backup_file_entries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RecordId = table.Column<long>(type: "INTEGER", nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_file_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_backup_file_entries_backup_records_RecordId",
                        column: x => x.RecordId,
                        principalTable: "backup_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_backup_file_entries_RecordId_RelativePath",
                table: "backup_file_entries",
                columns: new[] { "RecordId", "RelativePath" });

            migrationBuilder.CreateIndex(
                name: "IX_backup_records_TaskId_StartedAt",
                table: "backup_records",
                columns: new[] { "TaskId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_backup_tasks_Enabled",
                table: "backup_tasks",
                column: "Enabled");

            migrationBuilder.CreateIndex(
                name: "IX_backup_tasks_UpdatedAt",
                table: "backup_tasks",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "backup_file_entries");

            migrationBuilder.DropTable(
                name: "backup_records");

            migrationBuilder.DropTable(
                name: "backup_tasks");
        }
    }
}
