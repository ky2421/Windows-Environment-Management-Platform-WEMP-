using Microsoft.EntityFrameworkCore;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;

namespace WEMP.GameMode.Detection;

/// <summary>
/// 自定义游戏库：用户运行时增删的游戏进程名，检测时与内置 <see cref="GameLibrary"/> 合并。
/// 维护进程名内存缓存，避免每次前台检测都查询数据库。
/// </summary>
public interface ICustomGameLibrary
{
    /// <summary>全部自定义条目（按名称排序）。</summary>
    IReadOnlyList<CustomGame> GetAll();

    /// <summary>自定义游戏库内容变化（新增/删除成功后）时触发，供检测器刷新缓存。</summary>
    event EventHandler? LibraryChanged;

    /// <summary>进程名（可含 .exe 后缀）是否命中自定义库。</summary>
    bool IsCustomGame(string processName);

    /// <summary>新增条目；进程名重复时抛出 <see cref="InvalidOperationException"/>。</summary>
    Task<CustomGame> AddAsync(string name, string processName, CancellationToken cancellationToken = default);

    /// <summary>按 Id 删除条目；不存在返回 false。</summary>
    Task<bool> RemoveAsync(int id, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class CustomGameLibraryService(IDbContextFactory<WempDbContext> dbFactory) : ICustomGameLibrary
{
    private readonly object _sync = new();
    private HashSet<string>? _cache;

    public event EventHandler? LibraryChanged;

    public IReadOnlyList<CustomGame> GetAll()
    {
        using var db = dbFactory.CreateDbContext();
        return db.CustomGames.OrderBy(g => g.Name).ToList();
    }

    public bool IsCustomGame(string processName)
    {
        var normalized = Normalize(processName);
        if (normalized is null)
        {
            return false;
        }

        return GetCache().Contains(normalized);
    }

    public async Task<CustomGame> AddAsync(
        string name, string processName, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(processName)
            ?? throw new InvalidOperationException("进程名不能为空");
        var trimmedName = name.Trim();
        if (trimmedName.Length == 0)
        {
            throw new InvalidOperationException("游戏名称不能为空");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var exists = await db.CustomGames
            .AnyAsync(g => g.ProcessName == normalized, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException($"进程 {normalized} 已在自定义游戏库中");
        }

        var game = new CustomGame
        {
            Name = trimmedName,
            ProcessName = normalized,
            AddedAt = DateTime.Now,
        };
        db.CustomGames.Add(game);
        await db.SaveChangesAsync(cancellationToken);
        RefreshCache();
        LibraryChanged?.Invoke(this, EventArgs.Empty);
        return game;
    }

    public async Task<bool> RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var game = await db.CustomGames.FindAsync([id], cancellationToken);
        if (game is null)
        {
            return false;
        }

        db.CustomGames.Remove(game);
        await db.SaveChangesAsync(cancellationToken);
        RefreshCache();
        LibraryChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private HashSet<string> GetCache()
    {
        lock (_sync)
        {
            using var db = dbFactory.CreateDbContext();
            return _cache ??= db.CustomGames
                .Select(g => g.ProcessName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void RefreshCache()
    {
        lock (_sync)
        {
            using var db = dbFactory.CreateDbContext();
            _cache = db.CustomGames
                .Select(g => g.ProcessName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string? Normalize(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return null;
        }

        var name = processName.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        return name.Length == 0 ? null : name;
    }
}
