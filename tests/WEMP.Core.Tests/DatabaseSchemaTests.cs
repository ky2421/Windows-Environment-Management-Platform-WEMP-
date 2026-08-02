using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WEMP.Infrastructure.Data;

namespace WEMP.Core.Tests;

/// <summary>
/// 数据库结构验证：确保实体映射正确且 19 张核心表全部存在。
/// 使用内存 SQLite，不触碰真实数据库文件。
/// </summary>
public class DatabaseSchemaTests
{
    private static readonly string[] ExpectedTables =
    [
        // 用户配置
        "app_settings",
        // 系统信息
        "system_snapshots",
        // 优化记录
        "optimization_items",
        "optimization_records",
        // 游戏模式
        "game_sessions",
        // 日志
        "audit_logs",
        "system_events",
        "log_anomalies",
        // 软件列表
        "installed_software",
        "software_history",
        "software_groups",
        "software_group_items",
        "package_operations",
        "package_sources",
        // 开发环境
        "env_templates",
        "env_instances",
        "env_tools",
        "env_envvars",
        "env_snapshots",
        "env_deploy_logs",
    ];

    [Fact]
    public void All_expected_tables_are_created()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<WempDbContext>()
            .UseSqlite(connection)
            .Options;

        using var db = new WempDbContext(options);
        db.Database.EnsureCreated();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";

        var actual = new List<string>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                actual.Add(reader.GetString(0));
            }
        }

        var missing = ExpectedTables.Except(actual).ToList();
        Assert.True(
            missing.Count == 0,
            $"缺少数据表: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Unique_indexes_are_applied()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<WempDbContext>()
            .UseSqlite(connection)
            .Options;

        using var db = new WempDbContext(options);
        db.Database.EnsureCreated();

        var command = connection.CreateCommand();
        command.CommandText =
            "SELECT name FROM sqlite_master WHERE type = 'index' AND name IN " +
            "('IX_app_settings_Key', 'IX_optimization_items_Code', 'IX_env_templates_TemplateKey', 'IX_software_group_items_GroupId_PackageId')";

        var count = 0;
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                count++;
            }
        }

        Assert.Equal(4, count);
    }
}
