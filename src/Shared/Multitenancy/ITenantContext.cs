namespace SmoPmo.Shared.Multitenancy;

/// <summary>
/// The tenant of the current request. Resolved from the JWT and pushed onto the
/// PostgreSQL connection (SET app.tenant_id) by the request pipeline in build session B1.
///
/// Declared in /Shared so every module can depend on the seam without depending on the
/// Platform module that populates it.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; set; }
}
