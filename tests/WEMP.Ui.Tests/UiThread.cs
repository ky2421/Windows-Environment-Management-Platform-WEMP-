using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using Xunit;

// WPF 资源与 Application 单例：UI 测试必须串行执行
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace WEMP.Ui.Tests;

/// <summary>标记需在 UI 线程执行的测试（WPF 要求）。</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class StaFactAttribute : FactAttribute;

/// <summary>
/// 常驻 STA UI 线程：创建 Application 并加载 Dark 主题资源，Dispatcher 持续运行。
/// 所有测试通过 TaskScheduler marshal 到同一 UI 线程执行，
/// 保证 Application.Current.Dispatcher 可用且集合操作在 UI 线程完成。
/// </summary>
public static class UiThread
{
    private static readonly ManualResetEventSlim Ready = new();
    private static TaskScheduler? _scheduler;

    public static void Initialize()
    {
        if (_scheduler is not null)
        {
            return;
        }

        var thread = new Thread(() =>
        {
            var app = new Application();
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/WEMP.App;component/Themes/Dark.xaml"),
            });
            // Dispatcher.Run() 之前显式建立 DispatcherSynchronizationContext，
            // 否则 FromCurrentSynchronizationContext() 会因上下文为空而抛出。
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            _scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            Ready.Set();
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        Ready.Wait();
    }

    /// <summary>在 UI 线程执行异步委托并等待完成；异常原样传播。</summary>
    public static void Run(Action action) => RunAsync(() =>
    {
        action();
        return Task.CompletedTask;
    }).GetAwaiter().GetResult();

    public static Task RunAsync(Func<Task> action)
    {
        Initialize();
        return Task.Factory.StartNew(
                () => action(),
                CancellationToken.None,
                TaskCreationOptions.None,
                _scheduler!)
            .Unwrap();
    }

    /// <summary>同步执行并捕获异常（供测试断言）。</summary>
    public static void RunSafely(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }
}
