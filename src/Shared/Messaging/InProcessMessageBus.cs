using Microsoft.Extensions.DependencyInjection;

namespace SmoPmo.Shared.Messaging;

public sealed class InProcessMessageBus : IMessageBus
{
    private readonly IServiceProvider _serviceProvider;

    public InProcessMessageBus(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task SendAsync(ICommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());
        var handler = _serviceProvider.GetRequiredService(handlerType);
        var method = handlerType.GetMethod("HandleAsync", new[] { command.GetType(), typeof(CancellationToken) })!;
        return (Task)method.Invoke(handler, new object?[] { command, ct })!;
    }

    public Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResult));
        var handler = _serviceProvider.GetRequiredService(handlerType);
        var method = handlerType.GetMethod("HandleAsync", new[] { command.GetType(), typeof(CancellationToken) })!;
        return (Task<TResult>)method.Invoke(handler, new object?[] { command, ct })!;
    }

    public Task PublishAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());

        // OfType<object>() rather than ToList(): GetServices is typed as IEnumerable<object?>,
        // and an event with no subscribers must publish as a no-op, not throw.
        var handlers = _serviceProvider.GetServices(handlerType).OfType<object>().ToList();
        return InvokeHandlersAsync(handlers, domainEvent, ct);
    }

    private static async Task InvokeHandlersAsync(IReadOnlyCollection<object> handlers, IDomainEvent domainEvent, CancellationToken ct)
    {
        foreach (var handler in handlers)
        {
            var method = handler.GetType().GetMethod("HandleAsync", new[] { domainEvent.GetType(), typeof(CancellationToken) })!;
            await (Task)method.Invoke(handler, new object?[] { domainEvent, ct })!;
        }
    }
}
