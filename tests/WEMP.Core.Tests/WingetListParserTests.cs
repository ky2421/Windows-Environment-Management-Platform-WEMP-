using System.Text;
using WEMP.PackageManagement.Winget;

namespace WEMP.Core.Tests;

/// <summary>winget 表格输出解析测试（按 winget 固定列宽语义构造样例）。</summary>
public class WingetListParserTests
{
    // 列宽与真实 winget 输出一致（名称/ID/版本/可用/来源）
    private const int NameWidth = 60;
    private const int IdWidth = 90;
    private const int VersionWidth = 20;
    private const int AvailableWidth = 16;

    private static string Row(string name, string id, string version, string available, string source)
        => name.PadRight(NameWidth)
           + id.PadRight(IdWidth)
           + version.PadRight(VersionWidth)
           + available.PadRight(AvailableWidth)
           + source;

    private static string BuildSample()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Row("名称", "ID", "版本", "可用", "来源"));
        sb.AppendLine(new string('-', NameWidth + IdWidth + VersionWidth + AvailableWidth + 12));
        sb.AppendLine(Row("5EClient 8.2.7", "5E.5EClient", "8.2.7", "", "winget"));
        sb.AppendLine(Row("AMD Radeon Software", "MSIX\\AdvancedMicroDevicesInc-RSXCM_22.10.0.0_x64__v2es6h43hjn86", "22.10.0.0", "", ""));
        sb.AppendLine(Row("AVC 编码器视频扩展", "MSIX\\Microsoft.AVCEncoderVideoExtension_1.1.23.0_x64__8wekyb3d8bbwe", "1.1.23.0", "", ""));
        sb.AppendLine(Row("Google Chrome", "Google.Chrome", "136.0.7103.177", "137.0.7151.52", "winget"));
        return sb.ToString();
    }

    [Fact]
    public void Parse_extracts_all_columns()
    {
        var packages = WingetListParser.Parse(BuildSample());

        Assert.Equal(4, packages.Count);

        var chrome = packages.First(p => p.Id == "Google.Chrome");
        Assert.Equal("Google Chrome", chrome.Name);
        Assert.Equal("136.0.7103.177", chrome.Version);
        Assert.Equal("137.0.7151.52", chrome.Available);
        Assert.Equal("winget", chrome.Source);
    }

    [Fact]
    public void Parse_handles_missing_available_and_source()
    {
        var packages = WingetListParser.Parse(BuildSample());

        var amd = packages.First(p => p.Id.Contains("AdvancedMicroDevices"));
        Assert.Null(amd.Available);
        Assert.Equal("", amd.Source);

        var chrome = packages.First(p => p.Id == "Google.Chrome");
        Assert.Equal("137.0.7151.52", chrome.Available);
    }

    [Fact]
    public void Parse_handles_unicode_names()
    {
        var packages = WingetListParser.Parse(BuildSample());

        var avc = packages.First(p => p.Id.Contains("AVCEncoder"));
        Assert.Equal("AVC 编码器视频扩展", avc.Name);
    }

    [Fact]
    public void Parse_returns_empty_when_no_header()
    {
        Assert.Empty(WingetListParser.Parse("no header here\nsome data"));
    }

    [Fact]
    public void Parse_returns_empty_on_empty_input()
    {
        Assert.Empty(WingetListParser.Parse(""));
    }
}
