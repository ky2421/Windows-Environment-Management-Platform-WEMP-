using System.IO;
using System.Text;

namespace WEMP.DevEnvironment.Services;

/// <summary>INI 风格配置文件写入实现（保留注释与空行，键值支持大小写不敏感合并）。</summary>
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
        var existing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (File.Exists(fullPath) && strategy.Equals("merge", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var line in File.ReadAllLines(fullPath, Encoding.UTF8))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
                {
                    continue;
                }

                var eq = trimmed.IndexOf('=');
                if (eq > 0)
                {
                    existing[trimmed[..eq].Trim()] = trimmed[(eq + 1)..].Trim();
                }
            }
        }

        foreach (var (key, value) in values)
        {
            existing[key] = value;
        }

        var sb = new StringBuilder();
        foreach (var (key, value) in existing)
        {
            sb.AppendLine($"{key}={value}");
        }

        File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);
        return new ConfigWriteResult(fullPath, created, values.Count);
    }
}
