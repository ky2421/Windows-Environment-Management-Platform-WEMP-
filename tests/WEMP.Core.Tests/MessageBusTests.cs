using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using WEMP.Core.Abstractions;
using WEMP.Core.Services;

namespace WEMP.Core.Tests;

/// <summary>消息总线测试：订阅/退订、类型隔离、快照语义与并发安全。</summary>
public class MessageBusTests
{
    private sealed record SampleMessage(string Text);

    private sealed record OtherMessage(int Value);

    [Fact]
    public void Subscribe_receives_published_message()
    {
        var bus = new MessageBus();
        string? received = null;
        using (bus.Subscribe<SampleMessage>(m => received = m.Text))
        {
            bus.Publish(new SampleMessage("hello"));
        }

        Assert.Equal("hello", received);
    }

    [Fact]
    public void Multiple_subscribers_all_receive()
    {
        var bus = new MessageBus();
        var received = new List<string>();
        using (bus.Subscribe<SampleMessage>(m => received.Add($"a:{m.Text}")))
        using (bus.Subscribe<SampleMessage>(m => received.Add($"b:{m.Text}")))
        {
            bus.Publish(new SampleMessage("x"));
        }

        Assert.Equal(["a:x", "b:x"], received);
    }

    [Fact]
    public void Unsubscribe_stops_receiving()
    {
        var bus = new MessageBus();
        var count = 0;
        var token = bus.Subscribe<SampleMessage>(_ => count++);

        token.Dispose();
        bus.Publish(new SampleMessage("ignored"));

        Assert.Equal(0, count);
    }

    [Fact]
    public void Dispose_twice_is_idempotent()
    {
        var bus = new MessageBus();
        var count = 0;
        var token = bus.Subscribe<SampleMessage>(_ => count++);

        token.Dispose();
        token.Dispose();
        bus.Publish(new SampleMessage("x"));

        Assert.Equal(0, count);
    }

    [Fact]
    public void Publish_with_no_subscribers_is_noop()
    {
        var bus = new MessageBus();

        bus.Publish(new SampleMessage("no-subscribers"));
    }

    [Fact]
    public void Messages_of_different_types_do_not_cross()
    {
        var bus = new MessageBus();
        string? sampleReceived = null;
        var otherCount = 0;

        using (bus.Subscribe<SampleMessage>(m => sampleReceived = m.Text))
        using (bus.Subscribe<OtherMessage>(m => otherCount = m.Value))
        {
            bus.Publish(new OtherMessage(42));
        }

        Assert.Null(sampleReceived);
        Assert.Equal(42, otherCount);
    }

    [Fact]
    public void Unsubscribe_during_publish_uses_snapshot()
    {
        var bus = new MessageBus();
        IDisposable? token = null;
        var calls = new List<string>();

        token = bus.Subscribe<SampleMessage>(m =>
        {
            calls.Add($"first:{m.Text}");
            token!.Dispose(); // 发布过程中退订自己
        });
        using (bus.Subscribe<SampleMessage>(m => calls.Add($"second:{m.Text}")))
        {
            bus.Publish(new SampleMessage("snapshot"));
            // 快照语义：发布中的订阅者集合已固定，两次都收到
            bus.Publish(new SampleMessage("again"));
        }

        Assert.Equal(
            ["first:snapshot", "second:snapshot", "second:again"],
            calls);
    }

    [Fact]
    public void Publish_null_throws_ArgumentNullException()
    {
        var bus = new MessageBus();

        Assert.Throws<ArgumentNullException>(() => bus.Publish<SampleMessage>(null!));
    }

    [Fact]
    public void Subscribe_null_throws_ArgumentNullException()
    {
        var bus = new MessageBus();

        Assert.Throws<ArgumentNullException>(() => bus.Subscribe<SampleMessage>(null!));
    }

    [Fact]
    public async Task Concurrent_publish_and_subscribe_is_safe()
    {
        var bus = new MessageBus();
        var received = new ConcurrentBag<string>();
        const int threadCount = 8;
        const int messagesPerThread = 200;

        using (bus.Subscribe<SampleMessage>(m => received.Add(m.Text)))
        {
            var tasks = Enumerable.Range(0, threadCount)
                .Select(i => Task.Run(() =>
                {
                    for (var j = 0; j < messagesPerThread; j++)
                    {
                        bus.Publish(new SampleMessage($"t{i}-{j}"));
                    }
                }))
                .ToArray();
            await Task.WhenAll(tasks);
        }

        Assert.Equal(threadCount * messagesPerThread, received.Count);
        Assert.Equal(
            threadCount * messagesPerThread,
            received.Distinct().Count());
    }

    [Fact]
    public void Registered_via_di_as_singleton()
    {
        var provider = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddWempCore()
            .BuildServiceProvider();

        var first = provider.GetService(typeof(IMessageBus));
        var second = provider.GetService(typeof(IMessageBus));

        Assert.NotNull(first);
        Assert.Same(first, second);
    }
}
