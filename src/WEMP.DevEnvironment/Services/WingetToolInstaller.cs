using WEMP.PackageManagement.Winget;

namespace WEMP.DevEnvironment.Services;

/// <summary>
/// 基于 winget 的工具安装实现。维护模板工具名到 winget 包 id 的映射；
/// npm/pip 全局包（typescript、ruff 等）无映射，可选工具跳过、必需工具失败。
/// </summary>
public sealed class WingetToolInstaller : IToolInstaller
{
    private static readonly IReadOnlyDictionary<string, string> PackageIdMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["node"] = "OpenJS.NodeJS.LTS",
            ["nodejs"] = "OpenJS.NodeJS.LTS",
            ["python"] = "Python.Python.3.12",
            ["git"] = "Git.Git",
            ["yarn"] = "Yarn.Yarn",
            ["pnpm"] = "Pnpm.Pnpm",
            ["docker"] = "Docker.DockerDesktop",
            ["vscode"] = "Microsoft.VisualStudioCode",
            ["code"] = "Microsoft.VisualStudioCode",
            ["uv"] = "astral-sh.uv",
            ["poetry"] = "Python.Poetry",
            ["go"] = "GoLang.Go",
            ["rust"] = "Rustlang.Rustup",
            ["jdk"] = "Microsoft.OpenJDK.21",
            ["java"] = "Microsoft.OpenJDK.21",
            ["7zip"] = "7zip.7zip",
        };

    private readonly IPackageProvider _provider;

    public WingetToolInstaller(IPackageProvider provider)
    {
        _provider = provider;
    }

    public string? ResolvePackageId(string toolName)
        => PackageIdMap.TryGetValue(toolName.Trim(), out var id) ? id : null;

    public async Task<ToolInstallResult> InstallAsync(string toolName, string? version, bool optional, CancellationToken cancellationToken = default)
    {
        var packageId = ResolvePackageId(toolName);
        if (string.IsNullOrWhiteSpace(packageId))
        {
            var message = $"工具 {toolName} 无 winget 包映射（可能为 npm/pip 全局包）";
            return optional ? ToolInstallResult.Skipped(message) : ToolInstallResult.Failed(message);
        }

        var result = await _provider.InstallAsync(packageId, cancellationToken).ConfigureAwait(false);
        if (result.Success)
        {
            return ToolInstallResult.Ok($"{toolName} -> {packageId} 安装完成");
        }

        var failure = $"{toolName} -> {packageId} 安装失败（退出码 {result.ExitCode}）";
        return optional ? ToolInstallResult.Skipped(failure) : ToolInstallResult.Failed(failure);
    }
}
