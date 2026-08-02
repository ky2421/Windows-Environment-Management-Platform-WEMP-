using WEMP.PackageManagement.Models;

namespace WEMP.PackageManagement.Winget;

/// <summary>解析 winget list / upgrade 的固定列宽表格输出。</summary>
public static class WingetListParser
{
    /// <summary>从输出文本解析软件包列表。</summary>
    public static List<WingetPackage> Parse(string output)
    {
        var result = new List<WingetPackage>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // 定位表头（包含中文列名），确定各列起始位置
        var headerIndex = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("ID", StringComparison.Ordinal) && lines[i].Contains("版本", StringComparison.Ordinal))
            {
                headerIndex = i;
                break;
            }
        }

        if (headerIndex < 0)
        {
            return result;
        }

        var header = lines[headerIndex];
        var idStart = header.IndexOf("ID", StringComparison.Ordinal);
        var versionStart = header.IndexOf("版本", StringComparison.Ordinal);
        var availableStart = header.IndexOf("可用", StringComparison.Ordinal);
        // 兼容列名「来源」（旧版/测试样例）与「源」（真实 winget），取列起始位置
        var sourceStart = header.LastIndexOf("来源", StringComparison.Ordinal);
        if (sourceStart < 0)
        {
            sourceStart = header.LastIndexOf("源", StringComparison.Ordinal);
        }
        if (idStart < 0 || versionStart < 0 || sourceStart < 0)
        {
            return result;
        }

        // 表头行之后是分隔线，再之后是数据行
        for (var i = headerIndex + 2; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            result.Add(ParseLine(line, idStart, versionStart, availableStart, sourceStart));
        }

        return result;
    }

    private static WingetPackage ParseLine(string line, int idStart, int versionStart, int availableStart, int sourceStart)
    {
        var name = Slice(line, 0, idStart);
        var id = Slice(line, idStart, versionStart);
        var version = Slice(line, versionStart, availableStart);
        var available = Slice(line, availableStart, sourceStart);
        var source = line.Length > sourceStart ? line[sourceStart..].Trim() : "";

        return new WingetPackage(name, id, version, available.Length > 0 ? available : null, source);
    }

    private static string Slice(string line, int start, int end)
    {
        if (start >= line.Length)
        {
            return "";
        }

        var length = Math.Min(end, line.Length) - start;
        return length > 0 ? line.Substring(start, length).Trim() : "";
    }
}
