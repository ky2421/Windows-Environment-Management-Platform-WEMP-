namespace WEMP.Core.Abstractions;

/// <summary>
/// 跨模块消息总线：模块间通过发布/订阅实现松散耦合通信。
/// 同步查询请使用 DI 接口注入，异步通知请使用此总线。
/// </summary>
public interface IMessageBus
{
    /// <summary>发布一条消息给所有订阅者。</summary>
    void Publish<T>(T message) where T : class;

    /// <summary>订阅指定类型的消息，返回的 <see cref="IDisposable"/> 用于退订。</summary>
    IDisposable Subscribe<T>(Action<T> handler) where T : class;
}
