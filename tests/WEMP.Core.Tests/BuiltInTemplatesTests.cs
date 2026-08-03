using System.IO;
using WEMP.DevEnvironment.Parsing;

namespace WEMP.Core.Tests;

/// <summary>
/// 内置环境模板测试：全部模板可解析、id 唯一、且覆盖环境清单要求的类别
/// （清单缺失时测试失败，防止后续维护遗漏）。
/// </summary>
public class BuiltInTemplatesTests
{
    private static readonly string TemplatesDir =
        Path.Combine(AppContext.BaseDirectory, "assets", "templates");

    /// <summary>环境清单要求的模板 id（与 assets/templates/*.yaml 的 id 对应）。</summary>
    private static readonly string[] RequiredTemplateIds =
    [
        // 一、前端开发
        "nodejs-20",        // Node.js（npm/yarn/pnpm）
        "browser-chrome",   // 浏览器环境
        "deno-bun",         // Deno / Bun
        // 二、后端服务开发
        "java-21",          // JDK
        "python-312",       // Python + conda/venv/poetry
        "go-122",           // Go
        "dotnet-8",         // .NET SDK
        "php-8",            // PHP + php-fpm
        "ruby",             // Ruby on Rails
        // 三、客户端 / 桌面软件开发
        "cpp-msvc",         // Visual C++ 运行库 + MinGW / MSVC
        "qt",               // Qt
        "flutter",          // Flutter SDK
        "android-sdk",      // Android SDK
        "xcode",            // Xcode（macOS 专属）
        // 四、数据库环境
        "mysql-mariadb",    // MySQL / MariaDB
        "postgresql",       // PostgreSQL
        "redis",            // Redis
        "mongodb",          // MongoDB
        "sqlserver",        // SQL Server
        // 五、运维、容器、工具环境
        "docker",           // Docker
        "git",              // Git
        "wsl2",             // WSL2
        "nginx",            // Nginx
        // 六、嵌入式、硬件开发
        "arduino",          // Arduino
        "stm32-keil",       // STM32 Keil MDK
        "raspberrypi",      // Raspberry Pi Linux
        // 七、常用开发 IDE
        "ide-common",       // VS Code / IDEA / PyCharm / WebStorm / CLion / Android Studio
    ];

    [Fact]
    public void All_builtin_templates_parse_successfully()
    {
        Assert.True(Directory.Exists(TemplatesDir), $"模板目录不存在：{TemplatesDir}");
        var files = Directory.EnumerateFiles(TemplatesDir, "*.yaml").ToList();
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            // 解析失败会抛出带行号的 InvalidDataException
            var spec = EnvTemplateParser.Parse(File.ReadAllText(file));
            Assert.False(string.IsNullOrWhiteSpace(spec.Id), $"{file} 缺少 id");
            Assert.False(string.IsNullOrWhiteSpace(spec.Name), $"{file} 缺少 name");
        }
    }

    [Fact]
    public void Builtin_template_ids_are_unique()
    {
        var ids = Directory.EnumerateFiles(TemplatesDir, "*.yaml")
            .Select(f => EnvTemplateParser.Parse(File.ReadAllText(f)).Id)
            .ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Environment_checklist_is_fully_covered()
    {
        var present = Directory.EnumerateFiles(TemplatesDir, "*.yaml")
            .Select(f => EnvTemplateParser.Parse(File.ReadAllText(f)).Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = RequiredTemplateIds.Where(id => !present.Contains(id)).ToList();
        Assert.True(missing.Count == 0, $"清单要求的环境模板缺失：{string.Join(", ", missing)}");
    }

    [Fact]
    public void Github_source_templates_declare_deploy_warning()
    {
        // 下载源为 GitHub 的模板（实测此网络下连接极慢）必须声明 deployWarning，
        // 部署时 UI 会先弹确认框提示用户准备加速器。
        var templates = Directory.EnumerateFiles(TemplatesDir, "*.yaml")
            .Select(f => EnvTemplateParser.Parse(File.ReadAllText(f)))
            .ToList();

        var githubIds = new[] { "git", "deno-bun", "flutter", "cpp-msvc" };
        foreach (var id in githubIds)
        {
            var spec = templates.Single(t => t.Id == id);
            Assert.False(string.IsNullOrWhiteSpace(spec.DeployWarning), $"{id} 缺少 deployWarning");
        }
    }
}
