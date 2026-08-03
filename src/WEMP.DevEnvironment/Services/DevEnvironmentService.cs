using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Serilog;
using WEMP.DevEnvironment.Models;
using WEMP.DevEnvironment.Parsing;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;

namespace WEMP.DevEnvironment.Services;

/// <summary>开发环境部署服务实现。</summary>
public sealed class DevEnvironmentService : IDevEnvironmentService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IDbContextFactory<WempDbContext> _dbFactory;
    private readonly IToolInstaller _installer;
    private readonly IEnvironmentVariableService _envVars;
    private readonly IConfigFileWriter _configWriter;
    private readonly IToolValidator _validator;

    public DevEnvironmentService(
        IDbContextFactory<WempDbContext> dbFactory,
        IToolInstaller installer,
        IEnvironmentVariableService envVars,
        IConfigFileWriter configWriter,
        IToolValidator validator)
    {
        _dbFactory = dbFactory;
        _installer = installer;
        _envVars = envVars;
        _configWriter = configWriter;
        _validator = validator;
    }

    public async Task<List<EnvTemplate>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.EnvTemplates.AsNoTracking().OrderBy(t => t.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<EnvInstance>> GetInstancesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.EnvInstances
            .AsNoTracking()
            .Include(i => i.Tools)
            .Include(i => i.EnvVars)
            .Include(i => i.DeployLogs.OrderByDescending(l => l.Id).Take(50))
            .OrderByDescending(i => i.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> EnsureSeedAsync(string? templatesDirectory = null, CancellationToken cancellationToken = default)
    {
        var templatesDir = templatesDirectory ?? Path.Combine(AppContext.BaseDirectory, "templates");
        var builtIns = EnvTemplateParser.LoadBuiltInFiles(templatesDir);
        if (builtIns.Count == 0)
        {
            Log.Warning("未找到内置模板目录：{Directory}", templatesDir);
            return 0;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var now = DateTime.Now;
        var seeded = 0;
        foreach (var (key, content) in builtIns)
        {
            var spec = EnvTemplateParser.Parse(content);
            var existing = await db.EnvTemplates.FirstOrDefaultAsync(t => t.TemplateKey == key, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                db.EnvTemplates.Add(new EnvTemplate
                {
                    TemplateKey = key,
                    Name = spec.Name,
                    Description = spec.Description,
                    Version = spec.Version,
                    Content = content,
                    BuiltIn = true,
                    Enabled = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
                seeded++;
            }
            else if (existing.Content != content)
            {
                existing.Content = content;
                existing.Name = spec.Name;
                existing.Description = spec.Description;
                existing.Version = spec.Version;
                existing.UpdatedAt = now;
                seeded++;
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Log.Information("环境模板种子完成：{Count} 个模板", seeded);
        return seeded;
    }

    public async Task<EnvInstance> DeployAsync(long templateId, string? instanceName = null, IEnumerable<string>? selectedTools = null, IProgress<Models.DeployProgressInfo>? progress = null, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var template = await db.EnvTemplates.FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"模板不存在：{templateId}");

        var spec = EnvTemplateParser.Parse(template.Content);
        var name = string.IsNullOrWhiteSpace(instanceName)
            ? $"{template.Name} {DateTime.Now:yyyyMMdd-HHmm}"
            : instanceName.Trim();

        // 用户可选择安装工具子集；未选择时安装全部
        var toolsToInstall = spec.Tools;
        if (selectedTools?.Any() == true)
        {
            var set = new HashSet<string>(selectedTools, StringComparer.OrdinalIgnoreCase);
            toolsToInstall = spec.Tools.Where(t => set.Contains(t.Name)).ToList();
            Log.Information("按选择安装工具：{Selected} / {Total}", toolsToInstall.Count, spec.Tools.Count);
        }

        var instance = new EnvInstance
        {
            TemplateId = template.Id,
            Template = template,
            Name = name,
            Status = "deploying",
        };
        db.EnvInstances.Add(instance);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var failed = false;
        try
        {
            await LogStepAsync(db, instance.Id, "detect", "started", $"开始部署模板 {template.Name}（v{spec.Version}）", null, cancellationToken).ConfigureAwait(false);

            // 步骤 2：工具安装（winget）
            await LogStepAsync(db, instance.Id, "install", "started", $"待安装工具 {toolsToInstall.Count} 个", null, cancellationToken).ConfigureAwait(false);
            for (var toolIndex = 0; toolIndex < toolsToInstall.Count; toolIndex++)
            {
                var tool = toolsToInstall[toolIndex];
                var toolProgress = toolsToInstall.Count == 1 ? 60 : 10 + (int)(toolIndex * 80.0 / (toolsToInstall.Count - 1));
                progress?.Report(new Models.DeployProgressInfo(toolProgress, $"正在安装 {tool.Name}（{toolIndex + 1}/{toolsToInstall.Count}）"));
                var result = await _installer.InstallAsync(tool.Name, tool.Version, tool.Optional, cancellationToken).ConfigureAwait(false);
                db.EnvTools.Add(new EnvTool
                {
                    InstanceId = instance.Id,
                    ToolName = tool.Name,
                    RequestedVersion = tool.Version,
                    Provider = "winget",
                    Status = result.Success ? result.Status : "failed",
                    InstalledAt = result.Success ? DateTime.Now : null,
                    ValidationOutput = result.Message,
                });

                if (!result.Success && !tool.Optional)
                {
                    failed = true;
                }

                await LogStepAsync(db, instance.Id, "install", result.Success ? "success" : "failed", result.Message, null, cancellationToken).ConfigureAwait(false);
            }

            // 步骤 3：环境变量（user 作用域，回滚依据写入 env_envvars）
            progress?.Report(new Models.DeployProgressInfo(90, "正在设置环境变量"));
            await LogStepAsync(db, instance.Id, "envvar", "started", $"待设置环境变量 {spec.EnvironmentVariables.Count} 个", null, cancellationToken).ConfigureAwait(false);
            foreach (var env in spec.EnvironmentVariables)
            {
                var original = _envVars.GetValue(env.Name, env.Scope);
                var action = original is null ? "added" : (env.Overwrite ? "updated" : "skipped");
                if (!env.Overwrite && original is not null)
                {
                    await LogStepAsync(db, instance.Id, "envvar", "skipped", $"环境变量 {env.Name} 已存在且未开启覆盖，跳过", null, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                _envVars.SetValue(env.Name, env.Value, env.Scope);
                db.EnvEnvVars.Add(new EnvEnvVar
                {
                    InstanceId = instance.Id,
                    VarName = env.Name,
                    VarValue = env.Value,
                    Scope = env.Scope,
                    Action = action,
                    OriginalValue = original,
                    AppliedAt = DateTime.Now,
                });
                await LogStepAsync(db, instance.Id, "envvar", "success", $"设置环境变量 {env.Name}（{action}）", null, cancellationToken).ConfigureAwait(false);
            }

            // 步骤 4：配置文件写入
            progress?.Report(new Models.DeployProgressInfo(93, "正在写入配置文件"));
            var configFiles = spec.Config?.Files ?? [];
            await LogStepAsync(db, instance.Id, "config", "started", $"待写入配置文件 {configFiles.Count} 个", null, cancellationToken).ConfigureAwait(false);
            foreach (var file in configFiles)
            {
                var result = _configWriter.Write(file.Path, file.Values, file.Strategy);
                await LogStepAsync(db, instance.Id, "config", "success", $"写入 {result.Path}（{(result.Created ? "新建" : "合并")}，{result.KeysWritten} 项）", null, cancellationToken).ConfigureAwait(false);
            }

            // 步骤 5：验证命令
            progress?.Report(new Models.DeployProgressInfo(96, "正在执行验证命令"));
            var validationCommands = spec.Validation?.Commands ?? [];
            await LogStepAsync(db, instance.Id, "validate", "started", $"待执行验证命令 {validationCommands.Count} 条", null, cancellationToken).ConfigureAwait(false);
            var validationMessages = new List<string>();
            foreach (var cmd in validationCommands)
            {
                var result = await _validator.ValidateAsync(cmd.Command, cmd.Expected, cancellationToken).ConfigureAwait(false);
                if (!result.Passed)
                {
                    failed = true;
                }

                validationMessages.Add($"{cmd.Command} => {(result.Passed ? "通过" : result.Message ?? "失败")}");
                await LogStepAsync(db, instance.Id, "validate", result.Passed ? "success" : "failed",
                    result.Passed ? $"验证通过：{cmd.Command}" : $"验证失败：{cmd.Command} {result.Message}", null, cancellationToken).ConfigureAwait(false);
            }

            instance.LastValidatedAt = DateTime.Now;
            instance.LastValidationResult = validationMessages.Count == 0 ? "无验证命令" : string.Join("；", validationMessages);

            // 步骤 6：快照（工具与环境变量状态）
            progress?.Report(new Models.DeployProgressInfo(99, "正在生成环境快照"));
            await LogStepAsync(db, instance.Id, "snapshot", "started", "生成环境快照", null, cancellationToken).ConfigureAwait(false);
            var toolState = db.EnvTools.Where(t => t.InstanceId == instance.Id)
                .Select(t => new { t.ToolName, t.Status, t.InstalledVersion }).ToList();
            var envvarState = db.EnvEnvVars.Where(v => v.InstanceId == instance.Id)
                .Select(v => new { v.VarName, v.VarValue, v.Action }).ToList();
            db.EnvSnapshots.Add(new EnvSnapshot
            {
                InstanceId = instance.Id,
                Kind = "after",
                CapturedAt = DateTime.Now,
                ToolStateJson = JsonSerializer.Serialize(toolState, JsonOptions),
                EnvvarStateJson = JsonSerializer.Serialize(envvarState, JsonOptions),
            });
            await LogStepAsync(db, instance.Id, "snapshot", "success", "环境快照已生成", null, cancellationToken).ConfigureAwait(false);

            instance.Status = failed ? "failed" : "deployed";
            if (instance.Status == "deployed")
            {
                instance.DeployedAt = DateTime.Now;
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            progress?.Report(new Models.DeployProgressInfo(100, failed ? "部署完成（部分步骤失败）" : "部署完成"));
            return instance;
        }
        catch (Exception ex)
        {
            instance.Status = "failed";
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            Log.Error(ex, "环境部署失败：{Name}", name);
            throw;
        }
    }

    public async Task<ValidationResult> ValidateAsync(long instanceId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var instance = await db.EnvInstances
            .Include(i => i.Template)
            .FirstOrDefaultAsync(i => i.Id == instanceId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"实例不存在：{instanceId}");

        var spec = EnvTemplateParser.Parse(instance.Template.Content);
        var commands = spec.Validation?.Commands ?? [];
        if (commands.Count == 0)
        {
            return new ValidationResult(true, "", "模板未定义验证命令");
        }

        var results = new List<string>();
        var allPassed = true;
        foreach (var cmd in commands)
        {
            var result = await _validator.ValidateAsync(cmd.Command, cmd.Expected, cancellationToken).ConfigureAwait(false);
            allPassed &= result.Passed;
            results.Add($"{cmd.Command} => {(result.Passed ? "通过" : result.Message ?? "失败")}");
        }

        instance.LastValidatedAt = DateTime.Now;
        instance.LastValidationResult = string.Join("；", results);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ValidationResult(allPassed, string.Join("；", results), allPassed ? null : "存在未通过的验证命令");
    }

    public async Task<EnvInstance?> RollbackAsync(long instanceId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var instance = await db.EnvInstances
            .Include(i => i.EnvVars)
            .FirstOrDefaultAsync(i => i.Id == instanceId, cancellationToken).ConfigureAwait(false);
        if (instance is null)
        {
            return null;
        }

        // 逆序恢复环境变量：added → 删除；updated → 恢复原值；skipped 无动作
        foreach (var env in instance.EnvVars.OrderByDescending(v => v.Id))
        {
            switch (env.Action)
            {
                case "added":
                    _envVars.SetValue(env.VarName, null, env.Scope);
                    break;
                case "updated":
                    _envVars.SetValue(env.VarName, env.OriginalValue, env.Scope);
                    break;
            }
        }

        instance.Status = "rolled_back";
        instance.Note = $"回滚于 {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Log.Information("环境实例已回滚：{Name}（{Id}）", instance.Name, instance.Id);
        return instance;
    }

    private static async Task LogStepAsync(WempDbContext db, long instanceId, string step, string status, string? message, string? detailJson, CancellationToken cancellationToken)
    {
        db.EnvDeployLogs.Add(new EnvDeployLog
        {
            InstanceId = instanceId,
            Step = step,
            Status = status,
            Message = message,
            DetailJson = detailJson,
            StartedAt = DateTime.Now,
            FinishedAt = DateTime.Now,
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
