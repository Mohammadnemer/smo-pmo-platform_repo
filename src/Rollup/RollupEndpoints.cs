using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SmoPmo.Shared.Integration;
using SmoPmo.Shared.Messaging;

namespace SmoPmo.Rollup;

/// <summary>
/// Authorization policy names the Roll-up endpoints ask for. The *matrix* behind them lives
/// in /Api (B3/AuthenticationExtensions) — the module names the policy, the composition
/// root decides who satisfies it, mirroring SmoPolicies/PmoPolicies.
/// </summary>
public static class RollupPolicies
{
    public const string Read = "RollupReadPolicy";
    public const string Write = "RollupWritePolicy";
}

/// <summary>
/// The Roll-up REST surface (X1): CRUD over the initiative ↔ delivery link, plus the one
/// aggregate read the session's "Done when" line asks for — an initiative listing the
/// programs and projects delivering it, with names resolved from SMO/PMO through the
/// mediator (<see cref="IMessageBus"/>) since Roll-up never project-references either
/// module (architecture-mvp.md §9).
/// </summary>
public static class RollupEndpoints
{
    public static IEndpointRouteBuilder MapRollupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/rollup").RequireAuthorization(RollupPolicies.Read);

        MapLinks(group);
        MapInitiativeDelivery(group);

        return endpoints;
    }

    // ───────────────────────────── Links (CRUD) ─────────────────────────────

    private static void MapLinks(RouteGroupBuilder group)
    {
        group.MapGet("/links", async (
            RollupDbContext db,
            CancellationToken ct,
            Guid? initiativeId = null,
            Guid? programId = null,
            Guid? projectId = null) =>
        {
            var entities = await db.Links
                .AsNoTracking()
                .Where(e => initiativeId == null || e.InitiativeId == initiativeId)
                .Where(e => programId == null || e.ProgramId == programId)
                .Where(e => projectId == null || e.ProjectId == projectId)
                .OrderByDescending(e => e.IsPrimary)
                .ToListAsync(ct);

            return Results.Ok(entities.Select(RollupMapping.ToResponse).ToList());
        });

        group.MapGet("/links/{id:guid}", async (Guid id, RollupDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Links.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
            return entity is null ? NotFound("delivery link", id) : Results.Ok(entity.ToResponse());
        });

        group.MapPost("/links", async (
            InitiativeDeliveryLinkWriteModel model,
            RollupDbContext db,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            if (RollupValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            if (await UnknownDeliveryParent(model, bus, ct) is { } problem)
            {
                return problem;
            }

            if (model.IsPrimary && await db.Links.AnyAsync(l => l.InitiativeId == model.InitiativeId && l.IsPrimary, ct))
            {
                return DuplicatePrimary();
            }

            var entity = new InitiativeDeliveryLink();
            entity.Apply(model);
            db.Links.Add(entity);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/rollup/links/{entity.Id}", entity.ToResponse());
        }).RequireAuthorization(RollupPolicies.Write);

        group.MapPut("/links/{id:guid}", async (
            Guid id,
            InitiativeDeliveryLinkWriteModel model,
            RollupDbContext db,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            if (RollupValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            var entity = await db.Links.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("delivery link", id);
            }

            if (await UnknownDeliveryParent(model, bus, ct) is { } problem)
            {
                return problem;
            }

            if (model.IsPrimary && await db.Links.AnyAsync(l => l.InitiativeId == model.InitiativeId && l.IsPrimary && l.Id != id, ct))
            {
                return DuplicatePrimary();
            }

            entity.Apply(model);
            await db.SaveChangesAsync(ct);

            return Results.Ok(entity.ToResponse());
        }).RequireAuthorization(RollupPolicies.Write);

        group.MapDelete("/links/{id:guid}", async (Guid id, RollupDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Links.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("delivery link", id);
            }

            db.Links.Remove(entity);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        }).RequireAuthorization(RollupPolicies.Write);
    }

    // ──────────────────────── Initiative delivery (aggregate read) ────────────────────────

    private static void MapInitiativeDelivery(RouteGroupBuilder group)
    {
        group.MapGet("/initiatives/{initiativeId:guid}/delivery", async (
            Guid initiativeId,
            RollupDbContext db,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var initiative = await bus.SendAsync(new GetInitiativeSummaryQuery(initiativeId), ct);
            if (initiative is null)
            {
                return NotFound("initiative", initiativeId);
            }

            var links = await db.Links
                .AsNoTracking()
                .Where(e => e.InitiativeId == initiativeId)
                .OrderByDescending(e => e.IsPrimary)
                .ToListAsync(ct);

            var programs = new List<DeliveryVehicleResponse>();
            foreach (var link in links.Where(l => l.ProgramId is not null))
            {
                var program = await bus.SendAsync(new GetProgramSummaryQuery(link.ProgramId!.Value), ct);
                if (program is not null)
                {
                    programs.Add(new DeliveryVehicleResponse(link.Id, program.Id, program.Name, link.IsPrimary, link.ContributionWeight));
                }
            }

            var projects = new List<DeliveryVehicleResponse>();
            foreach (var link in links.Where(l => l.ProjectId is not null))
            {
                var project = await bus.SendAsync(new GetProjectSummaryQuery(link.ProjectId!.Value), ct);
                if (project is not null)
                {
                    projects.Add(new DeliveryVehicleResponse(link.Id, project.Id, project.Name, link.IsPrimary, link.ContributionWeight));
                }
            }

            return Results.Ok(new InitiativeDeliveryResponse(initiative.Id, initiative.Name, initiative.NameAr, programs, projects));
        });
    }

    // ───────────────────────────── Cross-module validation ─────────────────────────────

    private static async Task<IResult?> UnknownDeliveryParent(
        InitiativeDeliveryLinkWriteModel model,
        IMessageBus bus,
        CancellationToken ct)
    {
        if (await bus.SendAsync(new GetInitiativeSummaryQuery(model.InitiativeId), ct) is null)
        {
            return UnknownParent(nameof(model.InitiativeId), "initiative");
        }

        if (model.ProgramId is { } programId && await bus.SendAsync(new GetProgramSummaryQuery(programId), ct) is null)
        {
            return UnknownParent(nameof(model.ProgramId), "program");
        }

        if (model.ProjectId is { } projectId && await bus.SendAsync(new GetProjectSummaryQuery(projectId), ct) is null)
        {
            return UnknownParent(nameof(model.ProjectId), "project");
        }

        return null;
    }

    // A missing row and a row belonging to another tenant are indistinguishable here — the
    // tenant filter (or, cross-module, the owning module's own filter) has already removed
    // the latter, which is the point.
    private static IResult NotFound(string what, Guid id) =>
        Results.Problem(title: "Not found", detail: $"No {what} with id {id}.", statusCode: StatusCodes.Status404NotFound);

    private static IResult UnknownParent(string field, string what) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [field] = new[] { $"No such {what} in this tenant." }
        });

    private static IResult DuplicatePrimary() =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(InitiativeDeliveryLinkWriteModel.IsPrimary)] = new[] { "This initiative already has a primary delivery link." }
        });
}
