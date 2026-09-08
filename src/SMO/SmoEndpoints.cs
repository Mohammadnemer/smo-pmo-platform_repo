using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace SmoPmo.Smo;

/// <summary>
/// Authorization policy names the SMO endpoints ask for. The *matrix* behind them lives
/// in /Api (B3) — the module names the policy, the composition root decides who satisfies
/// it, and neither has to reference the other's types.
/// </summary>
public static class SmoPolicies
{
    public const string Read = "SmoReadPolicy";
    public const string Write = "SmoWritePolicy";
}

/// <summary>
/// The SMO REST surface (ADR 0002: REST resources + one scorecard aggregate).
///
/// The module maps its own routes; /Api only calls <see cref="MapSmoEndpoints"/>. Every
/// query goes through <see cref="SmoDbContext"/>, so the tenant filter and RLS apply
/// without any endpoint having to remember a WHERE clause.
/// </summary>
public static class SmoEndpoints
{
    public static IEndpointRouteBuilder MapSmoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/smo").RequireAuthorization(SmoPolicies.Read);

        MapStrategies(group);
        MapPerspectives(group);
        MapStrategicThemes(group);
        MapObjectives(group);
        MapKpis(group);
        MapInitiatives(group);
        MapObjectiveLinks(group);

        return endpoints;
    }

    // ───────────────────────────── Strategies ─────────────────────────────

    private static void MapStrategies(RouteGroupBuilder group)
    {
        // Rows are materialised before mapping: ToResponse() is C#, not SQL, and the KPI
        // mapping in particular evaluates RAG in the domain rather than in the database.
        group.MapGet("/strategies", async (SmoDbContext db, CancellationToken ct) =>
        {
            var entities = await db.Strategies
                .AsNoTracking()
                .OrderBy(e => e.Name)
                .ToListAsync(ct);

            return Results.Ok(entities.Select(SmoMapping.ToResponse).ToList());
        });

        group.MapGet("/strategies/{id:guid}", async (Guid id, SmoDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Strategies.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
            return entity is null ? NotFound("strategy", id) : Results.Ok(entity.ToResponse());
        });

        group.MapPost("/strategies", async (StrategyWriteModel model, SmoDbContext db, CancellationToken ct) =>
        {
            if (SmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            var entity = new Strategy();
            entity.Apply(model);
            db.Strategies.Add(entity);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/smo/strategies/{entity.Id}", entity.ToResponse());
        }).RequireAuthorization(SmoPolicies.Write);

        group.MapPut("/strategies/{id:guid}", async (Guid id, StrategyWriteModel model, SmoDbContext db, CancellationToken ct) =>
        {
            if (SmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            var entity = await db.Strategies.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("strategy", id);
            }

            entity.Apply(model);
            await db.SaveChangesAsync(ct);

            return Results.Ok(entity.ToResponse());
        }).RequireAuthorization(SmoPolicies.Write);

        group.MapDelete("/strategies/{id:guid}", async (Guid id, SmoDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Strategies.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("strategy", id);
            }

            db.Strategies.Remove(entity);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        }).RequireAuthorization(SmoPolicies.Write);

        // The aggregate read: the whole scorecard in one round trip (ADR 0002).
        group.MapGet("/strategies/{id:guid}/scorecard", async (
            Guid id,
            SmoDbContext db,
            CancellationToken ct,
            bool includeHidden = false,
            int trendPoints = ScorecardQuery.DefaultTrendPoints) =>
        {
            var scorecard = await ScorecardQuery.BuildAsync(db, id, includeHidden, trendPoints, ct);
            return scorecard is null ? NotFound("strategy", id) : Results.Ok(scorecard);
        });
    }

    // ──────────────────────────── Perspectives ────────────────────────────

    private static void MapPerspectives(RouteGroupBuilder group)
    {
        group.MapGet("/perspectives", async (SmoDbContext db, CancellationToken ct, Guid? strategyId = null) =>
        {
            var entities = await db.Perspectives
                .AsNoTracking()
                .Where(e => strategyId == null || e.StrategyId == strategyId)
                .OrderBy(e => e.DisplayOrder)
                .ThenBy(e => e.Name)
                .ToListAsync(ct);

            return Results.Ok(entities.Select(SmoMapping.ToResponse).ToList());
        });

        group.MapGet("/perspectives/{id:guid}", async (Guid id, SmoDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Perspectives.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
            return entity is null ? NotFound("perspective", id) : Results.Ok(entity.ToResponse());
        });

        group.MapPost("/perspectives", async (PerspectiveWriteModel model, SmoDbContext db, CancellationToken ct) =>
        {
            if (SmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            if (!await db.Strategies.AnyAsync(e => e.Id == model.StrategyId, ct))
            {
                return UnknownParent(nameof(model.StrategyId), "strategy");
            }

            var entity = new Perspective();
            entity.Apply(model);
            db.Perspectives.Add(entity);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/smo/perspectives/{entity.Id}", entity.ToResponse());
        }).RequireAuthorization(SmoPolicies.Write);

        group.MapPut("/perspectives/{id:guid}", async (Guid id, PerspectiveWriteModel model, SmoDbContext db, CancellationToken ct) =>
        {
            if (SmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            var entity = await db.Perspectives.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("perspective", id);
            }

            if (!await db.Strategies.AnyAsync(e => e.Id == model.StrategyId, ct))
            {
                return UnknownParent(nameof(model.StrategyId), "strategy");
            }

            entity.Apply(model);
            await db.SaveChangesAsync(ct);

            return Results.Ok(entity.ToResponse());
        }).RequireAuthorization(SmoPolicies.Write);

        group.MapDelete("/perspectives/{id:guid}", async (Guid id, SmoDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Perspectives.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("perspective", id);
            }

            db.Perspectives.Remove(entity);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        }).RequireAuthorization(SmoPolicies.Write);
    }

    // ───────────────────── Strategic themes (alignment grid) ─────────────────────
    // The cross-perspective storylines an objective may belong to (PRD §6.1.2) — the
    // alignment grid's columns. Full CRUD like perspectives: without POST there would be
    // no way to author a theme at all, and assignment then happens through the existing
    // objective PUT (ObjectiveWriteModel.StrategicThemeId).

    private static void MapStrategicThemes(RouteGroupBuilder group)
    {
        group.MapGet("/strategic-themes", async (SmoDbContext db, CancellationToken ct, Guid? strategyId = null) =>
        {
            var entities = await db.StrategicThemes
                .AsNoTracking()
                .Where(e => strategyId == null || e.StrategyId == strategyId)
                .OrderBy(e => e.DisplayOrder)
                .ThenBy(e => e.Name)
                .ToListAsync(ct);

            return Results.Ok(entities.Select(SmoMapping.ToResponse).ToList());
        });

        group.MapGet("/strategic-themes/{id:guid}", async (Guid id, SmoDbContext db, CancellationToken ct) =>
        {
            var entity = await db.StrategicThemes.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
            return entity is null ? NotFound("strategic theme", id) : Results.Ok(entity.ToResponse());
        });

        group.MapPost("/strategic-themes", async (StrategicThemeWriteModel model, SmoDbContext db, CancellationToken ct) =>
        {
            if (SmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            if (!await db.Strategies.AnyAsync(e => e.Id == model.StrategyId, ct))
            {
                return UnknownParent(nameof(model.StrategyId), "strategy");
            }

            var entity = new StrategicTheme();
            entity.Apply(model);
            db.StrategicThemes.Add(entity);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/smo/strategic-themes/{entity.Id}", entity.ToResponse());
        }).RequireAuthorization(SmoPolicies.Write);

        group.MapPut("/strategic-themes/{id:guid}", async (Guid id, StrategicThemeWriteModel model, SmoDbContext db, CancellationToken ct) =>
        {
            if (SmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            var entity = await db.StrategicThemes.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("strategic theme", id);
            }

            if (!await db.Strategies.AnyAsync(e => e.Id == model.StrategyId, ct))
            {
                return UnknownParent(nameof(model.StrategyId), "strategy");
            }

            entity.Apply(model);
            await db.SaveChangesAsync(ct);

            return Results.Ok(entity.ToResponse());
        }).RequireAuthorization(SmoPolicies.Write);

        // Deleting a theme un-themes its objectives (FK ON DELETE SET NULL) rather than
        // deleting them — see SmoDbContext. The grid then shows them in its unthemed column.
        group.MapDelete("/strategic-themes/{id:guid}", async (Guid id, SmoDbContext db, CancellationToken ct) =>
        {
            var entity = await db.StrategicThemes.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("strategic theme", id);
            }

            db.StrategicThemes.Remove(entity);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        }).RequireAuthorization(SmoPolicies.Write);
    }

    // ───────────────────────────── Objectives ─────────────────────────────

    private static void MapObjectives(RouteGroupBuilder group)
    {
        group.MapGet("/objectives", async (
            SmoDbContext db,
            CancellationToken ct,
            Guid? perspectiveId = null,
            Guid? strategyId = null) =>
        {
            var query = db.Objectives.AsNoTracking().AsQueryable();

            if (perspectiveId is { } perspective)
            {
                query = query.Where(e => e.PerspectiveId == perspective);
            }

            if (strategyId is { } strategy)
            {
                var perspectiveIds = db.Perspectives.Where(e => e.StrategyId == strategy).Select(e => e.Id);
                query = query.Where(e => perspectiveIds.Contains(e.PerspectiveId));
            }

            var entities = await query
                .OrderBy(e => e.DisplayOrder)
                .ThenBy(e => e.Name)
                .ToListAsync(ct);

            return Results.Ok(entities.Select(SmoMapping.ToResponse).ToList());
        });

        group.MapGet("/objectives/{id:guid}", async (Guid id, SmoDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Objectives.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
            return entity is null ? NotFound("objective", id) : Results.Ok(entity.ToResponse());
        });

        group.MapPost("/objectives", async (ObjectiveWriteModel model, SmoDbContext db, CancellationToken ct) =>
        {
            if (SmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            if (!await db.Perspectives.AnyAsync(e => e.Id == model.PerspectiveId, ct))
            {
                return UnknownParent(nameof(model.PerspectiveId), "perspective");
            }

            if (model.StrategicThemeId is { } themeId
                && !await db.StrategicThemes.AnyAsync(e => e.Id == themeId, ct))
            {
                return UnknownParent(nameof(model.StrategicThemeId), "strategic theme");
            }

            var entity = new Objective();
            entity.Apply(model);
            db.Objectives.Add(entity);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/smo/objectives/{entity.Id}", entity.ToResponse());
        }).RequireAuthorization(SmoPolicies.Write);

        group.MapPut("/objectives/{id:guid}", async (Guid id, ObjectiveWriteModel model, SmoDbContext db, CancellationToken ct) =>
        {
            if (SmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            var entity = await db.Objectives.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("objective", id);
            }

            if (!await db.Perspectives.AnyAsync(e => e.Id == model.PerspectiveId, ct))
            {
                return UnknownParent(nameof(model.PerspectiveId), "perspective");
            }

            if (model.StrategicThemeId is { } themeId
                && !await db.StrategicThemes.AnyAsync(e => e.Id == themeId, ct))
            {
                return UnknownParent(nameof(model.StrategicThemeId), "strategic theme");
            }

            entity.Apply(model);
            await db.SaveChangesAsync(ct);

            return Results.Ok(entity.ToResponse());
        }).RequireAuthorization(SmoPolicies.Write);

        group.MapDelete("/objectives/{id:guid}", async (Guid id, SmoDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Objectives.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("objective", id);
            }

            db.Objectives.Remove(entity);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        }).RequireAuthorization(SmoPolicies.Write);
    }

    // ──────────────────────── KPIs & measurements ────────────────────────

    private static void MapKpis(RouteGroupBuilder group)
    {
        group.MapGet("/kpis", async (SmoDbContext db, CancellationToken ct, Guid? objectiveId = null) =>
        {
            var entities = await db.Kpis
                .AsNoTracking()
                .Where(e => objectiveId == null || e.ObjectiveId == objectiveId)
                .OrderBy(e => e.Name)
                .ToListAsync(ct);

            return Results.Ok(entities.Select(SmoMapping.ToResponse).ToList());
        });

        group.MapGet("/kpis/{id:guid}", async (Guid id, SmoDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Kpis.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
            return entity is null ? NotFound("KPI", id) : Results.Ok(entity.ToResponse());
        });

        group.MapPost("/kpis", async (KpiWriteModel model, SmoDbContext db, CancellationToken ct) =>
        {
            if (SmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            if (!await db.Objectives.AnyAsync(e => e.Id == model.ObjectiveId, ct))
            {
                return UnknownParent(nameof(model.ObjectiveId), "objective");
            }

            var entity = new Kpi();
            entity.Apply(model);
            db.Kpis.Add(entity);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/smo/kpis/{entity.Id}", entity.ToResponse());
        }).RequireAuthorization(SmoPolicies.Write);

        group.MapPut("/kpis/{id:guid}", async (Guid id, KpiWriteModel model, SmoDbContext db, CancellationToken ct) =>
        {
            if (SmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            var entity = await db.Kpis.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("KPI", id);
            }

            if (!await db.Objectives.AnyAsync(e => e.Id == model.ObjectiveId, ct))
            {
                return UnknownParent(nameof(model.ObjectiveId), "objective");
            }

            entity.Apply(model);
            await db.SaveChangesAsync(ct);

            return Results.Ok(entity.ToResponse());
        }).RequireAuthorization(SmoPolicies.Write);

        group.MapDelete("/kpis/{id:guid}", async (Guid id, SmoDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Kpis.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("KPI", id);
            }

            db.Kpis.Remove(entity);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        }).RequireAuthorization(SmoPolicies.Write);

        group.MapGet("/kpis/{id:guid}/measurements", async (Guid id, SmoDbContext db, CancellationToken ct) =>
        {
            if (!await db.Kpis.AnyAsync(e => e.Id == id, ct))
            {
                return NotFound("KPI", id);
            }

            var entities = await db.KpiMeasurements
                .AsNoTracking()
                .Where(e => e.KpiId == id)
                .OrderBy(e => e.PeriodStart)
                .ThenBy(e => e.PeriodEnd)
                .ToListAsync(ct);

            return Results.Ok(entities.Select(SmoMapping.ToResponse).ToList());
        });

        // Appending an actual is the KPI owner's day-to-day action (PRD §6.1.4). It writes
        // the historical series *and* refreshes the denormalised latest actual in one save,
        // so the scorecard never has to aggregate the series on read.
        group.MapPost("/kpis/{id:guid}/measurements", async (
            Guid id,
            KpiMeasurementWriteModel model,
            SmoDbContext db,
            CancellationToken ct) =>
        {
            if (SmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            var kpi = await db.Kpis.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (kpi is null)
            {
                return NotFound("KPI", id);
            }

            var latestPeriodStart = await db.KpiMeasurements
                .Where(e => e.KpiId == id)
                .OrderByDescending(e => e.PeriodStart)
                .Select(e => (DateOnly?)e.PeriodStart)
                .FirstOrDefaultAsync(ct);

            var measurement = new KpiMeasurement
            {
                KpiId = id,
                PeriodStart = model.PeriodStart,
                PeriodEnd = model.PeriodEnd,
                Value = model.Value,
                Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim()
            };

            db.KpiMeasurements.Add(measurement);

            if (latestPeriodStart is null || model.PeriodStart >= latestPeriodStart)
            {
                kpi.Actual = model.Value;
                kpi.ActualAsOf = DateTimeOffset.UtcNow;
            }

            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/smo/kpis/{id}/measurements/{measurement.Id}", measurement.ToResponse());
        }).RequireAuthorization(SmoPolicies.Write);
    }

    // ──────────────────────────── Initiatives ────────────────────────────

    private static void MapInitiatives(RouteGroupBuilder group)
    {
        group.MapGet("/initiatives", async (SmoDbContext db, CancellationToken ct, Guid? objectiveId = null) =>
        {
            var entities = await db.Initiatives
                .AsNoTracking()
                .Where(e => objectiveId == null || e.ObjectiveId == objectiveId)
                .OrderBy(e => e.Name)
                .ToListAsync(ct);

            return Results.Ok(entities.Select(SmoMapping.ToResponse).ToList());
        });

        group.MapGet("/initiatives/{id:guid}", async (Guid id, SmoDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Initiatives.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
            return entity is null ? NotFound("initiative", id) : Results.Ok(entity.ToResponse());
        });

        group.MapPost("/initiatives", async (InitiativeWriteModel model, SmoDbContext db, CancellationToken ct) =>
        {
            if (SmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            if (!await db.Objectives.AnyAsync(e => e.Id == model.ObjectiveId, ct))
            {
                return UnknownParent(nameof(model.ObjectiveId), "objective");
            }

            var entity = new Initiative();
            entity.Apply(model);
            db.Initiatives.Add(entity);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/smo/initiatives/{entity.Id}", entity.ToResponse());
        }).RequireAuthorization(SmoPolicies.Write);

        group.MapPut("/initiatives/{id:guid}", async (Guid id, InitiativeWriteModel model, SmoDbContext db, CancellationToken ct) =>
        {
            if (SmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            var entity = await db.Initiatives.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("initiative", id);
            }

            if (!await db.Objectives.AnyAsync(e => e.Id == model.ObjectiveId, ct))
            {
                return UnknownParent(nameof(model.ObjectiveId), "objective");
            }

            entity.Apply(model);
            await db.SaveChangesAsync(ct);

            return Results.Ok(entity.ToResponse());
        }).RequireAuthorization(SmoPolicies.Write);

        group.MapDelete("/initiatives/{id:guid}", async (Guid id, SmoDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Initiatives.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("initiative", id);
            }

            db.Initiatives.Remove(entity);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        }).RequireAuthorization(SmoPolicies.Write);
    }

    // ────────────────────── Objective links (strategy map) ──────────────────────
    // Cause-effect edges between objectives (PRD §6.1.6). CRUD is intentionally thin — an
    // edge has no lifecycle beyond existing or not, so there is no PUT.

    private static void MapObjectiveLinks(RouteGroupBuilder group)
    {
        group.MapGet("/objective-links", async (
            SmoDbContext db,
            CancellationToken ct,
            Guid? objectiveId = null,
            Guid? strategyId = null) =>
        {
            var query = db.ObjectiveLinks.AsNoTracking().AsQueryable();

            if (objectiveId is { } objective)
            {
                query = query.Where(e => e.SourceObjectiveId == objective || e.TargetObjectiveId == objective);
            }

            if (strategyId is { } strategy)
            {
                var perspectiveIds = db.Perspectives.Where(e => e.StrategyId == strategy).Select(e => e.Id);
                var objectiveIds = db.Objectives.Where(e => perspectiveIds.Contains(e.PerspectiveId)).Select(e => e.Id);
                query = query.Where(e => objectiveIds.Contains(e.SourceObjectiveId) && objectiveIds.Contains(e.TargetObjectiveId));
            }

            var entities = await query.ToListAsync(ct);
            return Results.Ok(entities.Select(SmoMapping.ToResponse).ToList());
        });

        group.MapPost("/objective-links", async (ObjectiveLinkWriteModel model, SmoDbContext db, CancellationToken ct) =>
        {
            if (SmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            if (!await db.Objectives.AnyAsync(e => e.Id == model.SourceObjectiveId, ct))
            {
                return UnknownParent(nameof(model.SourceObjectiveId), "objective");
            }

            if (!await db.Objectives.AnyAsync(e => e.Id == model.TargetObjectiveId, ct))
            {
                return UnknownParent(nameof(model.TargetObjectiveId), "objective");
            }

            var entity = new ObjectiveLink();
            entity.Apply(model);
            db.ObjectiveLinks.Add(entity);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/smo/objective-links/{entity.Id}", entity.ToResponse());
        }).RequireAuthorization(SmoPolicies.Write);

        group.MapDelete("/objective-links/{id:guid}", async (Guid id, SmoDbContext db, CancellationToken ct) =>
        {
            var entity = await db.ObjectiveLinks.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("objective link", id);
            }

            db.ObjectiveLinks.Remove(entity);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        }).RequireAuthorization(SmoPolicies.Write);
    }

    // A missing row and a row belonging to another tenant are indistinguishable here —
    // the tenant filter has already removed the latter, which is the point.
    private static IResult NotFound(string what, Guid id) =>
        Results.Problem(title: "Not found", detail: $"No {what} with id {id}.", statusCode: StatusCodes.Status404NotFound);

    private static IResult UnknownParent(string field, string what) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [field] = new[] { $"No such {what} in this tenant." }
        });
}
