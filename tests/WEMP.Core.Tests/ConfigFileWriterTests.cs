using System.Text;
using WEMP.DevEnvironment.Services;

namespace WEMP.Core.Tests;

/// <summary>INI 配置文件写入测试：分节语法与合并语义。</summary>
public class ConfigFileWriterTests
{
    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"wemp-cfg-{Guid.NewGuid():N}.ini");

    [Fact]
    public void Write_dotted_key_creates_ini_section()
    {
        var writer = new ConfigFileWriter();
        var path = TempPath();
        try
        {
            var result = writer.Write(path, new Dictionary<string, string>
            {
                ["global.index-url"] = "https://pypi.example.com/simple",
            }, "create");

            Assert.True(result.Created);
            var content = File.ReadAllText(path);
            Assert.Contains("[global]", content);
            Assert.Contains("index-url=https://pypi.example.com/simple", content);
            // pip 需要 [global] 段头，而不是扁平行
            Assert.DoesNotContain("global.index-url=", content);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Write_merge_preserves_sections_and_updates_values()
    {
        var writer = new ConfigFileWriter();
        var path = TempPath();
        try
        {
            File.WriteAllText(path, "[global]\nindex-url=https://old.example.com\n\n[install]\ntrusted-host=pypi.example.com\n", Encoding.UTF8);

            writer.Write(path, new Dictionary<string, string>
            {
                ["global.index-url"] = "https://new.example.com",
                ["tool.ruff"] = "on",
            }, "merge");

            var content = File.ReadAllText(path);
            Assert.Contains("[global]", content);
            Assert.Contains("index-url=https://new.example.com", content);
            Assert.Contains("[install]", content);
            Assert.Contains("trusted-host=pypi.example.com", content);
            Assert.Contains("[tool]", content);
            Assert.Contains("ruff=on", content);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Write_plain_key_stays_without_section()
    {
        var writer = new ConfigFileWriter();
        var path = TempPath();
        try
        {
            writer.Write(path, new Dictionary<string, string>
            {
                ["width"] = "800",
            }, "create");

            var content = File.ReadAllText(path);
            Assert.DoesNotContain("[", content);
            Assert.Contains("width=800", content);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
