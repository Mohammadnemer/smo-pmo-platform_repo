namespace SmoPmo.Rollup;

// ───────────────────────────── Write models ─────────────────────────────

public sealed record InitiativeDeliveryLinkWriteModel(
    Guid InitiativeId,
    Guid? ProgramId,
    Guid? ProjectId,
    bool IsPrimary,
    decimal? ContributionWeight);

// ───────────────────────────── Read models ─────────────────────────────

public sealed record InitiativeDeliveryLinkResponse(
    Guid Id,
    Guid InitiativeId,
    Guid? ProgramId,
    Guid? ProjectId,
    bool IsPrimary,
    decimal ContributionWeight);

/// <summary>
/// The X1 aggregate read: an initiative and the programs/projects delivering it, with
/// display names resolved from SMO/PMO through the mediator rather than a table join —
/// there is no shared table to join across modules (architecture-mvp.md §9).
/// </summary>
public sealed record InitiativeDeliveryResponse(
    Guid InitiativeId,
    string InitiativeName,
    string? InitiativeNameAr,
    IReadOnlyList<DeliveryVehicleResponse> Programs,
    IReadOnlyList<DeliveryVehicleResponse> Projects);

public sealed record DeliveryVehicleResponse(
    Guid LinkId,
    Guid Id,
    string Name,
    bool IsPrimary,
    decimal ContributionWeight);
