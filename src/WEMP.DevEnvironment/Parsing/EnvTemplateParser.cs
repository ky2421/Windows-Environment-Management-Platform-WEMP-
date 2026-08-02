using System.IO;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace WEMP.DevEnvironment.Parsing;

/// <summary>
/// 环境模板 YAML 解析器：将模板文本反序列化为 <see cref="Models.EnvTemplateSpec"/>。
/// 解析失败抛出 <see cref="InvalidDataException"/> 并附带行号定位信息。
/// </summary>
public static class EnvTemplateParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static Models.EnvTemplateSpec Parse(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            throw new InvalidDataException("模板内容为空");
        }

        try
        {
            var spec = Deserializer.Deserialize<Models.EnvTemplateSpec>(yaml);
            if (string.IsNullOrWhiteSpace(spec.Id) || string.IsNullOrWhiteSpace(spec.Name))
            {
                throw new InvalidDataException("模板缺少必填字段 id / name");
            }

            return spec;
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            var line = ex.Start.Line > 0 ? $"（第 {ex.Start.Line} 行）" : "";
            throw new InvalidDataException($"模板 YAML 语法错误{line}：{ex.Message}", ex);
        }
    }

    /// <summary>按模板键加载内置模板目录（templates/*.yaml）。</summary>
    public static IReadOnlyDictionary<string, string> LoadBuiltInFiles(string directory)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(directory))
        {
            return result;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.yaml"))
        {
            var spec = Parse(File.ReadAllText(file, Encoding.UTF8));
            result[spec.Id] = File.ReadAllText(file, Encoding.UTF8);
        }

        return result;
    }
}
