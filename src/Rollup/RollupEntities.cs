using SmoPmo.Platform;

namespace SmoPmo.Rollup;

/// <summary>
/// The M:N link between an SMO Initiative and its PMO delivery vehicle — a Program *or* a
/// Project, never both (architecture-mvp.md §5: "one initiative fulfilled by many
/// programs/projects... with a primary flag + contribution_weight for proportional
/// attribution"). Roll-up owns this table but never project-references SMO or PMO, so
/// <see cref="InitiativeId"/>/<see cref="ProgramId"/>/<see cref="ProjectId"/> are bare ids
/// resolved through the cross-module query seam in /Shared (see CrossModuleQueries) rather
/// than EF navigation properties.
/// </summary>
public sealed class InitiativeDeliveryLink : BaseEntity
{
    public Guid InitiativeId { get; set; }
    public Guid? ProgramId { get; set; }
    public Guid? ProjectId { get; set; }

    /// <summary>Marks the main delivery vehicle for this initiative. At most one primary
    /// link per initiative — enforced in the endpoint, the same tier B5/B6 validate parent
    /// references at.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Percentage attribution the roll-up worker (X2) will use to weight this
    /// delivery vehicle's health when computing the initiative's stored health.</summary>
    public decimal ContributionWeight { get; set; } = 100m;
}
