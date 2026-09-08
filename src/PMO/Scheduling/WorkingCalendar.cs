namespace SmoPmo.Pmo.Scheduling;

/// <summary>
/// Index↔date conversion that skips weekends (architecture-mvp.md §7, layer 2). The CPM
/// engine itself works entirely in integer working-day index space — this is the only place
/// that ever touches a calendar date, so the Jordan/GCC Fri–Sat default (or whatever a
/// tenant's <see cref="PmoCalendar"/> row says) applies uniformly to every project.
///
/// Holidays are not modelled yet — P3 owns the tenant calendar admin UI and can extend this
/// with a holiday set without changing the index math below.
/// </summary>
public sealed class WorkingCalendar
{
    private readonly HashSet<DayOfWeek> _weekend;

    public WorkingCalendar(IEnumerable<DayOfWeek> weekendDays)
    {
        _weekend = new HashSet<DayOfWeek>(weekendDays);
    }

    public static WorkingCalendar Default { get; } = new(new[] { DayOfWeek.Friday, DayOfWeek.Saturday });

    public bool IsWorkingDay(DateOnly date) => !_weekend.Contains(date.DayOfWeek);

    /// <summary>Rolls a date forward (never back) to the next working day, or itself if it
    /// already is one. Used to normalise a project's requested start date into the anchor
    /// that working-day index 0 refers to.</summary>
    public DateOnly FirstWorkingDayOnOrAfter(DateOnly date)
    {
        while (!IsWorkingDay(date))
        {
            date = date.AddDays(1);
        }

        return date;
    }

    /// <summary>
    /// The calendar date <paramref name="workingDays"/> working days after
    /// <paramref name="anchor"/>, where <paramref name="anchor"/> itself is index 0.
    /// <paramref name="anchor"/> must already be a working day (see
    /// <see cref="FirstWorkingDayOnOrAfter"/>).
    /// </summary>
    public DateOnly AddWorkingDays(DateOnly anchor, int workingDays)
    {
        var date = anchor;
        var remaining = workingDays;

        while (remaining > 0)
        {
            date = date.AddDays(1);
            if (IsWorkingDay(date))
            {
                remaining--;
            }
        }

        return date;
    }

    /// <summary>
    /// The inverse of <see cref="AddWorkingDays"/>: how many working days <paramref name="date"/>
    /// is after <paramref name="anchor"/>. A <paramref name="date"/> on or before the anchor
    /// clamps to 0 (a task cannot be constrained to start before the project does). A
    /// <paramref name="date"/> that falls on a non-working day resolves to the index of the last
    /// working day at or before it — the frontend has no client-side view of the tenant's
    /// calendar (F7 follow-up), so a drag that lands on a weekend degrades gracefully here
    /// instead of needing to be rejected or silently rounded on the client.
    /// </summary>
    public int WorkingDayIndex(DateOnly anchor, DateOnly date)
    {
        if (date <= anchor)
        {
            return 0;
        }

        var index = 0;
        var current = anchor;
        while (current < date)
        {
            current = current.AddDays(1);
            if (IsWorkingDay(current))
            {
                index++;
            }
        }

        return index;
    }
}
