namespace SmoPmo.Shared.Messaging;

/// <summary>
/// The single cross-module seam. Modules never call each other directly — they send
/// commands and publish domain events through this bus.
///
/// The in-process implementation (dispatch to handlers) is wired in /Api in build
/// session B4. At scale the same interface can be backed by Azure Service Bus without
/// touching any module code (architecture-mvp.md §5, §8).
/// </summary>
public interface IMessageBus
{
    Task SendAsync(ICommand command, CancellationToken ct = default);
    Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default);
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken ct = default);
}
