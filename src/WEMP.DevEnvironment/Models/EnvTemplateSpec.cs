namespace WEMP.DevEnvironment.Models;

/// <summary>
/// 环境模板规范（YAML 反序列化目标），对应 assets/templates/*.yaml。
/// </summary>
public sealed class EnvTemplateSpec
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? DeployWarning { get; set; }
    public string Version { get; set; } = "1.0";
    public string? MinWindowsVersion { get; set; }
    public List<ToolSpec> Tools { get; set; } = [];
    public List<EnvVarSpec> EnvironmentVariables { get; set; } = [];
    public ConfigSpec? Config { get; set; }
    public ValidationSpec? Validation { get; set; }
    public List<PrerequisiteSpec> Prerequisites { get; set; } = [];
}

/// <summary>模板内工具清单项。</summary>
public sealed class ToolSpec
{
    public string Name { get; set; } = "";
    public string? Version { get; set; }
    public string? VersionManager { get; set; }
    public bool Optional { get; set; }
}

/// <summary>模板内环境变量项。</summary>
public sealed class EnvVarSpec
{
    public string Name { get; set; } = "";
    public string? Value { get; set; }
    public string Scope { get; set; } = "user";
    public bool Overwrite { get; set; }
}

/// <summary>模板内配置文件写入项。</summary>
public sealed class ConfigSpec
{
    public List<ConfigFileSpec> Files { get; set; } = [];
}

/// <summary>单个配置文件定义。</summary>
public sealed class ConfigFileSpec
{
    public string Path { get; set; } = "";
    public string Strategy { get; set; } = "merge";
    public Dictionary<string, string> Values { get; set; } = [];
}

/// <summary>模板内验证命令项。</summary>
public sealed class ValidationSpec
{
    public List<ValidationCommandSpec> Commands { get; set; } = [];
}

/// <summary>单条验证命令：运行命令并匹配输出正则。</summary>
public sealed class ValidationCommandSpec
{
    public string Command { get; set; } = "";
    public string? Expected { get; set; }
}

/// <summary>模板前置条件项。</summary>
public sealed class PrerequisiteSpec
{
    public string Tool { get; set; } = "";
    public string? MinVersion { get; set; }
}
