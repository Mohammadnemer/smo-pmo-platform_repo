namespace SmoPmo.Pmo.Scheduling;

public enum DependencyType
{
    FS,
    SS,
    FF,
    SF
}

/// <summary>A dependency graph references a task this engine was not given a duration for.</summary>
public sealed class UnknownScheduleTaskException : Exception
{
    public UnknownScheduleTaskException(Guid taskId) : base($"Dependency references unknown task '{taskId}'.")
    {
    }
}

/// <summary>The dependency graph is not a DAG — CPM has no forward/backward pass over a cycle.</summary>
public sealed class CyclicScheduleException : Exception
{
    public CyclicScheduleException() : base("The project's dependencies contain a cycle; no schedule can be computed.")
    {
    }
}

/// <summary>
/// Forward/backward pass CPM (architecture-mvp.md §7, layer 3): all four dependency types
/// (FS/SS/FF/SF) with lag, total float, critical path, plus an optional per-task "start no
/// earlier than" constraint (F8 — dragging a task in the Gantt) that floors the forward pass.
/// Works entirely in integer working-day index space — <see cref="WorkingCalendar"/> is the
/// only thing that ever turns an index into a calendar date, so this class has no notion of
/// weekends, holidays or timezones.
///
/// Durations use an exclusive-end convention: a task occupies indices
/// [EarlyStart, EarlyStart + Duration). A milestone has Duration 0, so its start and finish
/// index are the same index.
/// </summary>
public static class CriticalPathEngine
{
    /// <summary><paramref name="ConstraintStartDay"/> is a "start no earlier than" floor in the
    /// same working-day index space as everything else here — <see langword="null"/> when the
    /// task is purely dependency-driven (the engine's original, still-default behaviour).</summary>
    public readonly record struct TaskInput(Guid Id, int DurationDays, int? ConstraintStartDay = null);

    public readonly record struct DependencyInput(Guid PredecessorId, Guid SuccessorId, DependencyType Type, int LagDays);

    public readonly record struct TaskSchedule(
        Guid Id,
        int EarlyStart,
        int EarlyFinish,
        int LateStart,
        int LateFinish,
        int TotalFloat,
        bool IsCritical);

    public sealed record ScheduleResult(IReadOnlyDictionary<Guid, TaskSchedule> Tasks, int ProjectDurationDays);

    public static ScheduleResult Compute(IReadOnlyList<TaskInput> tasks, IReadOnlyList<DependencyInput> dependencies)
    {
        var duration = new Dictionary<Guid, int>(tasks.Count);
        foreach (var task in tasks)
        {
            duration[task.Id] = task.DurationDays;
        }

        foreach (var dependency in dependencies)
        {
            if (!duration.ContainsKey(dependency.PredecessorId))
            {
                throw new UnknownScheduleTaskException(dependency.PredecessorId);
            }

            if (!duration.ContainsKey(dependency.SuccessorId))
            {
                throw new UnknownScheduleTaskException(dependency.SuccessorId);
            }
        }

        var outgoing = tasks.ToDictionary(t => t.Id, _ => new List<DependencyInput>());
        var incoming = tasks.ToDictionary(t => t.Id, _ => new List<DependencyInput>());
        foreach (var dependency in dependencies)
        {
            outgoing[dependency.PredecessorId].Add(dependency);
            incoming[dependency.SuccessorId].Add(dependency);
        }

        var topoOrder = TopologicalSort(tasks, outgoing);

        var earlyStart = new Dictionary<Guid, int>(tasks.Count);
        var earlyFinish = new Dictionary<Guid, int>(tasks.Count);

        var constraintFloor = new Dictionary<Guid, int>(tasks.Count);
        foreach (var task in tasks)
        {
            constraintFloor[task.Id] = task.ConstraintStartDay ?? 0;
        }

        foreach (var taskId in topoOrder)
        {
            var start = constraintFloor[taskId];
            foreach (var edge in incoming[taskId])
            {
                start = Math.Max(start, ForwardConstraint(edge, earlyStart, earlyFinish, duration));
            }

            earlyStart[taskId] = start;
            earlyFinish[taskId] = start + duration[taskId];
        }

        var projectDuration = earlyFinish.Count == 0 ? 0 : earlyFinish.Values.Max();

        var lateStart = new Dictionary<Guid, int>(tasks.Count);
        var lateFinish = new Dictionary<Guid, int>(tasks.Count);

        for (var i = topoOrder.Count - 1; i >= 0; i--)
        {
            var taskId = topoOrder[i];
            var finish = projectDuration;
            foreach (var edge in outgoing[taskId])
            {
                finish = Math.Min(finish, BackwardConstraint(edge, lateStart, lateFinish, duration));
            }

            lateFinish[taskId] = finish;
            lateStart[taskId] = finish - duration[taskId];
        }

        var result = new Dictionary<Guid, TaskSchedule>(tasks.Count);
        foreach (var task in tasks)
        {
            var totalFloat = lateStart[task.Id] - earlyStart[task.Id];
            result[task.Id] = new TaskSchedule(
                task.Id,
                earlyStart[task.Id],
                earlyFinish[task.Id],
                lateStart[task.Id],
                lateFinish[task.Id],
                totalFloat,
                IsCritical: totalFloat <= 0);
        }

        return new ScheduleResult(result, projectDuration);
    }

