using NeoEditor.Messaging;

namespace NeoEditor.Messaging.Tests;

/// <summary>
/// Validates the IMessageBus abstraction contract.
/// The actual implementation (wrapping CommunityToolkit.Mvvm) is tested in Infra.Tests.
/// </summary>
public class MessageBusTests
{
    private sealed class TestMessageBus : IMessageBus
    {
        private readonly Dictionary<Type, List<object>> _handlers = new();

        public void Send<T>(T message) where T : class
        {
            if (_handlers.TryGetValue(typeof(T), out var list))
            {
                foreach (var h in list.OfType<Action<T>>())
                    h(message);
            }
        }

        public void Register<T>(object recipient, Action<T> handler) where T : class
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
            {
                list = new List<object>();
                _handlers[typeof(T)] = list;
            }
            list.Add(handler);
        }

        public void Unregister<T>(object recipient) where T : class
        {
            _handlers.Remove(typeof(T));
        }

        public void UnregisterAll(object recipient)
        {
            _handlers.Clear();
        }
    }

    [Fact]
    public void Send_ShouldDeliverMessageToRegisteredHandler()
    {
        var bus = new TestMessageBus();
        string? received = null;
        bus.Register<string>(this, msg => received = msg);

        bus.Send("hello");

        Assert.Equal("hello", received);
    }

    [Fact]
    public void Send_ShouldNotDeliverToUnregisteredHandlers()
    {
        var bus = new TestMessageBus();
        string? received = null;
        bus.Register<string>(this, msg => received = msg);
        bus.Unregister<string>(this);

        bus.Send("hello");

        Assert.Null(received);
    }

    [Fact]
    public void UnregisterAll_ShouldClearAllHandlers()
    {
        var bus = new TestMessageBus();
        string? receivedStr = null;
        bus.Register<DummyMessage>(this, msg => receivedStr = msg.Text);

        bus.UnregisterAll(this);
        bus.Send(new DummyMessage("test"));

        Assert.Null(receivedStr);
    }

    private record DummyMessage(string Text);
}
