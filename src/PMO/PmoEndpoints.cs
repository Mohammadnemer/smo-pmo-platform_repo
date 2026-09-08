using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SmoPmo.Pmo.Scheduling;
using SmoPmo.Shared.Integration;
using SmoPmo.Shared.Messaging;
using SmoPmo.Shared.Multitenancy;

namespace SmoPmo.Pmo;

/// <summary>
/// Authorization policy names the PMO endpoints ask for. The *matrix* behind them lives in
/// /Api (B3) — the module names the policy, the composition root decides who satisfies it.
/// Reuses B3's "Project" resource, which already covers the whole PMO surface the same way
/// SMO's "Strategy" resource covers its whole surface.
/// </summary>
public static class PmoPolicies
{
    public const string Read = "PmoReadPolicy";
    public const string Write = "PmoWritePolicy";
}

/// <summary>
/// The PMO REST surface: CRUD for portfolios/programs/projects, tasks and dependencies, RAID
/// and status reports, plus the CPM engine wired server-side (B6's own line) — a project's
/// schedule and critical path as read-only aggregates over the CPM output.
///
/// The module maps its own routes; /Api only calls <see cref="MapPmoEndpoints"/>.
/// </summary>
public static class PmoEndpoints
{
    public static IEndpointRouteBuilder MapPmoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/pmo").RequireAuthorization(PmoPolicies.Read);

        MapPortfolios(group);
        MapPrograms(group);
        MapProjects(group);
        MapTasks(group);
        MapDependencies(group);
        MapRaidItems(group);
        MapStatusReports(group);

