using System.Text.RegularExpressions;
using WEMP.PackageManagement.Infrastructure;

namespace WEMP.DevEnvironment.Services;

/// <summary>基于命令行执行的验证实现（命令经 cmd.exe /c 运行，UTF-8 捕获输出）。</summary>
public sealed class CommandValidator : IToolValidator
{
    public async Task<ValidationResult> ValidateAsync(string command, string? expected, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await ProcessRunner.RunAsync("cmd.exe", $"/c {command}", cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(expected))
            {
                var regex = new Regex(expected, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                var passed = regex.IsMatch(result.Output);
                return new ValidationResult(passed, result.Output.Trim(), passed ? null : $"输出未匹配 {expected}");
            }

            return new ValidationResult(result.Success, result.Output.Trim(), result.Success ? null : $"退出码 {result.ExitCode}");
        }
        catch (Exception ex)
        {
            return new ValidationResult(false, "", $"验证执行失败：{ex.Message}");
        }
    }
}
