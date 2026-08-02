using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WEMP.DevEnvironment.Services;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;

namespace WEMP.Core.Tests;

/// <summary>开发环境部署服务测试：Fake 依赖隔离 Windows 交互与 winget 调用。</summary>
public class DevEnvironmentServiceTests
{
    private sealed class FakeInstaller : IToolInstaller
    {
        public Func<string, bool, Task<ToolInstallResult>> Handler { get; set; } =
            (name, _) => Task.FromResult(
                name == "typescript"
                    ? ToolInstallResult.Skipped("typescript 无 winget 包映射")
                    : ToolInstallResult.Ok($"{name} 安装完成"));

        public List<string> InstalledTools { get; } = [];

        public string? ResolvePackageId(string toolName)
            => toolName == "node" ? "OpenJS.NodeJS.LTS" : null;

        public Task<ToolInstallResult> InstallAsync(string toolName, string? version, bool optional, CancellationToken ct)
        {
            InstalledTools.Add(toolName);
            return Handler(toolName, optional);
        }
    }

    private sealed class FakeEnvVars : IEnvironmentVariableService
    {
        public Dictionary<string, string?> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string? GetValue(string name, string scope = "user") => Values.GetValueOrDefault(name);

        public string? SetValue(string name, string? value, string scope = "user")
        {
            var original = GetValue(name, scope);
            if (value is null)
            {
                Values.Remove(name);
            }
            else
            {
                Values[name] = value;
            }

            return original;
        }
    }

    private sealed class FakeConfigWriter : IConfigFileWriter
    {
        public List<(string Path, int Keys, string Strategy)> Writes { get; } = [];

        public ConfigWriteResult Write(string path, IReadOnlyDictionary<string, string> values, string strategy)
        {
            Writes.Add((path, values.Count, strategy));
            return new ConfigWriteResult(path, Created: true, values.Count);
        }
    }

    private sealed class FakeValidator : IToolValidator
    {
        public bool Passed { get; set; } = true;

        public Task<ValidationResult> ValidateAsync(string command, string? expected, CancellationToken ct)
            => Task.FromResult(new ValidationResult(Passed, "v20.11.0", Passed ? null : $"输出未匹配 {expected}"));
    }

    private const string TemplateYaml = """
        id: node-20
        name: Node.js 测试环境
        description: 测试
        version: "1.0"
        tools:
          - name: node
            version: "20"
            optional: false
          - name: typescript
            optional: true
        environmentVariables:
          - name: NODE_HOME
            value: "C:\\node"
            overwrite: false
        config:
          files:
            - path: "C:\\temp\\.npmrc"
              strategy: merge
              values:
                registry: "https://registry.example.com"
        validation:
          commands:
            - command: "node --version"
              expected: "v20.*"
        """;

    private static (WempDbContext Db, DevEnvironmentService Service, FakeInstaller Installer, FakeEnvVars EnvVars, FakeConfigWriter Config, FakeValidator Validator) CreateHarness()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<WempDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new WempDbContext(options);
        db.Database.EnsureCreated();

        var installer = new FakeInstaller();
        var envVars = new FakeEnvVars();
        var config = new FakeConfigWriter();
        var validator = new FakeValidator();