    private static int ForwardConstraint(
        DependencyInput edge,
        IReadOnlyDictionary<Guid, int> earlyStart,
        IReadOnlyDictionary<Guid, int> earlyFinish,
        IReadOnlyDictionary<Guid, int> duration)
    {
        var predStart = earlyStart[edge.PredecessorId];
        var predFinish = earlyFinish[edge.PredecessorId];
        var succDuration = duration[edge.SuccessorId];

        return edge.Type switch
        {
            DependencyType.FS => predFinish + edge.LagDays,
            DependencyType.SS => predStart + edge.LagDays,
            DependencyType.FF => predFinish + edge.LagDays - succDuration,
            DependencyType.SF => predStart + edge.LagDays - succDuration,
            _ => throw new ArgumentOutOfRangeException(nameof(edge))
        };
    }

    private static int BackwardConstraint(
        DependencyInput edge,
        IReadOnlyDictionary<Guid, int> lateStart,
        IReadOnlyDictionary<Guid, int> lateFinish,
        IReadOnlyDictionary<Guid, int> duration)
    {
        var succStart = lateStart[edge.SuccessorId];
        var succFinish = lateFinish[edge.SuccessorId];
        var predDuration = duration[edge.PredecessorId];

        return edge.Type switch
        {
            DependencyType.FS => succStart - edge.LagDays,
            DependencyType.SS => succStart - edge.LagDays + predDuration,
            DependencyType.FF => succFinish - edge.LagDays,
            DependencyType.SF => succFinish - edge.LagDays + predDuration,
            _ => throw new ArgumentOutOfRangeException(nameof(edge))
        };
    }

    /// <summary>Kahn's algorithm. Throws <see cref="CyclicScheduleException"/> rather than
    /// returning a partial order — a cyclic graph has no valid forward/backward pass.</summary>
    private static List<Guid> TopologicalSort(IReadOnlyList<TaskInput> tasks, IReadOnlyDictionary<Guid, List<DependencyInput>> outgoing)
    {
        var inDegree = tasks.ToDictionary(t => t.Id, _ => 0);
        foreach (var edges in outgoing.Values)
        {
            foreach (var edge in edges)
            {
                inDegree[edge.SuccessorId]++;
            }
        }

        var queue = new Queue<Guid>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var order = new List<Guid>(tasks.Count);

        while (queue.Count > 0)
        {
            var taskId = queue.Dequeue();
            order.Add(taskId);

            foreach (var edge in outgoing[taskId])
            {
                if (--inDegree[edge.SuccessorId] == 0)
                {
                    queue.Enqueue(edge.SuccessorId);
                }
            }
        }

        if (order.Count != tasks.Count)
        {
            throw new CyclicScheduleException();
        }

        return order;
    }
}
