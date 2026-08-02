namespace WEMP.DevEnvironment.Services;

/// <summary>
/// 工具验证抽象：执行验证命令并匹配输出。
/// </summary>
public interface IToolValidator
{
    /// <summary>执行命令并判断输出是否匹配期望正则；expected 为空时以退出码 0 为准。</summary>
    Task<ValidationResult> ValidateAsync(string command, string? expected, CancellationToken cancellationToken = default);
}

/// <summary>验证结果。</summary>
public sealed record ValidationResult(bool Passed, string Output, string? Message);
