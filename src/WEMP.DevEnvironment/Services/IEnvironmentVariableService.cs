namespace WEMP.DevEnvironment.Services;

/// <summary>
/// 环境变量读写抽象（scope：user 范围持久化到注册表）。
/// 测试中以内存实现替换。
/// </summary>
public interface IEnvironmentVariableService
{
    /// <summary>读取指定作用域的环境变量；不存在返回 null。</summary>
    string? GetValue(string name, string scope = "user");

    /// <summary>设置环境变量；value 为 null 时删除。返回修改前的原始值（回滚依据）。</summary>
    string? SetValue(string name, string? value, string scope = "user");
}
