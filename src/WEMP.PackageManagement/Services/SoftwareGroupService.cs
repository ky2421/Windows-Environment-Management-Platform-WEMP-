using Microsoft.EntityFrameworkCore;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;

namespace WEMP.PackageManagement.Services;

/// <summary>软件分组服务实现：分组 CRUD + 一键安装。</summary>
public sealed class SoftwareGroupService(
    IDbContextFactory<WempDbContext> dbFactory,
    IPackageManagerService packageManager) : ISoftwareGroupService
{
    public async Task<IReadOnlyList<SoftwareGroup>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.SoftwareGroups
            .Include(g => g.Items)
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => g.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<SoftwareGroup> CreateGroupAsync(
        string name, string? description, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var group = new SoftwareGroup
        {
            Name = name.Trim(),
            Description = description,
            SortOrder = await db.SoftwareGroups.CountAsync(cancellationToken) + 1,
            CreatedAt = DateTime.Now,
        };
        db.SoftwareGroups.Add(group);
        await db.SaveChangesAsync(cancellationToken);
        return group;
    }

    public async Task DeleteGroupAsync(long groupId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var group = await db.SoftwareGroups
            .Include(g => g.Items)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);
        if (group is not null)
        {
            db.SoftwareGroups.Remove(group); // 条目级联删除
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task AddItemAsync(
        long groupId, string packageId, string? displayName, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        packageId = packageId.Trim();
        var exists = await db.SoftwareGroupItems.AnyAsync(
            i => i.GroupId == groupId && i.PackageId == packageId, cancellationToken);
        if (exists)
        {
            return;
        }

        db.SoftwareGroupItems.Add(new SoftwareGroupItem
        {
            GroupId = groupId,
            PackageId = packageId,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? packageId : displayName,
            SortOrder = await db.SoftwareGroupItems.CountAsync(i => i.GroupId == groupId, cancellationToken) + 1,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveItemAsync(long itemId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var item = await db.SoftwareGroupItems.FindAsync([itemId], cancellationToken);
        if (item is not null)
        {
            db.SoftwareGroupItems.Remove(item);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> InstallGroupAsync(long groupId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var group = await db.SoftwareGroups
            .Include(g => g.Items)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);
        if (group is null)
        {
            return 0;
        }

        foreach (var item in group.Items.OrderBy(i => i.SortOrder))
        {
            await packageManager.InstallAsync(item.PackageId, null, cancellationToken);
        }

        return group.Items.Count;
    }
}
