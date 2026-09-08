namespace SmoPmo.Rollup;

/// <summary>
/// Entity ↔ contract translation, same role as SmoMapping/PmoMapping: kept in one place so
/// no endpoint can accidentally project a tenant id.
/// </summary>
internal static class RollupMapping
{
    public static void Apply(this InitiativeDeliveryLink entity, InitiativeDeliveryLinkWriteModel model)
    {
        entity.InitiativeId = model.InitiativeId;
        entity.ProgramId = model.ProgramId;
        entity.ProjectId = model.ProjectId;
        entity.IsPrimary = model.IsPrimary;
        entity.ContributionWeight = model.ContributionWeight ?? 100m;
    }

    public static InitiativeDeliveryLinkResponse ToResponse(this InitiativeDeliveryLink e) => new(
        e.Id, e.InitiativeId, e.ProgramId, e.ProjectId, e.IsPrimary, e.ContributionWeight);
}
