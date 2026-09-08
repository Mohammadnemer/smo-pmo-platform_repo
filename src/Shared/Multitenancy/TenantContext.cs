namespace SmoPmo.Shared.Multitenancy;

public sealed class TenantContext : ITenantContext
{
    public Guid TenantId { get; set; }
}
