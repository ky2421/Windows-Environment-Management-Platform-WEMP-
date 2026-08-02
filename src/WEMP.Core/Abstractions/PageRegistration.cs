namespace WEMP.Core.Abstractions;

/// <summary>
/// 模块注册到主导航的页面描述。宿主据此动态构建导航菜单并解析视图。
/// </summary>
/// <param name="Key">页面唯一键。</param>
/// <param name="Title">导航显示标题。</param>
/// <param name="ViewModelType">ViewModel 类型（由 DI 容器解析）。</param>
/// <param name="ViewType">View 类型（由 DI 容器解析）。</param>
/// <param name="Order">导航排序，越小越靠前。</param>
public sealed record PageRegistration(
    string Key,
    string Title,
    Type ViewModelType,
    Type ViewType,
    int Order);
