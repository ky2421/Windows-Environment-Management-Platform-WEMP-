namespace WEMP.DevEnvironment.Services;

/// <summary>
/// 工具安装抽象：将模板工具名解析为实际安装动作。
/// </summary>
public interface IToolInstaller
{
    /// <summary>安装指定工具。optional 为 true 时失败仅告警不视为部署失败。</summary>
    Task<ToolInstallResult> InstallAsync(string toolName, string? version, bool optional, CancellationToken cancellationToken = default);

    /// <summary>尝试解析工具对应的安装包标识；无法解析返回 null（如 npm/pip 全局包）。</summary>
    string? ResolvePackageId(string toolName);
}

/// <summary>工具安装结果。</summary>
public sealed record ToolInstallResult(bool Success, string Status, string Message)
{
    /// <summary>跳过安装（可选工具或无需包管理器安装）。</summary>
    public static ToolInstallResult Skipped(string message) => new(true, "skipped", message);

    public static ToolInstallResult Ok(string message) => new(true, "installed", message);

    public static ToolInstallResult Failed(string message) => new(false, "failed", message);
}
