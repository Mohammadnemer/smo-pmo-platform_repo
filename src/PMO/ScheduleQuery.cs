using Microsoft.EntityFrameworkCore;
using SmoPmo.Pmo.Scheduling;

namespace SmoPmo.Pmo;

/// <summary>
/// The CPM engine wired server-side (B6's own line). Converts entities to/from
/// <see cref="CriticalPathEngine"/> inputs, resolves the tenant's <see cref="PmoCalendar"/>,
/// and stores the result on <see cref="TaskItem"/> — the same "stored, not recomputed on
/// read" rule the roll-up engine follows elsewhere (architecture-mvp.md §5).
///
/// Recomputing a schedule is synchronous and eager, unlike the roll-up worker's debounced
/// background recompute: a project's task graph is small and the Gantt (F7/F8) needs to see
/// the effect of a save immediately, so there is nothing to gain by deferring it.
/// </summary>
internal static class ScheduleQuery
{
    public static CriticalPathEngine.ScheduleResult Compute(
        IReadOnlyList<TaskItem> tasks,
        IReadOnlyList<Dependency> dependencies,
        WorkingCalendar calendar,
        DateOnly anchor)
    {
        var taskInputs = tasks
            .Select(t => new CriticalPathEngine.TaskInput(
                t.Id,
                t.DurationDays,
                t.ConstraintStart is { } constraint ? calendar.WorkingDayIndex(anchor, constraint) : null))
            .ToList();

        var dependencyInputs = dependencies
            .Select(d => new CriticalPathEngine.DependencyInput(
                d.PredecessorTaskId, d.SuccessorTaskId, ParseType(d.Type), d.LagDays))
            .ToList();

        return CriticalPathEngine.Compute(taskInputs, dependencyInputs);
    }

    /// <summary>Resolves the tenant's working calendar and the project's CPM anchor (working-day
    /// index 0) — the one conversion every call site that touches <see cref="Compute"/> or
    /// <see cref="PersistAsync"/> needs, factored out so it's resolved once per recompute rather
    /// than once per call.</summary>
    public static async Task<(WorkingCalendar Calendar, DateOnly Anchor)> ResolveAnchorAsync(
        PmoDbContext db, Guid projectId, CancellationToken ct)
    {
        var project = await db.Projects.FirstAsync(p => p.Id == projectId, ct);
        var calendar = await GetOrCreateCalendarAsync(db, ct);
        var anchor = calendar.FirstWorkingDayOnOrAfter(project.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow));
        return (calendar, anchor);
    }

    /// <summary>
    /// Reloads the project's current tasks/dependencies, computes CPM and persists it. Safe
    /// to call after any write that cannot itself introduce a cycle — task create/update/
    /// delete, or dependency delete. Dependency create/update instead compute against an
    /// in-memory candidate graph first (see <c>PmoEndpoints</c>) so a cyclic dependency is
    /// rejected before anything is written, then call <see cref="PersistAsync"/> directly
    /// with that already-computed result.
    /// </summary>
    public static async Task RecomputeAsync(PmoDbContext db, Guid projectId, CancellationToken ct)
    {
        var tasks = await db.Tasks.Where(t => t.ProjectId == projectId).ToListAsync(ct);
        var dependencies = await db.Dependencies.Where(d => d.ProjectId == projectId).ToListAsync(ct);
        var (calendar, anchor) = await ResolveAnchorAsync(db, projectId, ct);
        var result = Compute(tasks, dependencies, calendar, anchor);
        await PersistAsync(db, projectId, tasks, result, calendar, anchor, ct);
    }

    /// <summary>
    /// Applies an already-computed result onto already-loaded (tracked) task entities and
    /// saves. A single <c>SaveChangesAsync</c> covers both these updates and any pending
    /// entity change the caller staged on the same <paramref name="db"/> beforehand (e.g. a
    /// new dependency), so the write and its schedule effect land atomically.
    /// </summary>
    public static async Task PersistAsync(
        PmoDbContext db,
        Guid projectId,
        IReadOnlyList<TaskItem> tasks,
        CriticalPathEngine.ScheduleResult result,
        WorkingCalendar calendar,
        DateOnly anchor,
        CancellationToken ct)
    {
        foreach (var task in tasks)
        {
            var schedule = result.Tasks[task.Id];
            task.ScheduleStart = calendar.AddWorkingDays(anchor, schedule.EarlyStart);

            // Exclusive-end convention: EarlyFinish is the index *after* the last worked day.
            // A milestone (EarlyFinish == EarlyStart) finishes the same day it starts.
            task.ScheduleFinish = schedule.EarlyFinish == schedule.EarlyStart
                ? task.ScheduleStart
                : calendar.AddWorkingDays(anchor, schedule.EarlyFinish - 1);

            task.TotalFloatDays = schedule.TotalFloat;
            task.IsCritical = schedule.IsCritical;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Read-only: never provisions a row, so a GET never has a write side effect.
    /// Falls back to the Fri–Sat default when the tenant has not saved a schedule yet.</summary>
    public static async Task<WorkingCalendar> LoadCalendarAsync(PmoDbContext db, CancellationToken ct)
    {
        var calendar = await db.Calendars.AsNoTracking().FirstOrDefaultAsync(ct);
        return calendar is null ? WorkingCalendar.Default : ToWorkingCalendar(calendar);
    }

    /// <summary>Write path: auto-creates the tenant's calendar row with the Fri–Sat default
    /// the first time a schedule is computed (mirrors D4's tenant-seed-on-first-use pattern).
    /// P3 owns letting an admin edit it afterwards.</summary>
    private static async Task<WorkingCalendar> GetOrCreateCalendarAsync(PmoDbContext db, CancellationToken ct)
    {
        var calendar = await db.Calendars.FirstOrDefaultAsync(ct);
        if (calendar is null)
        {
            calendar = new PmoCalendar();
            db.Calendars.Add(calendar);
        }

        return ToWorkingCalendar(calendar);
    }

    private static WorkingCalendar ToWorkingCalendar(PmoCalendar calendar)
    {
        var days = new List<DayOfWeek> { calendar.WeekendDay1 };
        if (calendar.WeekendDay2 is { } day2)
        {
            days.Add(day2);
        }

        return new WorkingCalendar(days);
    }

    private static DependencyType ParseType(string type) => Enum.Parse<DependencyType>(type, ignoreCase: true);
}
