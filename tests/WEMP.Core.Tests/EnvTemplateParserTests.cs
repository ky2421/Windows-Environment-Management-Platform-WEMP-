using WEMP.DevEnvironment.Models;
using WEMP.DevEnvironment.Parsing;

namespace WEMP.Core.Tests;

/// <summary>环境模板 YAML 解析测试。</summary>
public class EnvTemplateParserTests
{
    private const string SampleYaml = """
        id: sample-1
        name: 示例环境
        description: 测试模板
        version: "1.0"

        tools:
          - name: node
            version: "20"
            versionManager: fnm
            optional: false
          - name: typescript
            optional: true

        environmentVariables:
          - name: NODE_HOME
            value: "%APPDATA%\\node"
            scope: user
            overwrite: false

        config:
          files:
            - path: "%USERPROFILE%\\.npmrc"
              strategy: merge
              values:
                registry: "https://registry.npmmirror.com"

        validation:
          commands:
            - command: "node --version"
              expected: "v20.*"

        prerequisites:
          - tool: git
            minVersion: "2.0"
        """;

    [Fact]
    public void Parse_extracts_full_spec()
    {
        var spec = EnvTemplateParser.Parse(SampleYaml);

        Assert.Equal("sample-1", spec.Id);
        Assert.Equal("示例环境", spec.Name);
        Assert.Equal("1.0", spec.Version);
        Assert.Equal(2, spec.Tools.Count);
        Assert.Equal("node", spec.Tools[0].Name);
        Assert.Equal("20", spec.Tools[0].Version);
        Assert.Equal("fnm", spec.Tools[0].VersionManager);
        Assert.False(spec.Tools[0].Optional);
        Assert.True(spec.Tools[1].Optional);

        Assert.Single(spec.EnvironmentVariables);
        Assert.Equal("NODE_HOME", spec.EnvironmentVariables[0].Name);
        Assert.Equal("%APPDATA%\\node", spec.EnvironmentVariables[0].Value);
        Assert.False(spec.EnvironmentVariables[0].Overwrite);

        Assert.Single(spec.Config!.Files);
        Assert.Equal("%USERPROFILE%\\.npmrc", spec.Config.Files[0].Path);
        Assert.Equal("merge", spec.Config.Files[0].Strategy);
        Assert.Equal("https://registry.npmmirror.com", spec.Config.Files[0].Values["registry"]);

        Assert.Single(spec.Validation!.Commands);
        Assert.Equal("node --version", spec.Validation.Commands[0].Command);
        Assert.Equal("v20.*", spec.Validation.Commands[0].Expected);

        Assert.Single(spec.Prerequisites);
        Assert.Equal("git", spec.Prerequisites[0].Tool);
    }

    [Fact]
    public void Parse_ignores_unknown_fields()
    {
        var spec = EnvTemplateParser.Parse("""
            id: t1
            name: 测试
            someUnknownField: 123
            tools: []
            """);

        Assert.Equal("t1", spec.Id);
        Assert.Empty(spec.Tools);
    }

    [Fact]
    public void Parse_throws_on_empty_input()
    {
        var ex = Assert.Throws<InvalidDataException>(() => EnvTemplateParser.Parse("   "));
        Assert.Contains("为空", ex.Message);
    }

    [Fact]
    public void Parse_throws_when_missing_id()
    {
        var ex = Assert.Throws<InvalidDataException>(() => EnvTemplateParser.Parse("name: 无名\nversion: \"1.0\""));
        Assert.Contains("id / name", ex.Message);
    }

    [Fact]
    public void Parse_throws_on_syntax_error()
    {
        var ex = Assert.Throws<InvalidDataException>(() => EnvTemplateParser.Parse("id: [unclosed"));
        Assert.Contains("语法错误", ex.Message);
    }

    [Fact]
    public void Parse_handles_real_builtin_template_shape()
    {
        var spec = EnvTemplateParser.Parse(SampleYaml);
        Assert.NotNull(spec.Validation);
        Assert.NotNull(spec.Config);
    }
}
