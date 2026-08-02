using System.Text;
using System.Text.RegularExpressions;

namespace WEMP.Backup.Services;

/// <summary>
/// 最小 glob 匹配器：支持 <c>*</c>（当前段）、<c>**</c>（跨段）、<c>?</c>（单字符），
/// 路径分隔符统一为 <c>/</c>。大小写不敏感（Windows 文件系统语义）。
/// </summary>
public static class GlobMatcher
{
    /// <summary>判断路径是否匹配任一模。</summary>
    public static bool IsMatch(string path, IReadOnlyList<string> patterns)
    {
        var normalized = path.Replace('\\', '/');
        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                continue;
            }

            if (Regex.IsMatch(normalized, ToRegex(pattern.Trim()), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>把 glob 模式转换为正则表达式（锚定整串匹配）。</summary>
    private static string ToRegex(string glob)
    {
        var normalized = glob.Replace('\\', '/');
        var sb = new StringBuilder("^");
        for (var i = 0; i < normalized.Length; i++)
        {
            var c = normalized[i];
            switch (c)
            {
                case '*':
                    if (i + 1 < normalized.Length && normalized[i + 1] == '*')
                    {
                        // ** 跨段
                        i++;
                        if (i + 1 < normalized.Length && normalized[i + 1] == '/')
                        {
                            i++;
                            sb.Append("(?:.*/)?");
                        }
                        else
                        {
                            sb.Append(".*");
                        }
                    }
                    else
                    {
                        sb.Append("[^/]*");
                    }

                    break;

                case '?':
                    sb.Append("[^/]");
                    break;

                default:
                    sb.Append(Regex.Escape(c.ToString()));
                    break;
            }
        }

        sb.Append('$');
        return sb.ToString();
    }
}
