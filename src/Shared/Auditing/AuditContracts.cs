using SmoPmo.Shared.Messaging;

namespace SmoPmo.Shared.Auditing;

/// <summary>
/// One recorded change, produced by a module's SaveChanges interceptor.
///
/// The audit *log table* is owned by the Platform module (non-negotiable: one
/// owner per table). Other modules therefore cannot write to it directly — they
/// describe what changed and let Platform persist it. See <see cref="EntitiesChangedEvent"/>.
/// </summary>
public sealed record AuditRecord(
    string Module,
    string EntityName,
    Guid EntityId,
    string Action,
    string? Details);

/// <summary>
/// Raised after a module's changes are committed. Platform subscribes and writes
/// the audit rows; publishing is a no-op when nobody is listening, so a module
/// stays independently testable.
/// </summary>
public sealed record EntitiesChangedEvent(Guid TenantId, IReadOnlyList<AuditRecord> Records) : IDomainEvent;