        return endpoints;
    }

    // ───────────────────────────── Portfolios ─────────────────────────────

    private static void MapPortfolios(RouteGroupBuilder group)
    {
        group.MapGet("/portfolios", async (PmoDbContext db, CancellationToken ct) =>
        {
            var entities = await db.Portfolios.AsNoTracking().OrderBy(e => e.Name).ToListAsync(ct);
            return Results.Ok(entities.Select(PmoMapping.ToResponse).ToList());
        });

        group.MapGet("/portfolios/{id:guid}", async (Guid id, PmoDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Portfolios.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
            return entity is null ? NotFound("portfolio", id) : Results.Ok(entity.ToResponse());
        });

        group.MapPost("/portfolios", async (PortfolioWriteModel model, PmoDbContext db, CancellationToken ct) =>
        {
            if (PmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            var entity = new Portfolio();
            entity.Apply(model);
            db.Portfolios.Add(entity);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/pmo/portfolios/{entity.Id}", entity.ToResponse());
        }).RequireAuthorization(PmoPolicies.Write);

        group.MapPut("/portfolios/{id:guid}", async (Guid id, PortfolioWriteModel model, PmoDbContext db, CancellationToken ct) =>
        {
            if (PmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            var entity = await db.Portfolios.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("portfolio", id);
            }

            entity.Apply(model);
            await db.SaveChangesAsync(ct);

            return Results.Ok(entity.ToResponse());
        }).RequireAuthorization(PmoPolicies.Write);

        group.MapDelete("/portfolios/{id:guid}", async (Guid id, PmoDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Portfolios.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("portfolio", id);
            }

            db.Portfolios.Remove(entity);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        }).RequireAuthorization(PmoPolicies.Write);
    }

    // ───────────────────────────── Programs ─────────────────────────────

    private static void MapPrograms(RouteGroupBuilder group)
    {
        group.MapGet("/programs", async (PmoDbContext db, CancellationToken ct, Guid? portfolioId = null) =>
        {
            var entities = await db.Programs
                .AsNoTracking()
                .Where(e => portfolioId == null || e.PortfolioId == portfolioId)
                .OrderBy(e => e.Name)
                .ToListAsync(ct);

            return Results.Ok(entities.Select(PmoMapping.ToResponse).ToList());
        });

        group.MapGet("/programs/{id:guid}", async (Guid id, PmoDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Programs.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
            return entity is null ? NotFound("program", id) : Results.Ok(entity.ToResponse());
        });

        group.MapPost("/programs", async (ProgramWriteModel model, PmoDbContext db, CancellationToken ct) =>
        {
            if (PmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            if (!await db.Portfolios.AnyAsync(e => e.Id == model.PortfolioId, ct))
            {
                return UnknownParent(nameof(model.PortfolioId), "portfolio");
            }

            var entity = new Program();
            entity.Apply(model);
            db.Programs.Add(entity);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/pmo/programs/{entity.Id}", entity.ToResponse());
        }).RequireAuthorization(PmoPolicies.Write);

        group.MapPut("/programs/{id:guid}", async (Guid id, ProgramWriteModel model, PmoDbContext db, CancellationToken ct) =>
        {
            if (PmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            var entity = await db.Programs.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("program", id);
            }

            if (!await db.Portfolios.AnyAsync(e => e.Id == model.PortfolioId, ct))
            {
                return UnknownParent(nameof(model.PortfolioId), "portfolio");
            }

            entity.Apply(model);
            await db.SaveChangesAsync(ct);

            return Results.Ok(entity.ToResponse());
        }).RequireAuthorization(PmoPolicies.Write);

        group.MapDelete("/programs/{id:guid}", async (Guid id, PmoDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Programs.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("program", id);
            }

            db.Programs.Remove(entity);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        }).RequireAuthorization(PmoPolicies.Write);
    }

    // ───────────────────────────── Projects ─────────────────────────────

    private static void MapProjects(RouteGroupBuilder group)
    {
        group.MapGet("/projects", async (PmoDbContext db, CancellationToken ct, Guid? programId = null) =>
        {
            var entities = await db.Projects
                .AsNoTracking()
                .Where(e => programId == null || e.ProgramId == programId)
                .OrderBy(e => e.Name)
                .ToListAsync(ct);

            return Results.Ok(entities.Select(PmoMapping.ToResponse).ToList());
        });

        group.MapGet("/projects/{id:guid}", async (Guid id, PmoDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Projects.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
            return entity is null ? NotFound("project", id) : Results.Ok(entity.ToResponse());
        });

        group.MapPost("/projects", async (ProjectWriteModel model, PmoDbContext db, CancellationToken ct) =>
        {
            if (PmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            if (!await db.Programs.AnyAsync(e => e.Id == model.ProgramId, ct))
            {
                return UnknownParent(nameof(model.ProgramId), "program");
            }

            var entity = new Project();
            entity.Apply(model);
            db.Projects.Add(entity);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/pmo/projects/{entity.Id}", entity.ToResponse());
        }).RequireAuthorization(PmoPolicies.Write);

        group.MapPut("/projects/{id:guid}", async (Guid id, ProjectWriteModel model, PmoDbContext db, CancellationToken ct) =>
        {
            if (PmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            var entity = await db.Projects.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("project", id);
            }

            if (!await db.Programs.AnyAsync(e => e.Id == model.ProgramId, ct))
            {
                return UnknownParent(nameof(model.ProgramId), "program");
            }

            var startDateChanged = entity.StartDate != model.StartDate;
            entity.Apply(model);
            await db.SaveChangesAsync(ct);

            // The project's start date is the CPM anchor: moving it shifts every stored date
            // even though no task or dependency changed.
            if (startDateChanged)
            {
                await ScheduleQuery.RecomputeAsync(db, entity.Id, ct);
            }

            return Results.Ok(entity.ToResponse());
        }).RequireAuthorization(PmoPolicies.Write);

        group.MapDelete("/projects/{id:guid}", async (Guid id, PmoDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Projects.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("project", id);
            }

            db.Projects.Remove(entity);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        }).RequireAuthorization(PmoPolicies.Write);

        // ─────────────── The CPM engine wired server-side (B6's own line) ───────────────

        group.MapGet("/projects/{id:guid}/schedule", async (Guid id, PmoDbContext db, CancellationToken ct) =>
        {
            var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
            if (project is null)
            {
                return NotFound("project", id);
            }

            var tasks = await db.Tasks.AsNoTracking().Where(t => t.ProjectId == id).OrderBy(t => t.ScheduleStart).ToListAsync(ct);
            var dependencies = await db.Dependencies.AsNoTracking().Where(d => d.ProjectId == id).ToListAsync(ct);
            var calendar = await ScheduleQuery.LoadCalendarAsync(db, ct);
            var anchor = calendar.FirstWorkingDayOnOrAfter(project.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow));

            var finishDates = tasks.Where(t => t.ScheduleFinish is not null).Select(t => t.ScheduleFinish!.Value).ToList();
            var projectFinish = finishDates.Count == 0 ? (DateOnly?)null : finishDates.Max();

            return Results.Ok(new ScheduleResponse(
                id,
                anchor,
                projectFinish,
                tasks.Select(PmoMapping.ToResponse).ToList(),
                dependencies.Select(PmoMapping.ToResponse).ToList()));
        });

        // "asking for its critical path" (B6's own acceptance line): the ordered set of
        // tasks with zero total float, read straight off the stored schedule.
        group.MapGet("/projects/{id:guid}/critical-path", async (Guid id, PmoDbContext db, CancellationToken ct) =>
        {
            if (!await db.Projects.AnyAsync(e => e.Id == id, ct))
            {
                return NotFound("project", id);
            }

            var criticalTasks = await db.Tasks
                .AsNoTracking()
                .Where(t => t.ProjectId == id && t.IsCritical)
                .OrderBy(t => t.ScheduleStart)
                .ThenBy(t => t.Name)
                .ToListAsync(ct);

            var projectFinish = criticalTasks.Count == 0 ? null : criticalTasks.Max(t => t.ScheduleFinish);

            return Results.Ok(new CriticalPathResponse(
                id,
                projectFinish,
                criticalTasks.Select(PmoMapping.ToResponse).ToList()));
        });
    }

    // ───────────────────────────── Tasks ─────────────────────────────

    private static void MapTasks(RouteGroupBuilder group)
    {
        group.MapGet("/tasks", async (PmoDbContext db, CancellationToken ct, Guid? projectId = null) =>
        {
            var entities = await db.Tasks
                .AsNoTracking()
                .Where(e => projectId == null || e.ProjectId == projectId)
                .OrderBy(e => e.ScheduleStart)
                .ThenBy(e => e.Name)
                .ToListAsync(ct);

            return Results.Ok(entities.Select(PmoMapping.ToResponse).ToList());
        });

        group.MapGet("/tasks/{id:guid}", async (Guid id, PmoDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
            return entity is null ? NotFound("task", id) : Results.Ok(entity.ToResponse());
        });

        group.MapPost("/tasks", async (
            TaskWriteModel model, PmoDbContext db, IMessageBus bus, ITenantContext tenantContext, CancellationToken ct) =>
        {
            if (PmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            if (!await db.Projects.AnyAsync(e => e.Id == model.ProjectId, ct))
            {
                return UnknownParent(nameof(model.ProjectId), "project");
            }

            var entity = new TaskItem();
            entity.Apply(model);
            db.Tasks.Add(entity);
            await db.SaveChangesAsync(ct);

            // "saving a schedule" (B6's own acceptance line): every task/dependency write
            // recomputes the whole project's CPM immediately and stores it.
            await ScheduleQuery.RecomputeAsync(db, entity.ProjectId, ct);
            await PublishHealthInputsChangedAsync(bus, tenantContext, entity.ProjectId, ct);

            return Results.Created($"/api/pmo/tasks/{entity.Id}", entity.ToResponse());
        }).RequireAuthorization(PmoPolicies.Write);

        group.MapPut("/tasks/{id:guid}", async (
            Guid id, TaskWriteModel model, PmoDbContext db, IMessageBus bus, ITenantContext tenantContext, CancellationToken ct) =>
        {
            if (PmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            var entity = await db.Tasks.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("task", id);
            }

            if (!await db.Projects.AnyAsync(e => e.Id == model.ProjectId, ct))
            {
                return UnknownParent(nameof(model.ProjectId), "project");
            }

            var previousProjectId = entity.ProjectId;
            entity.Apply(model);
            await db.SaveChangesAsync(ct);

            await ScheduleQuery.RecomputeAsync(db, entity.ProjectId, ct);
            await PublishHealthInputsChangedAsync(bus, tenantContext, entity.ProjectId, ct);
            if (previousProjectId != entity.ProjectId)
            {
                await ScheduleQuery.RecomputeAsync(db, previousProjectId, ct);
                await PublishHealthInputsChangedAsync(bus, tenantContext, previousProjectId, ct);
            }

            return Results.Ok(entity.ToResponse());
        }).RequireAuthorization(PmoPolicies.Write);

        group.MapDelete("/tasks/{id:guid}", async (
            Guid id, PmoDbContext db, IMessageBus bus, ITenantContext tenantContext, CancellationToken ct) =>
        {
            var entity = await db.Tasks.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("task", id);
            }

            var projectId = entity.ProjectId;

            // A dependency pointing at a deleted task would make the next recompute throw
            // (the engine has no duration to look up for it), so orphaned edges go first.
            var orphanedDependencies = await db.Dependencies
                .Where(d => d.ProjectId == projectId && (d.PredecessorTaskId == id || d.SuccessorTaskId == id))
                .ToListAsync(ct);
            db.Dependencies.RemoveRange(orphanedDependencies);

            db.Tasks.Remove(entity);
            await db.SaveChangesAsync(ct);

            await ScheduleQuery.RecomputeAsync(db, projectId, ct);
            await PublishHealthInputsChangedAsync(bus, tenantContext, projectId, ct);

            return Results.NoContent();
        }).RequireAuthorization(PmoPolicies.Write);
    }

    // ───────────────────────────── Dependencies ─────────────────────────────

    private static void MapDependencies(RouteGroupBuilder group)
    {
        group.MapGet("/dependencies", async (PmoDbContext db, CancellationToken ct, Guid? projectId = null) =>
        {
            var entities = await db.Dependencies
                .AsNoTracking()
                .Where(e => projectId == null || e.ProjectId == projectId)
                .ToListAsync(ct);

            return Results.Ok(entities.Select(PmoMapping.ToResponse).ToList());
        });

        group.MapGet("/dependencies/{id:guid}", async (Guid id, PmoDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Dependencies.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
            return entity is null ? NotFound("dependency", id) : Results.Ok(entity.ToResponse());
        });

        group.MapPost("/dependencies", async (
            DependencyWriteModel model, PmoDbContext db, IMessageBus bus, ITenantContext tenantContext, CancellationToken ct) =>
        {
            if (PmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            if (!await db.Projects.AnyAsync(e => e.Id == model.ProjectId, ct))
            {
                return UnknownParent(nameof(model.ProjectId), "project");
            }

            var tasks = await db.Tasks.Where(t => t.ProjectId == model.ProjectId).ToListAsync(ct);
            var taskIds = tasks.Select(t => t.Id).ToHashSet();
            if (!taskIds.Contains(model.PredecessorTaskId) || !taskIds.Contains(model.SuccessorTaskId))
            {
                return UnknownParent(nameof(model.PredecessorTaskId), "task in this project");
            }

            var candidate = new Dependency();
            candidate.Apply(model);

            var existingDependencies = await db.Dependencies.Where(d => d.ProjectId == model.ProjectId).ToListAsync(ct);
            var (calendar, anchor) = await ScheduleQuery.ResolveAnchorAsync(db, model.ProjectId, ct);

            CriticalPathEngine.ScheduleResult result;
            try
            {
                result = ScheduleQuery.Compute(tasks, existingDependencies.Append(candidate).ToList(), calendar, anchor);
            }
            catch (CyclicScheduleException)
            {
                return CyclicDependency();
            }

            db.Dependencies.Add(candidate);
            await ScheduleQuery.PersistAsync(db, model.ProjectId, tasks, result, calendar, anchor, ct);
            await PublishHealthInputsChangedAsync(bus, tenantContext, model.ProjectId, ct);

            return Results.Created($"/api/pmo/dependencies/{candidate.Id}", candidate.ToResponse());
        }).RequireAuthorization(PmoPolicies.Write);

        group.MapPut("/dependencies/{id:guid}", async (
            Guid id, DependencyWriteModel model, PmoDbContext db, IMessageBus bus, ITenantContext tenantContext, CancellationToken ct) =>
        {
            if (PmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            var entity = await db.Dependencies.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("dependency", id);
            }

            if (!await db.Projects.AnyAsync(e => e.Id == model.ProjectId, ct))
            {
                return UnknownParent(nameof(model.ProjectId), "project");
            }

            var tasks = await db.Tasks.Where(t => t.ProjectId == model.ProjectId).ToListAsync(ct);
            var taskIds = tasks.Select(t => t.Id).ToHashSet();
            if (!taskIds.Contains(model.PredecessorTaskId) || !taskIds.Contains(model.SuccessorTaskId))
            {
                return UnknownParent(nameof(model.PredecessorTaskId), "task in this project");
            }

            var candidate = new Dependency { Id = entity.Id };
            candidate.Apply(model);

            var otherDependencies = await db.Dependencies
                .Where(d => d.ProjectId == model.ProjectId && d.Id != id)
                .ToListAsync(ct);
            var (calendar, anchor) = await ScheduleQuery.ResolveAnchorAsync(db, model.ProjectId, ct);

            CriticalPathEngine.ScheduleResult result;
            try
            {
                result = ScheduleQuery.Compute(tasks, otherDependencies.Append(candidate).ToList(), calendar, anchor);
            }
            catch (CyclicScheduleException)
            {
                return CyclicDependency();
            }

            entity.Apply(model);
            await ScheduleQuery.PersistAsync(db, model.ProjectId, tasks, result, calendar, anchor, ct);
            await PublishHealthInputsChangedAsync(bus, tenantContext, model.ProjectId, ct);

            return Results.Ok(entity.ToResponse());
        }).RequireAuthorization(PmoPolicies.Write);

        group.MapDelete("/dependencies/{id:guid}", async (
            Guid id, PmoDbContext db, IMessageBus bus, ITenantContext tenantContext, CancellationToken ct) =>
        {
            var entity = await db.Dependencies.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("dependency", id);
            }

            var projectId = entity.ProjectId;
            db.Dependencies.Remove(entity);
            await db.SaveChangesAsync(ct);

            // Removing an edge can only ever relax the schedule, never introduce a cycle.
            await ScheduleQuery.RecomputeAsync(db, projectId, ct);
            await PublishHealthInputsChangedAsync(bus, tenantContext, projectId, ct);

            return Results.NoContent();
        }).RequireAuthorization(PmoPolicies.Write);
    }

    // ───────────────────────────── RAID ─────────────────────────────

    private static void MapRaidItems(RouteGroupBuilder group)
    {
        group.MapGet("/raid", async (
            PmoDbContext db,
            CancellationToken ct,
            Guid? portfolioId = null,
            Guid? programId = null,
            Guid? projectId = null) =>
        {
            var entities = await db.RaidItems
                .AsNoTracking()
                .Where(e => portfolioId == null || e.PortfolioId == portfolioId)
                .Where(e => programId == null || e.ProgramId == programId)
                .Where(e => projectId == null || e.ProjectId == projectId)
                .OrderBy(e => e.Title)
                .ToListAsync(ct);

            return Results.Ok(entities.Select(PmoMapping.ToResponse).ToList());
        });

        group.MapGet("/raid/{id:guid}", async (Guid id, PmoDbContext db, CancellationToken ct) =>
        {
            var entity = await db.RaidItems.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
            return entity is null ? NotFound("RAID item", id) : Results.Ok(entity.ToResponse());
        });

        group.MapPost("/raid", async (
            RaidItemWriteModel model, PmoDbContext db, IMessageBus bus, ITenantContext tenantContext, CancellationToken ct) =>
        {
            if (PmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            if (await UnknownRaidParent(model, db, ct) is { } problem)
            {
                return problem;
            }

            var entity = new RaidItem();
            entity.Apply(model);
            db.RaidItems.Add(entity);
            await db.SaveChangesAsync(ct);

            if (entity.ProjectId is { } projectId)
            {
                await PublishHealthInputsChangedAsync(bus, tenantContext, projectId, ct);
            }

            return Results.Created($"/api/pmo/raid/{entity.Id}", entity.ToResponse());
        }).RequireAuthorization(PmoPolicies.Write);

        group.MapPut("/raid/{id:guid}", async (
            Guid id, RaidItemWriteModel model, PmoDbContext db, IMessageBus bus, ITenantContext tenantContext, CancellationToken ct) =>
        {
            if (PmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            var entity = await db.RaidItems.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("RAID item", id);
            }

            if (await UnknownRaidParent(model, db, ct) is { } problem)
            {
                return problem;
            }

            var previousProjectId = entity.ProjectId;
            entity.Apply(model);
            await db.SaveChangesAsync(ct);

            // A RAID item's parent can move between portfolio/program/project (exactly one
            // set), so both the old and new project (when either is set, and they differ)
            // may have had their inputs change.
            if (entity.ProjectId is { } projectId)
            {
                await PublishHealthInputsChangedAsync(bus, tenantContext, projectId, ct);
            }

            if (previousProjectId is { } previous && previous != entity.ProjectId)
            {
                await PublishHealthInputsChangedAsync(bus, tenantContext, previous, ct);
            }

            return Results.Ok(entity.ToResponse());
        }).RequireAuthorization(PmoPolicies.Write);

        group.MapDelete("/raid/{id:guid}", async (
            Guid id, PmoDbContext db, IMessageBus bus, ITenantContext tenantContext, CancellationToken ct) =>
        {
            var entity = await db.RaidItems.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity is null)
            {
                return NotFound("RAID item", id);
            }

            var projectId = entity.ProjectId;
            db.RaidItems.Remove(entity);
            await db.SaveChangesAsync(ct);

            if (projectId is { } removedFromProjectId)
            {
                await PublishHealthInputsChangedAsync(bus, tenantContext, removedFromProjectId, ct);
            }

            return Results.NoContent();
        }).RequireAuthorization(PmoPolicies.Write);
    }

    private static async Task<IResult?> UnknownRaidParent(RaidItemWriteModel model, PmoDbContext db, CancellationToken ct)
    {
        if (model.PortfolioId is { } portfolioId && !await db.Portfolios.AnyAsync(e => e.Id == portfolioId, ct))
        {
            return UnknownParent(nameof(model.PortfolioId), "portfolio");
        }

        if (model.ProgramId is { } programId && !await db.Programs.AnyAsync(e => e.Id == programId, ct))
        {
            return UnknownParent(nameof(model.ProgramId), "program");
        }

        if (model.ProjectId is { } projectId && !await db.Projects.AnyAsync(e => e.Id == projectId, ct))
        {
            return UnknownParent(nameof(model.ProjectId), "project");
        }

        return null;
    }

    // ───────────────────────────── Status reports ─────────────────────────────
    // Snapshot + history (PRD §6.2.7): creating one is always an append. There is
    // deliberately no update or delete — a status report is a point-in-time record.

    private static void MapStatusReports(RouteGroupBuilder group)
    {
        group.MapGet("/status-reports", async (
            PmoDbContext db,
            CancellationToken ct,
            Guid? projectId = null,
            Guid? programId = null) =>
        {
            var entities = await db.StatusReports
                .AsNoTracking()
                .Where(e => projectId == null || e.ProjectId == projectId)
                .Where(e => programId == null || e.ProgramId == programId)
                .OrderByDescending(e => e.ReportDate)
                .ToListAsync(ct);

            return Results.Ok(entities.Select(PmoMapping.ToResponse).ToList());
        });

        group.MapGet("/status-reports/{id:guid}", async (Guid id, PmoDbContext db, CancellationToken ct) =>
        {
            var entity = await db.StatusReports.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
            return entity is null ? NotFound("status report", id) : Results.Ok(entity.ToResponse());
        });

        group.MapPost("/status-reports", async (StatusReportWriteModel model, PmoDbContext db, CancellationToken ct) =>
        {
            if (PmoValidation.Validate(model) is { } errors)
            {
                return Results.ValidationProblem(errors);
            }

            if (model.ProjectId is { } projectId && !await db.Projects.AnyAsync(e => e.Id == projectId, ct))
            {
                return UnknownParent(nameof(model.ProjectId), "project");
            }

            if (model.ProgramId is { } programId && !await db.Programs.AnyAsync(e => e.Id == programId, ct))
            {
                return UnknownParent(nameof(model.ProgramId), "program");
            }

            var entity = new StatusReport();
            entity.Apply(model);
            db.StatusReports.Add(entity);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/pmo/status-reports/{entity.Id}", entity.ToResponse());
        }).RequireAuthorization(PmoPolicies.Write);
    }

    // A missing row and a row belonging to another tenant are indistinguishable here — the
    // tenant filter has already removed the latter, which is the point.
    private static IResult NotFound(string what, Guid id) =>
        Results.Problem(title: "Not found", detail: $"No {what} with id {id}.", statusCode: StatusCodes.Status404NotFound);

    private static IResult UnknownParent(string field, string what) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [field] = new[] { $"No such {what} in this tenant." }
        });

    private static IResult CyclicDependency() =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["type"] = new[] { "This dependency would create a cycle in the project's schedule." }
        });

    /// <summary>
    /// Signals the roll-up worker (X2) that this project's health inputs may have changed —
    /// task % complete, schedule variance (via the CPM recompute every task/dependency write
    /// already triggers), or RAID severity. The worker debounces these before recomputing
    /// (architecture-mvp.md §5), so publishing once per write here is cheap and correct even
    /// under a burst of edits.
    /// </summary>
    private static Task PublishHealthInputsChangedAsync(
        IMessageBus bus, ITenantContext tenantContext, Guid projectId, CancellationToken ct) =>
        bus.PublishAsync(new PmoDeliveryHealthInputsChanged(tenantContext.TenantId, projectId), ct);
}
