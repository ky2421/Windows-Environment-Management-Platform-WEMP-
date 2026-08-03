using WEMP.Optimization.Execution;

namespace WEMP.Core.Tests;

/// <summary>优化执行器工厂测试：知识库类别 → 执行器映射完整，新增类别不会漏注册。</summary>
public class OptimizationActionFactoryTests
{
    private static readonly OptimizationActionFactory Factory = new(
    [
        new ServiceAction(),
        new RegistryAction(),
        new ScheduledTaskAction(),
        new WindowsFeatureAction(),
    ]);

    [Theory]
    [InlineData("service", typeof(ServiceAction))]
    [InlineData("registry", typeof(RegistryAction))]
    [InlineData("game", typeof(RegistryAction))] // game 复用注册表执行器
    [InlineData("scheduled-task", typeof(ScheduledTaskAction))]
    [InlineData("windows-feature", typeof(WindowsFeatureAction))]
    public void Get_maps_category_to_action(string category, Type expectedType)
    {
        var action = Factory.Get(category);

        Assert.IsType(expectedType, action);
    }

    [Fact]
    public void Get_throws_for_unknown_category()
    {
        Assert.Throws<NotSupportedException>(() => Factory.Get("unknown-category"));
    }
}