        var service = new DevEnvironmentService(db, installer, envVars, config, validator);
        return (db, service, installer, envVars, config, validator);
    }

    private static async Task<EnvTemplate> SeedTemplateAsync(WempDbContext db)
    {
        var template = new EnvTemplate
        {
            TemplateKey = "node-20",
            Name = "Node.js 测试环境",
            Description = "测试",
            Version = "1.0",
            Content = TemplateYaml,
            BuiltIn = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
        };
        db.EnvTemplates.Add(template);
        await db.SaveChangesAsync();
        return template;
    }

    [Fact]
    public async Task Deploy_runs_full_pipeline_and_marks_deployed()
    {
        var (db, service, installer, envVars, config, _) = CreateHarness();
        var template = await SeedTemplateAsync(db);

        var instance = await service.DeployAsync(template.Id);

        Assert.Equal("deployed", instance.Status);
        Assert.NotNull(instance.DeployedAt);
        Assert.Contains("node", installer.InstalledTools);
        Assert.Equal("C:\\node", envVars.Values["NODE_HOME"]);
        Assert.Single(config.Writes);
        Assert.Equal("C:\\temp\\.npmrc", config.Writes[0].Path);

        var tools = await db.EnvTools.Where(t => t.InstanceId == instance.Id).ToListAsync();
        Assert.Equal(2, tools.Count);
        Assert.Equal("installed", tools.First(t => t.ToolName == "node").Status);
        Assert.Equal("skipped", tools.First(t => t.ToolName == "typescript").Status);

        var envVarRecord = await db.EnvEnvVars.SingleAsync(v => v.InstanceId == instance.Id);
        Assert.Equal("added", envVarRecord.Action);

        var logs = await db.EnvDeployLogs.Where(l => l.InstanceId == instance.Id).OrderBy(l => l.Id).ToListAsync();
        Assert.Contains(logs, l => l.Step == "validate" && l.Status == "success");
        Assert.Contains(logs, l => l.Step == "snapshot" && l.Status == "success");

        var snapshot = await db.EnvSnapshots.SingleAsync(s => s.InstanceId == instance.Id);
        Assert.Equal("after", snapshot.Kind);
        Assert.NotNull(snapshot.ToolStateJson);
        Assert.NotNull(snapshot.EnvvarStateJson);

        var loaded = await service.GetInstancesAsync();
        var loadedInstance = Assert.Single(loaded);
        Assert.Equal(2, loadedInstance.Tools.Count);
    }

    [Fact]
    public async Task Deploy_fails_when_required_tool_fails()
    {
        var (db, service, installer, _, _, _) = CreateHarness();
        var template = await SeedTemplateAsync(db);
        installer.Handler = (name, optional) =>
            Task.FromResult(name == "node"
                ? ToolInstallResult.Failed("node 安装失败")
                : ToolInstallResult.Skipped("skip"));

        var instance = await service.DeployAsync(template.Id);

        Assert.Equal("failed", instance.Status);
        var tool = await db.EnvTools.SingleAsync(t => t.InstanceId == instance.Id && t.ToolName == "node");
        Assert.Equal("failed", tool.Status);
    }

    [Fact]
    public async Task Deploy_skips_existing_envvar_when_overwrite_disabled()
    {
        var (db, service, _, envVars, _, _) = CreateHarness();
        var template = await SeedTemplateAsync(db);
        envVars.Values["NODE_HOME"] = "existing";

        var instance = await service.DeployAsync(template.Id);

        Assert.Equal("deployed", instance.Status);
        Assert.Equal("existing", envVars.Values["NODE_HOME"]);
        Assert.Empty(await db.EnvEnvVars.Where(v => v.InstanceId == instance.Id).ToListAsync());
        var log = await db.EnvDeployLogs.SingleAsync(l => l.InstanceId == instance.Id && l.Step == "envvar" && l.Status == "skipped");
        Assert.Contains("已存在", log.Message);
    }

    [Fact]
    public async Task Deploy_throws_when_template_missing()
    {
        var (_, service, _, _, _, _) = CreateHarness();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeployAsync(999));
    }

    [Fact]
    public async Task Rollback_restores_envvars_and_marks_rolled_back()
    {
        var (db, service, _, envVars, _, _) = CreateHarness();
        var template = await SeedTemplateAsync(db);
        envVars.Values["NODE_HOME"] = "old-value";
        var template2 = TemplateYaml.Replace("overwrite: false", "overwrite: true");
        template.Content = template2;
        await db.SaveChangesAsync();

        var instance = await service.DeployAsync(template.Id);
        Assert.Equal("C:\\node", envVars.Values["NODE_HOME"]);

        var rolled = await service.RollbackAsync(instance.Id);
        Assert.NotNull(rolled);
        Assert.Equal("rolled_back", rolled.Status);
        Assert.Equal("old-value", envVars.Values["NODE_HOME"]);

        var record = await db.EnvEnvVars.SingleAsync(v => v.InstanceId == instance.Id);
        Assert.Equal("updated", record.Action);
        Assert.Equal("old-value", record.OriginalValue);
    }

    [Fact]
    public async Task Rollback_removes_added_envvar()
    {
        var (db, service, _, envVars, _, _) = CreateHarness();
        var template = await SeedTemplateAsync(db);

        var instance = await service.DeployAsync(template.Id);
        Assert.Equal("C:\\node", envVars.Values["NODE_HOME"]);

        await service.RollbackAsync(instance.Id);

        Assert.False(envVars.Values.ContainsKey("NODE_HOME"));
    }

    [Fact]
    public async Task Validate_updates_last_validation_result()
    {
        var (db, service, _, _, _, validator) = CreateHarness();
        var template = await SeedTemplateAsync(db);
        validator.Passed = false;

        var instance = await service.DeployAsync(template.Id);
        var result = await service.ValidateAsync(instance.Id);

        Assert.False(result.Passed);
        var reloaded = await db.EnvInstances.FindAsync(instance.Id);
        Assert.NotNull(reloaded);
        Assert.NotNull(reloaded.LastValidationResult);
        Assert.Contains("未匹配", reloaded.LastValidationResult);
    }

    [Fact]
    public async Task EnsureSeed_seeds_and_updates_builtin_templates()
    {
        var (db, service, _, _, _, _) = CreateHarness();
        var dir = Path.Combine(Path.GetTempPath(), $"wemp-tpl-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "node.yaml"), TemplateYaml);

            var count = await service.EnsureSeedAsync(dir);
            Assert.Equal(1, count);
            var template = await db.EnvTemplates.SingleAsync();
            Assert.Equal("node-20", template.TemplateKey);
            Assert.True(template.BuiltIn);

            // 幂等：内容未变不重复计数
            var second = await service.EnsureSeedAsync(dir);
            Assert.Equal(0, second);
            Assert.Equal(1, await db.EnvTemplates.CountAsync());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureSeed_returns_zero_when_no_templates_dir()
    {
        var (_, service, _, _, _, _) = CreateHarness();

        var count = await service.EnsureSeedAsync(Path.Combine(Path.GetTempPath(), $"wemp-missing-{Guid.NewGuid():N}"));
        Assert.Equal(0, count);
    }
}
