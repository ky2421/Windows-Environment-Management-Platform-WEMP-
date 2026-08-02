using System.IO;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Serilog;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;

namespace WEMP.Optimization.Seeding;

/// <summary>
/// 优化知识库种子同步：将嵌入的 optimization-items.json 同步到数据库。
/// 幂等：按 Code 判断，缺失插入，已存在仅更新名称等展示字段，不覆盖用户自定义。
/// </summary>
public sealed class OptimizationSeedService(WempDbContext db)
{
    private const string ResourceSuffix = "optimization-items.json";

    private static readonly JsonSerializerOptions KbJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<int> EnsureSeedAsync(CancellationToken cancellationToken = default)
    {
        var resourceName = Assembly.GetExecutingAssembly()
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(ResourceSuffix, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"嵌入资源缺失：{ResourceSuffix}");

        await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)!;

        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(cancellationToken);

        // 知识库 JSON 使用 camelCase，Web 默认大小写不敏感
        var kb = JsonSerializer.Deserialize<OptimizationKb>(json, KbJsonOptions)
            ?? throw new InvalidOperationException("知识库 JSON 解析失败");

        var existing = await db.OptimizationItems
            .ToDictionaryAsync(i => i.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var added = 0;
        foreach (var item in kb.Items)
        {
            if (existing.TryGetValue(item.Code, out var dbItem))
            {
                dbItem.Name = item.Name;
                dbItem.Category = item.Category;
                dbItem.Principle = item.Principle;
                dbItem.Risk = item.Risk;
                dbItem.Recommendation = item.Recommendation;
                dbItem.IsRecoverable = item.IsRecoverable;
                dbItem.TargetJson = item.TargetJson;
                dbItem.KbVersion = kb.KbVersion;
                continue;
            }

            item.KbVersion = kb.KbVersion;
            db.OptimizationItems.Add(item);
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            Log.Information("优化知识库同步完成：新增 {Added} 条，共 {Total} 条", added, kb.Items.Length);
        }

        return added;
    }
}

/// <summary>知识库 JSON 结构。</summary>
public sealed class OptimizationKb
{
    public int KbVersion { get; set; } = 1;

    public OptimizationItem[] Items { get; set; } = [];
}
