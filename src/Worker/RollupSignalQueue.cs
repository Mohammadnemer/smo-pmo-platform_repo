using System.Collections.Concurrent;
using SmoPmo.Shared.Integration;
using SmoPmo.Shared.Messaging;

namespace SmoPmo.Worker;

/// <summary>
/// The debounce buffer behind "debounced domain events in a background worker"
/// (architecture-mvp.md §5): every <see cref="PmoDeliveryHealthInputsChanged"/> signal for a
/// (tenant, project) pair just overwrites the pair's last-seen timestamp, so editing the same
/// project 50 times in a row still recomputes it once, whenever nothing new has arrived for
/// at least the configured debounce window (see <see cref="RollupWorkerOptions"/>).
/// </summary>
internal sealed class RollupSignalQueue
{
    private readonly ConcurrentDictionary<(Guid TenantId, Guid ProjectId), DateTimeOffset> _lastSeen = new();

    public void Signal(Guid tenantId, Guid projectId) => _lastSeen[(tenantId, projectId)] = DateTimeOffset.UtcNow;

    /// <summary>
    /// Pops every (tenant, project) pair that has gone quiet for at least
    /// <paramref name="debounceWindow"/>. Passing <see cref="TimeSpan.Zero"/> drains
    /// everything currently pending immediately — what tests use to make the "Done when" line
    /// deterministic without sleeping on a real timer.
    /// </summary>
    public IReadOnlyList<(Guid TenantId, Guid ProjectId)> DequeueDue(TimeSpan debounceWindow)
    {
        var now = DateTimeOffset.UtcNow;
        var due = new List<(Guid, Guid)>();

        foreach (var key in _lastSeen.Keys)
        {
            if (_lastSeen.TryGetValue(key, out var lastSeen) && now - lastSeen >= debounceWindow)
            {
                // Re-check-and-remove: a signal that lands between the read above and this
                // removal simply survives to the next tick rather than being dropped.
                if (_lastSeen.TryRemove(new KeyValuePair<(Guid, Guid), DateTimeOffset>(key, lastSeen)))
                {
                    due.Add(key);
                }
            }
        }

        return due;
    }
}

/// <summary>Bridges the domain event to the queue above. Registered as the DI handler for
/// <see cref="PmoDeliveryHealthInputsChanged"/> so PMO's publish (through <c>IMessageBus</c>)
/// reaches the worker without either side referencing the other's module.</summary>
internal sealed class RollupSignalEventHandler : IDomainEventHandler<PmoDeliveryHealthInputsChanged>
{
    private readonly RollupSignalQueue _queue;

    public RollupSignalEventHandler(RollupSignalQueue queue)
    {
        _queue = queue;
    }

    public Task HandleAsync(PmoDeliveryHealthInputsChanged domainEvent, CancellationToken ct = default)
    {
        _queue.Signal(domainEvent.TenantId, domainEvent.ProjectId);
        return Task.CompletedTask;
    }
}
