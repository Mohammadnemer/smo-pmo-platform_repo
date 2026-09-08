using Microsoft.Extensions.DependencyInjection;
using SmoPmo.Shared.Integration;
using SmoPmo.Shared.Messaging;
using SmoPmo.Shared.Multitenancy;

namespace SmoPmo.Worker;

/// <summary>
/// Drains whatever the debounce queue says is ready and, for each (tenant, project) pair,
/// sends one <see cref="RecomputeDeliveryHealthCommand"/> into the Roll-up module — the
/// actual graph walk (architecture-mvp.md §5) lives there, not here; the Worker's only job is
/// debouncing and giving each recompute the right tenant scope. Public (not internal) so
/// tests can resolve it directly from DI and force an immediate flush instead of waiting on
/// the real timer — see <see cref="RollupWorkerOptions"/>.
/// </summary>
public interface IRollupHealthProcessor
{
    Task ProcessDueAsync(TimeSpan debounceWindow, CancellationToken ct = default);
}

internal sealed class RollupHealthProcessor : IRollupHealthProcessor
{
    private readonly RollupSignalQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;

    public RollupHealthProcessor(RollupSignalQueue queue, IServiceScopeFactory scopeFactory)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
    }

    public async Task ProcessDueAsync(TimeSpan debounceWindow, CancellationToken ct = default)
    {
        foreach (var (tenantId, projectId) in _queue.DequeueDue(debounceWindow))
        {
            // A fresh scope per (tenant, project): the mediator resolves scoped handlers
            // (module DbContexts included), and this runs outside any HTTP request, so the
            // tenant has to be stamped onto this scope's ITenantContext explicitly — the same
            // move Program.cs's dev seed makes before touching a tenant-scoped DbContext.
            using var scope = _scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.SendAsync(new RecomputeDeliveryHealthCommand(projectId), ct);
        }
    }
}
