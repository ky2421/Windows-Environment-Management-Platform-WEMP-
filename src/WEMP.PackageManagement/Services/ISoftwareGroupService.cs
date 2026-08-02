using WEMP.Infrastructure.Data.Entities;

namespace WEMP.PackageManagement.Services;

/// <summary>软件分组管理服务：分组 CRUD 与一键安装组内软件。</summary>
public interface ISoftwareGroupService
{
    /// <summary>获取全部分组（含条目）。</summary>
    Task<IReadOnlyList<SoftwareGroup>> GetGroupsAsync(CancellationToken cancellationToken = default);

    /// <summary>创建分组。</summary>
    Task<SoftwareGroup> CreateGroupAsync(string name, string? description, CancellationToken cancellationToken = default);

    /// <summary>删除分组。</summary>
    Task DeleteGroupAsync(long groupId, CancellationToken cancellationToken = default);

    /// <summary>向分组添加软件包（包 ID 允许引用未安装软件）。</summary>
    Task AddItemAsync(long groupId, string packageId, string? displayName = null, CancellationToken cancellationToken = default);

    /// <summary>从分组移除条目。</summary>
    Task RemoveItemAsync(long itemId, CancellationToken cancellationToken = default);

    /// <summary>一键安装分组内全部软件（顺序执行，返回操作数）。</summary>
    Task<int> InstallGroupAsync(long groupId, CancellationToken cancellationToken = default);
}
