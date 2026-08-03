using System.IO;
using System.Text;

namespace WEMP.DevEnvironment.Services;

/// <summary>
/// INI 风格配置文件写入实现（保留分节与键值，键支持 "section.key" 语法自动分节，
/// 例如 "global.index-url" 写出为 [global] 下的 index-url）。
/// </summary>
public sealed class ConfigFileWriter : IConfigFileWriter
{
    public ConfigWriteResult Write(string path, IReadOnlyDictionary<string, string> values, string strategy)
    {
        var fullPath = EnvironmentVariableService.ExpandVariables(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var created = !File.Exists(fullPath);
        // 内部统一用 "section.key" 扁平键；无分节时仅用 key
        var existing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (File.Exists(fullPath) && strategy.Equals("merge", StringComparison.OrdinalIgnoreCase))
        {
            var currentSection = string.Empty;
            foreach (var rawLine in File.ReadAllLines(fullPath, Encoding.UTF8))
            {
                var trimmed = rawLine.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
                {
                    continue;
                }

                if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                {
                    currentSection = trimmed[1..^1].Trim();
                    continue;
                }

                var eq = trimmed.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                var key = trimmed[..eq].Trim();
                existing[CompositeKey(currentSection, key)] = trimmed[(eq + 1)..].Trim();
            }
        }

        foreach (var (key, value) in values)
        {
            existing[key] = value;
        }

        // 按分节写出：无分节键在前，其余按节名字母序，节内按键名字母序
        var sb = new StringBuilder();
        foreach (var group in existing.GroupBy(kv => GetSection(kv.Key), StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(group.Key))
            {
                sb.AppendLine($"[{group.Key}]");
            }

            foreach (var (compositeKey, value) in group.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"{GetKeyPart(compositeKey)}={value}");
            }

            sb.AppendLine();
        }

        File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);
        return new ConfigWriteResult(fullPath, created, values.Count);
    }

    private static string CompositeKey(string section, string key)
        => string.IsNullOrEmpty(section) ? key : $"{section}.{key}";

    private static string? GetSection(string compositeKey)
    {
        var dot = compositeKey.IndexOf('.');
        return dot > 0 ? compositeKey[..dot] : null;
    }

    private static string GetKeyPart(string compositeKey)
    {
        var dot = compositeKey.IndexOf('.');
        return dot > 0 ? compositeKey[(dot + 1)..] : compositeKey;
    }
}
