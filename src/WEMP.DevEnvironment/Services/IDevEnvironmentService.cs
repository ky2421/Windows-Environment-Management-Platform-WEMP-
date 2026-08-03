using WEMP.Infrastructure.Data.Entities;

namespace WEMP.DevEnvironment.Services;

/// <summary>
/// 开发环境服务：YAML 模板驱动的工具链部署（工具安装 → 环境变量 → 配置文件 → 验证 → 快照），
/// 部署流水线记录到 env_deploy_logs，支持验证与回滚。
/// </summary>
public interface IDevEnvironmentService
{
    /// <summary>列出全部环境模板。</summary>
    Task<List<EnvTemplate>> GetTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>列出全部环境实例（含工具/环境变量/部署日志）。</summary>
    Task<List<EnvInstance>> GetInstancesAsync(CancellationToken cancellationToken = default);

    /// <summary>从内置模板目录种子模板库（幂等：已存在则更新内容）。</summary>
    Task<int> EnsureSeedAsync(string? templatesDirectory = null, CancellationToken cancellationToken = default);

    /// <summary>按模板创建实例并执行部署流水线；<paramref name="selectedTools"/> 非空时仅安装其中列出的工具，<paramref name="progress"/> 用于回报部署进度。</summary>
    Task<EnvInstance> DeployAsync(long templateId, string? instanceName = null, IEnumerable<string>? selectedTools = null, IProgress<Models.DeployProgressInfo>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>对实例重新执行验证命令。</summary>
    Task<ValidationResult> ValidateAsync(long instanceId, CancellationToken cancellationToken = default);

    /// <summary>回滚实例：恢复环境变量原值并标记为已回滚。</summary>
    Task<EnvInstance?> RollbackAsync(long instanceId, CancellationToken cancellationToken = default);
}
