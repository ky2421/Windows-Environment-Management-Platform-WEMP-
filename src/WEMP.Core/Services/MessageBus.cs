using System.Collections.Concurrent;

namespace WEMP.Core.Services;

/// <summary>
/// 进程内消息总线实现。线程安全；订阅与发布在调用线程上执行，
/// UI 场景请自行通过 Dispatcher 调度到主线程。
/// </summary>
public sealed class MessageBus : Abstractions.IMessageBus
{
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();

    public void Publish<T>(T message) where T : class
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!_handlers.TryGetValue(typeof(T), out var handlers) || handlers.Count == 0)
        {
            return;
        }

        // 快照遍历，避免发布过程中订阅/退订导致集合变更异常
        Delegate[] snapshot;
        lock (_gate)
        {
            snapshot = handlers.ToArray();
        }

        foreach (var handler in snapshot)
        {
            ((Action<T>)handler)(message);
        }
    }

    public IDisposable Subscribe<T>(Action<T> handler) where T : class
    {
        ArgumentNullException.ThrowIfNull(handler);

        var handlers = _handlers.GetOrAdd(typeof(T), _ => new List<Delegate>());
        lock (_gate)
        {
            handlers.Add(handler);
        }
        return new Unsubscriber<T>(this, handler);
    }

    private void Unsubscribe<T>(Action<T> handler) where T : class
    {
        if (!_handlers.TryGetValue(typeof(T), out var handlers))
        {
            return;
        }

        lock (_gate)
        {
            handlers.Remove(handler);
        }
    }

    private sealed class Unsubscriber<T>(MessageBus owner, Action<T> handler) : IDisposable where T : class
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            owner.Unsubscribe(handler);
        }
    }
}
