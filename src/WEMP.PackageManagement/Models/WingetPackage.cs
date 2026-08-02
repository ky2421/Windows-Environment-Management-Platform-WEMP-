namespace WEMP.PackageManagement.Models;

/// <summary>winget 输出中的软件包信息。</summary>
public record WingetPackage(
    string Name,
    string Id,
    string Version,
    string? Available,
    string Source);
