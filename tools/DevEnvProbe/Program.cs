using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WEMP.DevEnvironment.Parsing;
using WEMP.DevEnvironment.Services;
using WEMP.Infrastructure.Data;

// 1. 解析真实内置模板（输出目录 templates/，由 csproj 复制）
var templatesDir = Path.Combine(AppContext.BaseDirectory, "templates");
var builtIns = EnvTemplateParser.LoadBuiltInFiles(templatesDir);
Console.WriteLine($"内置模板目录: {templatesDir}");
foreach (var (key, _) in builtIns)
{
    var spec = EnvTemplateParser.Parse(builtIns[key]);
    Console.WriteLine($"  [{key}] {spec.Name} v{spec.Version} | 工具 {spec.Tools.Count} | 环境变量 {spec.EnvironmentVariables.Count} | 配置 {spec.Config?.Files.Count ?? 0} | 验证 {spec.Validation?.Commands.Count ?? 0}");
}

// 2. 临时库 + Fake 依赖：真实模板全流程部署（不触碰真实系统）
var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();
var options = new DbContextOptionsBuilder<WempDbContext>().UseSqlite(connection).Options;
var db = new WempDbContext(options);
db.Database.EnsureCreated();

var fakeInstaller = new FakeInstaller();
var service = new DevEnvironmentService(db, fakeInstaller, new FakeEnvVars(), new FakeConfigWriter(), new FakeValidator());

var seeded = await service.EnsureSeedAsync(templatesDir);
Console.WriteLine($"种子模板: {seeded} 个");
var templates = await service.GetTemplatesAsync();
foreach (var t in templates)
{
    var instance = await service.DeployAsync(t.Id, $"{t.Name}-probe");
    var tools = db.EnvTools.Where(x => x.InstanceId == instance.Id).ToList();
    var logs = db.EnvDeployLogs.Where(x => x.InstanceId == instance.Id).OrderBy(x => x.Id).ToList();
    var snapshots = db.EnvSnapshots.Where(x => x.InstanceId == instance.Id).ToList();
    Console.WriteLine($"部署实例 [{t.TemplateKey}] {instance.Name} 状态={instance.Status} | 工具 {tools.Count} | 日志 {logs.Count} | 快照 {snapshots.Count}");
    foreach (var tool in tools)
    {
        Console.WriteLine($"  工具 {tool.ToolName} -> {tool.Status}");
    }

    // 3. 回滚 dry-run
    var rolled = await service.RollbackAsync(instance.Id);
    Console.WriteLine($"回滚: {rolled?.Status}");
}

// 4. 真实数据库：仅种子内置模板（与 App 启动行为一致，无其他副作用）
var realOptions = new DbContextOptionsBuilder<WempDbContext>()
    .UseSqlite(WempDatabase.CreateConnectionString())
    .Options;
using var realDb = new WempDbContext(realOptions);
var realService = new DevEnvironmentService(realDb, fakeInstaller, new FakeEnvVars(), new FakeConfigWriter(), new FakeValidator());
var realSeeded = await realService.EnsureSeedAsync();
var realTemplates = await realService.GetTemplatesAsync();
Console.WriteLine($"真实库种子: {realSeeded} 个（env_templates 共 {realTemplates.Count} 条）");

public sealed class FakeInstaller : IToolInstaller
{
    public Task<ToolInstallResult> InstallAsync(string toolName, string? version, bool optional, CancellationToken ct)
        => Task.FromResult(optional
            ? ToolInstallResult.Skipped($"{toolName} 跳过（可选）")
            : ToolInstallResult.Ok($"{toolName} 安装完成（模拟）"));

    public string? ResolvePackageId(string toolName) => $"fake.{toolName}";
}

public sealed class FakeEnvVars : IEnvironmentVariableService
{
    public Dictionary<string, string?> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? GetValue(string name, string scope = "user") => Values.GetValueOrDefault(name);

    public string? SetValue(string name, string? value, string scope = "user")
    {
        var original = GetValue(name, scope);
        if (value is null) { Values.Remove(name); } else { Values[name] = value; }
        return original;
    }
}

public sealed class FakeConfigWriter : IConfigFileWriter
{
    public ConfigWriteResult Write(string path, IReadOnlyDictionary<string, string> values, string strategy)
        => new(path, Created: true, values.Count);
}

public sealed class FakeValidator : IToolValidator
{
    public Task<ValidationResult> ValidateAsync(string command, string? expected, CancellationToken ct)
        => Task.FromResult(new ValidationResult(true, "v1.0.0", null));
}
