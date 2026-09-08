using SmoPmo.Shared.Messaging;

namespace SmoPmo.Shared.Integration;

/// <summary>
/// Read-only cross-module lookups. A module must not project-reference another module
/// (architecture-mvp.md §9), so the *contract* for "does this id exist, and what's its
/// display name" has to live somewhere every module already depends on — here in /Shared —
/// while the *handler* stays owned by whichever module holds the table (SMO answers
/// <see cref="GetInitiativeSummaryQuery"/>, PMO answers the other two).
///
/// Roll-up (X1) is the first caller: it links SMO initiatives to PMO programs/projects
/// without ever touching their tables directly, using these queries both to validate the
/// ids it's given and to resolve display names for the initiative ↔ delivery aggregate
/// read. The roll-up worker (X2) will reuse the same seam to walk the health graph.
/// </summary>
public sealed record GetInitiativeSummaryQuery(Guid InitiativeId) : ICommand<InitiativeSummary?>;

public sealed record InitiativeSummary(Guid Id, string Name, string? NameAr);

public sealed record GetProgramSummaryQuery(Guid ProgramId) : ICommand<ProgramSummary?>;

public sealed record ProgramSummary(Guid Id, string Name);

public sealed record GetProjectSummaryQuery(Guid ProjectId) : ICommand<ProjectSummary?>;

public sealed record ProjectSummary(Guid Id, string Name);
