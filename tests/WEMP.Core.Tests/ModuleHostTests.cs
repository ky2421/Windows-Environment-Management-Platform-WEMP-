using Microsoft.Extensions.DependencyInjection;
using WEMP.Core.Abstractions;
using WEMP.Core.Services;

namespace WEMP.Core.Tests;

/// <summary>模块宿主测试：程序集扫描、排序、生命周期顺序与 DI 构造注入。</summary>
public class ModuleHostTests
{
    private static readonly object EventGate = new();
    private static readonly List<string> EventLog = [];

    internal static void Log(string entry)
    {
        lock (EventGate)
        {
            EventLog.Add(entry);
        }
    }

    private static List<string> DrainEvents()
    {
        lock (EventGate)
        {
            var copy = EventLog.ToList();
            EventLog.Clear();
            return copy;
        }
    }

    private static ModuleHost CreateHost()
        => new(new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddSingleton<TestDependency>()
            .BuildServiceProvider());

    [Fact]
    public void LoadFromAssemblies_scans_and_sorts_modules_by_name()
    {
        var host = CreateHost();

        host.LoadFromAssemblies(typeof(ModuleA).Assembly);

        // 测试程序集含 4 个模块（A/C/M/Z），按名称序排列
        Assert.Equal(
            ["WEMP.A", "WEMP.C", "WEMP.M", "WEMP.Z"],
            host.Modules.Select(m => m.Name).ToList());
    }

    [Fact]
    public void LoadFromAssemblies_ignores_abstract_and_interface_types()
    {
        var host = CreateHost();

        host.LoadFromAssemblies(typeof(ModuleA).Assembly);

        // 抽象基类 AbstractModule 与接口 ITestModule 均被忽略
        Assert.DoesNotContain(host.Modules, m => m.GetType() == typeof(AbstractModule));
        Assert.All(host.Modules, m => Assert.False(m.GetType().IsAbstract));
    }

    [Fact]
    public void LoadFromAssemblies_supports_constructor_injection()
    {
        var host = CreateHost();

        host.LoadFromAssemblies(typeof(ModuleA).Assembly);

        Assert.Single(host.Modules.OfType<ModuleC>());
        Assert.NotNull(host.Modules.OfType<ModuleC>().Single().Dependency);
    }

    [Fact]
    public void LoadFromAssemblies_multiple_assemblies_deduplicates_nothing_extra()
    {
        var host = CreateHost();

        // 第二个程序集（WEMP.Core）不含 IModule 实现 → 模块数不变
        host.LoadFromAssemblies(typeof(ModuleA).Assembly, typeof(IModule).Assembly);

        Assert.Equal(4, host.Modules.Count);
    }

    [Fact]
    public void LoadFromAssemblies_null_throws_ArgumentNullException()
    {
        var host = CreateHost();

        Assert.Throws<ArgumentNullException>(() => host.LoadFromAssemblies(null!));
    }

    [Fact]
    public async Task Lifecycle_runs_init_activate_shutdown_in_expected_order()
    {
        var host = CreateHost();
        host.LoadFromAssemblies(typeof(ModuleA).Assembly);

        await host.InitializeAllAsync();
        Assert.Equal(
            ["init:WEMP.A", "init:WEMP.C", "init:WEMP.M", "init:WEMP.Z"],
            DrainEvents());

        await host.ActivateAllAsync();
        Assert.Equal(
            ["activate:WEMP.A", "activate:WEMP.C", "activate:WEMP.M", "activate:WEMP.Z"],
            DrainEvents());

        // 逆序关闭：后加载的先释放
        await host.ShutdownAllAsync();
        Assert.Equal(
            ["shutdown:WEMP.Z", "shutdown:WEMP.M", "shutdown:WEMP.C", "shutdown:WEMP.A"],
            DrainEvents());
    }

    [Fact]
    public async Task Initialize_all_modules_receives_shared_service_provider()
    {
        var host = CreateHost();
        host.LoadFromAssemblies(typeof(ModuleA).Assembly);

        await host.InitializeAllAsync();

        // 各模块 InitializeAsync 收到的 IServiceProvider 能解析已注册服务
        Assert.All(host.Modules.OfType<ModuleA>(), m => Assert.NotNull(m.Services?.GetService(typeof(TestDependency))));
        DrainEvents();
    }

    [Fact]
    public void Registered_via_di_as_singleton()
    {
        var provider = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddWempCore()
            .BuildServiceProvider();

        var first = provider.GetService(typeof(IModuleHost));
        var second = provider.GetService(typeof(IModuleHost));

        Assert.NotNull(first);
        Assert.Same(first, second);
    }
}

/// <summary>供模块构造注入的测试依赖。</summary>
public sealed class TestDependency
{
}

/// <summary>模块宿主扫描用的测试模块基类（抽象 → 应被忽略）。</summary>
public abstract class AbstractModule : IModule
{
    public abstract string Name { get; }

    public string DisplayName => Name;

    public Version Version => new(1, 0, 0);

    public IReadOnlyList<PageRegistration> Pages => [];

    public virtual Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ModuleHostTests.Log($"init:{Name}");
        return Task.CompletedTask;
    }

    public Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        ModuleHostTests.Log($"activate:{Name}");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        ModuleHostTests.Log($"shutdown:{Name}");
        return Task.CompletedTask;
    }
}

/// <summary>测试模块接口（接口 → 应被忽略）。</summary>
public interface ITestModule : IModule
{
}

public sealed class ModuleA : AbstractModule
{
    public override string Name => "WEMP.A";

    public IServiceProvider? Services { get; private set; }

    public override Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        Services = services;
        return base.InitializeAsync(services, cancellationToken);
    }
}

public sealed class ModuleM : AbstractModule
{
    public override string Name => "WEMP.M";
}

public sealed class ModuleZ : AbstractModule
{
    public override string Name => "WEMP.Z";
}

public sealed class ModuleC : IModule
{
    public ModuleC(TestDependency dependency)
    {
        Dependency = dependency;
    }

    public TestDependency Dependency { get; }

    public string Name => "WEMP.C";

    public string DisplayName => Name;

    public Version Version => new(1, 0, 0);

    public IReadOnlyList<PageRegistration> Pages => [];

    public Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ModuleHostTests.Log($"init:{Name}");
        return Task.CompletedTask;
    }

    public Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        ModuleHostTests.Log($"activate:{Name}");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        ModuleHostTests.Log($"shutdown:{Name}");
        return Task.CompletedTask;
    }
}
