namespace WEMP.SystemInfo.Models;

/// <summary>已安装的开发工具。</summary>
public sealed class DevToolInfo
{
    /// <summary>工具标识，如 dotnet / node / python。</summary>
    public string Name { get; set; } = "";

    /// <summary>展示名，如 .NET SDK。</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>可执行文件名称。</summary>
    public string Executable { get; set; } = "";

    /// <summary>解析得到的版本号；未安装为 null。</summary>
    public string? Version { get; set; }

    /// <summary>是否已安装（探测成功）。</summary>
    public bool Installed => Version is not null;
}
