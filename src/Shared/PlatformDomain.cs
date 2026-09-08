using System;
using System.Collections.Generic;

namespace SmoPmo.Platform;

public abstract class BaseEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
}

public sealed class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ExternalId { get; set; }

    public ICollection<OrgUnit> OrgUnits { get; } = new List<OrgUnit>();
    public ICollection<User> Users { get; } = new List<User>();
    public ICollection<Role> Roles { get; } = new List<Role>();
}

public sealed class OrgUnit : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }

    public Tenant? Tenant { get; set; }
    public OrgUnit? Parent { get; set; }
    public ICollection<OrgUnit> Children { get; } = new List<OrgUnit>();
    public ICollection<User> Users { get; } = new List<User>();
    public ICollection<Role> Roles { get; } = new List<Role>();
}

public sealed class User : BaseEntity
{
    public string ExternalId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Guid? OrgUnitId { get; set; }

    public Tenant? Tenant { get; set; }
    public OrgUnit? OrgUnit { get; set; }
    public ICollection<Role> Roles { get; } = new List<Role>();
}

public sealed class Role : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Tenant? Tenant { get; set; }
    public ICollection<User> Users { get; } = new List<User>();
    public ICollection<OrgUnit> OrgUnits { get; } = new List<OrgUnit>();
}

// Strategy / Perspective / Objective / KPI / Initiative used to live here. As of B5 the
// SMO module owns those entities and their tables (src/SMO/SmoEntities.cs) — see
// docs/sessions/B5.md. As of B6 the same is true of Portfolio / Program / Project /
// TaskItem / Dependency / RaidItem: the PMO module owns them (src/PMO/PmoEntities.cs) —
// see docs/sessions/B6.md.

public sealed class LookupItem : BaseEntity
{
    public string Category { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
}
