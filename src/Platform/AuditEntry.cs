namespace SmoPmo.Platform;

public sealed class AuditEntry : BaseEntity
{
    public string EntityName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }

    /// <summary>Which module reported the change. Added in B5, when SMO became the first
    /// module to audit through the mediator instead of writing this table itself.</summary>
    public string Module { get; set; } = "Platform";

    /// <summary>The changed row's primary key, so an audit trail can be read per record.</summary>
    public Guid EntityId { get; set; }
}
