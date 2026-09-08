using SmoPmo.Pmo.Scheduling;
using Xunit;

namespace Pmo.Tests;

/// <summary>
/// Pure, no-database tests of the forward/backward CPM pass and the working-calendar index↔
/// date conversion — the two layers B6 wires into the API (architecture-mvp.md §7, layers 2-3).
/// </summary>
public sealed class CriticalPathEngineTests
{
    [Fact]
    public void ALinearChainIsEntirelyCriticalWithZeroFloat()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();

        var result = CriticalPathEngine.Compute(
            new[]
            {
                new CriticalPathEngine.TaskInput(a, 3),
                new CriticalPathEngine.TaskInput(b, 2),
                new CriticalPathEngine.TaskInput(c, 1)
            },
            new[]
            {
                new CriticalPathEngine.DependencyInput(a, b, DependencyType.FS, 0),
                new CriticalPathEngine.DependencyInput(b, c, DependencyType.FS, 0)
            });

        Assert.Equal(6, result.ProjectDurationDays);
        Assert.Equal((0, 3), (result.Tasks[a].EarlyStart, result.Tasks[a].EarlyFinish));
        Assert.Equal((3, 5), (result.Tasks[b].EarlyStart, result.Tasks[b].EarlyFinish));
        Assert.Equal((5, 6), (result.Tasks[c].EarlyStart, result.Tasks[c].EarlyFinish));
        Assert.All(result.Tasks.Values, t => Assert.Equal(0, t.TotalFloat));
        Assert.All(result.Tasks.Values, t => Assert.True(t.IsCritical));
    }

    [Fact]
    public void AShorterParallelPathHasPositiveFloatAndIsNotCritical()
    {
        // A(5d) and B(2d) both feed C(2d) via FS. A is the long pole; B has 3 days of slack.
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();

        var result = CriticalPathEngine.Compute(
            new[]
            {
                new CriticalPathEngine.TaskInput(a, 5),
                new CriticalPathEngine.TaskInput(b, 2),
                new CriticalPathEngine.TaskInput(c, 2)
            },
            new[]
            {
                new CriticalPathEngine.DependencyInput(a, c, DependencyType.FS, 0),
                new CriticalPathEngine.DependencyInput(b, c, DependencyType.FS, 0)
            });

        Assert.Equal(7, result.ProjectDurationDays);

        Assert.True(result.Tasks[a].IsCritical);
        Assert.Equal(0, result.Tasks[a].TotalFloat);

        Assert.False(result.Tasks[b].IsCritical);
        Assert.Equal(3, result.Tasks[b].TotalFloat);

        Assert.True(result.Tasks[c].IsCritical);
        Assert.Equal(0, result.Tasks[c].TotalFloat);
    }

    [Fact]
    public void FinishToStartLagPushesTheSuccessorOut()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var result = CriticalPathEngine.Compute(
            new[] { new CriticalPathEngine.TaskInput(a, 3), new CriticalPathEngine.TaskInput(b, 2) },
            new[] { new CriticalPathEngine.DependencyInput(a, b, DependencyType.FS, 2) });

        // EF(a) = 3, plus 2 days lag before b can start.
        Assert.Equal(5, result.Tasks[b].EarlyStart);
        Assert.Equal(7, result.Tasks[b].EarlyFinish);
    }

    [Fact]
    public void StartToStartMeansTheSuccessorCanStartAlongsideThePredecessor()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var result = CriticalPathEngine.Compute(
            new[] { new CriticalPathEngine.TaskInput(a, 5), new CriticalPathEngine.TaskInput(b, 2) },
            new[] { new CriticalPathEngine.DependencyInput(a, b, DependencyType.SS, 0) });

        Assert.Equal(0, result.Tasks[b].EarlyStart);
        Assert.Equal(2, result.Tasks[b].EarlyFinish);
    }

    [Fact]
    public void FinishToFinishMeansTheSuccessorMustEndNoEarlierThanThePredecessor()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var result = CriticalPathEngine.Compute(
            new[] { new CriticalPathEngine.TaskInput(a, 8), new CriticalPathEngine.TaskInput(b, 2) },
            new[] { new CriticalPathEngine.DependencyInput(a, b, DependencyType.FF, 0) });

        Assert.Equal(8, result.Tasks[a].EarlyFinish);
        Assert.Equal(8, result.Tasks[b].EarlyFinish);
        Assert.Equal(6, result.Tasks[b].EarlyStart);
    }

    [Fact]
    public void StartToFinishMeansTheSuccessorMustEndNoEarlierThanThePredecessorStarts()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var result = CriticalPathEngine.Compute(
            new[] { new CriticalPathEngine.TaskInput(a, 5), new CriticalPathEngine.TaskInput(b, 2) },
            new[] { new CriticalPathEngine.DependencyInput(a, b, DependencyType.SF, 0) });

        // a is unconstrained (no predecessor of its own) so ES(a) = 0; b must finish no
        // earlier than a starts, i.e. EF(b) >= 0, which is already true for any non-negative
        // start — so b is free to start at 0 too. Give a a predecessor to make ES(a) nonzero.
        var lead = Guid.NewGuid();
        result = CriticalPathEngine.Compute(
            new[]
            {
                new CriticalPathEngine.TaskInput(lead, 5),
                new CriticalPathEngine.TaskInput(a, 5),
                new CriticalPathEngine.TaskInput(b, 2)
            },
            new[]
            {
                new CriticalPathEngine.DependencyInput(lead, a, DependencyType.FS, 0),
                new CriticalPathEngine.DependencyInput(a, b, DependencyType.SF, 0)
            });

        Assert.Equal(5, result.Tasks[a].EarlyStart);
        Assert.Equal(5, result.Tasks[b].EarlyFinish);
        Assert.Equal(3, result.Tasks[b].EarlyStart);
    }

    [Fact]
    public void AMilestoneStartsAndFinishesOnTheSameIndex()
    {
        var a = Guid.NewGuid();
        var milestone = Guid.NewGuid();

        var result = CriticalPathEngine.Compute(
            new[] { new CriticalPathEngine.TaskInput(a, 4), new CriticalPathEngine.TaskInput(milestone, 0) },
            new[] { new CriticalPathEngine.DependencyInput(a, milestone, DependencyType.FS, 0) });

        Assert.Equal(4, result.Tasks[milestone].EarlyStart);
        Assert.Equal(4, result.Tasks[milestone].EarlyFinish);
    }

    [Fact]
    public void ACycleIsRejectedRatherThanLoopingForever()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        Assert.Throws<CyclicScheduleException>(() => CriticalPathEngine.Compute(
            new[] { new CriticalPathEngine.TaskInput(a, 1), new CriticalPathEngine.TaskInput(b, 1) },
            new[]
            {
                new CriticalPathEngine.DependencyInput(a, b, DependencyType.FS, 0),
                new CriticalPathEngine.DependencyInput(b, a, DependencyType.FS, 0)
            }));
    }

    [Fact]
    public void ADependencyReferencingAnUnknownTaskIsRejected()
    {
        var a = Guid.NewGuid();
        var ghost = Guid.NewGuid();

        Assert.Throws<UnknownScheduleTaskException>(() => CriticalPathEngine.Compute(
            new[] { new CriticalPathEngine.TaskInput(a, 1) },
            new[] { new CriticalPathEngine.DependencyInput(a, ghost, DependencyType.FS, 0) }));
    }

    [Fact]
    public void NoTasksProducesAnEmptyZeroDurationSchedule()
    {
        var result = CriticalPathEngine.Compute(
            Array.Empty<CriticalPathEngine.TaskInput>(),
            Array.Empty<CriticalPathEngine.DependencyInput>());

        Assert.Empty(result.Tasks);
        Assert.Equal(0, result.ProjectDurationDays);
    }

    // ──────────────────── ConstraintStartDay (F8: drag-to-move) ────────────────────

    [Fact]
    public void AConstraintLaterThanTheDependencyDrivenStartPushesTheTaskAndItsSuccessorOut()
    {
        // A(3d) -> B(2d) would normally put B at [3,5). Dragging B out to day 10 (F8) should
        // push B there, and its own successor C along with it, even though nothing about A or
        // the dependency graph changed.
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();

        var result = CriticalPathEngine.Compute(
            new[]
            {
                new CriticalPathEngine.TaskInput(a, 3),
                new CriticalPathEngine.TaskInput(b, 2, ConstraintStartDay: 10),
                new CriticalPathEngine.TaskInput(c, 1)
            },
            new[]
            {
                new CriticalPathEngine.DependencyInput(a, b, DependencyType.FS, 0),
                new CriticalPathEngine.DependencyInput(b, c, DependencyType.FS, 0)
            });

        Assert.Equal((10, 12), (result.Tasks[b].EarlyStart, result.Tasks[b].EarlyFinish));
        Assert.Equal((12, 13), (result.Tasks[c].EarlyStart, result.Tasks[c].EarlyFinish));
    }

    [Fact]
    public void AConstraintEarlierThanTheDependencyDrivenStartHasNoEffect()
    {
        // "Start no earlier than" is a floor, not a pin — it never schedules a task earlier
        // than its dependencies allow.
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var result = CriticalPathEngine.Compute(
            new[]
            {
                new CriticalPathEngine.TaskInput(a, 5),
                new CriticalPathEngine.TaskInput(b, 2, ConstraintStartDay: 1)
            },
            new[] { new CriticalPathEngine.DependencyInput(a, b, DependencyType.FS, 0) });

        Assert.Equal(5, result.Tasks[b].EarlyStart);
    }

    [Fact]
    public void AConstraintOnAnUnconstrainedRootTaskGivesItAStartWhereItHadNone()
    {
        var a = Guid.NewGuid();

        var result = CriticalPathEngine.Compute(
            new[] { new CriticalPathEngine.TaskInput(a, 3, ConstraintStartDay: 7) },
            Array.Empty<CriticalPathEngine.DependencyInput>());

        Assert.Equal((7, 10), (result.Tasks[a].EarlyStart, result.Tasks[a].EarlyFinish));
    }

    // ─────────────────────────── WorkingCalendar ───────────────────────────

    private static readonly DateOnly Monday = new(2024, 1, 1);

    [Fact]
    public void DefaultCalendarTreatsFridayAndSaturdayAsWeekend()
    {
        var calendar = WorkingCalendar.Default;

        Assert.True(calendar.IsWorkingDay(Monday));
        Assert.False(calendar.IsWorkingDay(new DateOnly(2024, 1, 5))); // Friday
        Assert.False(calendar.IsWorkingDay(new DateOnly(2024, 1, 6))); // Saturday
        Assert.True(calendar.IsWorkingDay(new DateOnly(2024, 1, 7)));  // Sunday
    }

    [Fact]
    public void AddWorkingDaysSkipsTheWeekend()
    {
        var calendar = WorkingCalendar.Default;

        // Mon(0) Tue(1) Wed(2) Thu(3) [Fri skip] [Sat skip] Sun(4)
        Assert.Equal(new DateOnly(2024, 1, 7), calendar.AddWorkingDays(Monday, 4));
        Assert.Equal(Monday, calendar.AddWorkingDays(Monday, 0));
    }

    [Fact]
    public void FirstWorkingDayOnOrAfterRollsForwardOverTheWeekend()
    {
        var calendar = WorkingCalendar.Default;

        Assert.Equal(new DateOnly(2024, 1, 7), calendar.FirstWorkingDayOnOrAfter(new DateOnly(2024, 1, 5)));
        Assert.Equal(Monday, calendar.FirstWorkingDayOnOrAfter(Monday));
    }

    [Fact]
    public void WorkingDayIndexIsTheInverseOfAddWorkingDays()
    {
        var calendar = WorkingCalendar.Default;

        Assert.Equal(4, calendar.WorkingDayIndex(Monday, new DateOnly(2024, 1, 7)));
        Assert.Equal(0, calendar.WorkingDayIndex(Monday, Monday));
    }

    [Fact]
    public void WorkingDayIndexClampsADateOnOrBeforeTheAnchorToZero()
    {
        var calendar = WorkingCalendar.Default;

        Assert.Equal(0, calendar.WorkingDayIndex(Monday, Monday.AddDays(-3)));
    }

    [Fact]
    public void WorkingDayIndexOnAWeekendResolvesToTheLastWorkingDayAtOrBeforeIt()
    {
        var calendar = WorkingCalendar.Default;

        // Friday (2024-01-05) is a weekend; the last working day at/before it is Thursday,
        // 3 working days after Monday.
        Assert.Equal(3, calendar.WorkingDayIndex(Monday, new DateOnly(2024, 1, 5)));
    }
}
