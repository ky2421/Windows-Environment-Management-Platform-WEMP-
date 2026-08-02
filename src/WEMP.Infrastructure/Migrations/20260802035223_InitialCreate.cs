using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WEMP.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_settings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    ValueType = table.Column<string>(type: "TEXT", nullable: false),
                    Module = table.Column<string>(type: "TEXT", nullable: false),
                    IsProtected = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Module = table.Column<string>(type: "TEXT", nullable: false),
                    Level = table.Column<string>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    Target = table.Column<string>(type: "TEXT", nullable: true),
                    Message = table.Column<string>(type: "TEXT", nullable: true),
                    DetailJson = table.Column<string>(type: "TEXT", nullable: true),
                    User = table.Column<string>(type: "TEXT", nullable: true),
                    Result = table.Column<string>(type: "TEXT", nullable: true),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "env_templates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TemplateKey = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Version = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    BuiltIn = table.Column<bool>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_env_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "installed_software",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Publisher = table.Column<string>(type: "TEXT", nullable: true),
                    Version = table.Column<string>(type: "TEXT", nullable: false),
                    InstallDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    PackageId = table.Column<string>(type: "TEXT", nullable: true),
                    InstallLocation = table.Column<string>(type: "TEXT", nullable: true),
                    UninstallKey = table.Column<string>(type: "TEXT", nullable: true),
                    IsManaged = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsWempInstalled = table.Column<bool>(type: "INTEGER", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_installed_software", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "log_anomalies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DetectedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RuleCode = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    EvidenceJson = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_log_anomalies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "optimization_items",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Principle = table.Column<string>(type: "TEXT", nullable: true),
                    Risk = table.Column<string>(type: "TEXT", nullable: true),
                    Recommendation = table.Column<string>(type: "TEXT", nullable: false),
                    IsRecoverable = table.Column<bool>(type: "INTEGER", nullable: false),
                    TargetJson = table.Column<string>(type: "TEXT", nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    KbVersion = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_optimization_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "optimization_records",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemCode = table.Column<string>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    Trigger = table.Column<string>(type: "TEXT", nullable: false),
                    Result = table.Column<string>(type: "TEXT", nullable: false),
                    BeforeJson = table.Column<string>(type: "TEXT", nullable: true),
                    AfterJson = table.Column<string>(type: "TEXT", nullable: true),
                    Detail = table.Column<string>(type: "TEXT", nullable: true),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                    RestorePointId = table.Column<long>(type: "INTEGER", nullable: true),
                    ExecutedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_optimization_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "package_operations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", nullable: false),
                    PackageId = table.Column<string>(type: "TEXT", nullable: false),
                    RequestedVersion = table.Column<string>(type: "TEXT", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Result = table.Column<string>(type: "TEXT", nullable: false),
                    ExitCode = table.Column<int>(type: "INTEGER", nullable: true),
                    DetailJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_package_operations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "package_sources",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Provider = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_package_sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "software_groups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_software_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "software_history",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SoftwareName = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<string>(type: "TEXT", nullable: true),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    Trigger = table.Column<string>(type: "TEXT", nullable: false),
                    DetailJson = table.Column<string>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_software_history", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "system_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", nullable: true),
                    EventId = table.Column<int>(type: "INTEGER", nullable: true),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Computer = table.Column<string>(type: "TEXT", nullable: true),
                    Message = table.Column<string>(type: "TEXT", nullable: true),
                    RawJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "system_snapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Trigger = table.Column<string>(type: "TEXT", nullable: false),
                    Hostname = table.Column<string>(type: "TEXT", nullable: true),
                    OsName = table.Column<string>(type: "TEXT", nullable: true),
                    OsVersion = table.Column<string>(type: "TEXT", nullable: true),
                    OsBuild = table.Column<string>(type: "TEXT", nullable: true),
                    OsArch = table.Column<string>(type: "TEXT", nullable: true),
                    OsInstallDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    BootMode = table.Column<string>(type: "TEXT", nullable: true),
                    SecureBoot = table.Column<bool>(type: "INTEGER", nullable: true),
                    CpuModel = table.Column<string>(type: "TEXT", nullable: true),
                    CpuCores = table.Column<int>(type: "INTEGER", nullable: true),
                    CpuThreads = table.Column<int>(type: "INTEGER", nullable: true),
                    CpuVirtualization = table.Column<bool>(type: "INTEGER", nullable: true),
                    RamTotalMb = table.Column<long>(type: "INTEGER", nullable: true),
                    RamAvailableMb = table.Column<long>(type: "INTEGER", nullable: true),
                    GpuJson = table.Column<string>(type: "TEXT", nullable: true),
                    DiskJson = table.Column<string>(type: "TEXT", nullable: true),
                    NetworkJson = table.Column<string>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "env_instances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TemplateId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    DeployedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastValidatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastValidationResult = table.Column<string>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_env_instances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_env_instances_env_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "env_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "software_group_items",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GroupId = table.Column<long>(type: "INTEGER", nullable: false),
                    PackageId = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_software_group_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_software_group_items_software_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "software_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "env_deploy_logs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InstanceId = table.Column<long>(type: "INTEGER", nullable: false),
                    Step = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: true),
                    DetailJson = table.Column<string>(type: "TEXT", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_env_deploy_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_env_deploy_logs_env_instances_InstanceId",
                        column: x => x.InstanceId,
                        principalTable: "env_instances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "env_envvars",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InstanceId = table.Column<long>(type: "INTEGER", nullable: false),
                    VarName = table.Column<string>(type: "TEXT", nullable: false),
                    VarValue = table.Column<string>(type: "TEXT", nullable: true),
                    Scope = table.Column<string>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalValue = table.Column<string>(type: "TEXT", nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_env_envvars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_env_envvars_env_instances_InstanceId",
                        column: x => x.InstanceId,
                        principalTable: "env_instances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "env_snapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InstanceId = table.Column<long>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ToolStateJson = table.Column<string>(type: "TEXT", nullable: true),
                    EnvvarStateJson = table.Column<string>(type: "TEXT", nullable: true),
                    ConfigBackupPath = table.Column<string>(type: "TEXT", nullable: true),
                    RestorePointId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_env_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_env_snapshots_env_instances_InstanceId",
                        column: x => x.InstanceId,
                        principalTable: "env_instances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "env_tools",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InstanceId = table.Column<long>(type: "INTEGER", nullable: false),
                    ToolName = table.Column<string>(type: "TEXT", nullable: false),
                    RequestedVersion = table.Column<string>(type: "TEXT", nullable: true),
                    InstalledVersion = table.Column<string>(type: "TEXT", nullable: true),
                    Provider = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    InstalledAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ValidationOutput = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_env_tools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_env_tools_env_instances_InstanceId",
                        column: x => x.InstanceId,
                        principalTable: "env_instances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_app_settings_Key",
                table: "app_settings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_Action",
                table: "audit_logs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_Module_Level",
                table: "audit_logs",
                columns: new[] { "Module", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_Timestamp",
                table: "audit_logs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_env_deploy_logs_InstanceId",
                table: "env_deploy_logs",
                column: "InstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_env_envvars_InstanceId_VarName_Scope",
                table: "env_envvars",
                columns: new[] { "InstanceId", "VarName", "Scope" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_env_instances_TemplateId",
                table: "env_instances",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_env_snapshots_InstanceId",
                table: "env_snapshots",
                column: "InstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_env_templates_TemplateKey",
                table: "env_templates",
                column: "TemplateKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_env_tools_InstanceId",
                table: "env_tools",
                column: "InstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_installed_software_Name",
                table: "installed_software",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_installed_software_PackageId",
                table: "installed_software",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_log_anomalies_DetectedAt",
                table: "log_anomalies",
                column: "DetectedAt");

            migrationBuilder.CreateIndex(
                name: "IX_optimization_items_Category",
                table: "optimization_items",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_optimization_items_Code",
                table: "optimization_items",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_optimization_records_ExecutedAt",
                table: "optimization_records",
                column: "ExecutedAt");

            migrationBuilder.CreateIndex(
                name: "IX_optimization_records_ItemCode",
                table: "optimization_records",
                column: "ItemCode");

            migrationBuilder.CreateIndex(
                name: "IX_package_operations_StartedAt",
                table: "package_operations",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_package_sources_Provider_Name",
                table: "package_sources",
                columns: new[] { "Provider", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_software_group_items_GroupId_PackageId",
                table: "software_group_items",
                columns: new[] { "GroupId", "PackageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_software_groups_Name",
                table: "software_groups",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_software_history_Timestamp",
                table: "software_history",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_system_events_EventTime",
                table: "system_events",
                column: "EventTime");

            migrationBuilder.CreateIndex(
                name: "IX_system_events_Provider_EventId",
                table: "system_events",
                columns: new[] { "Provider", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_system_snapshots_CapturedAt",
                table: "system_snapshots",
                column: "CapturedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_settings");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "env_deploy_logs");

            migrationBuilder.DropTable(
                name: "env_envvars");

            migrationBuilder.DropTable(
                name: "env_snapshots");

            migrationBuilder.DropTable(
                name: "env_tools");

            migrationBuilder.DropTable(
                name: "installed_software");

            migrationBuilder.DropTable(
                name: "log_anomalies");

            migrationBuilder.DropTable(
                name: "optimization_items");

            migrationBuilder.DropTable(
                name: "optimization_records");

            migrationBuilder.DropTable(
                name: "package_operations");

            migrationBuilder.DropTable(
                name: "package_sources");

            migrationBuilder.DropTable(
                name: "software_group_items");

            migrationBuilder.DropTable(
                name: "software_history");

            migrationBuilder.DropTable(
                name: "system_events");

            migrationBuilder.DropTable(
                name: "system_snapshots");

            migrationBuilder.DropTable(
                name: "env_instances");

            migrationBuilder.DropTable(
                name: "software_groups");

            migrationBuilder.DropTable(
                name: "env_templates");
        }
    }
}
