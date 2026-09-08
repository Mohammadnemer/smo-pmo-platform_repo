using System.Net;
using System.Net.Http.Json;
using SmoPmo.Pmo;
using Xunit;

namespace Pmo.Tests;

/// <summary>
/// B6's acceptance line: saving a schedule and asking for its critical path returns the right
/// tasks. Nothing here touches a DbContext to set data up — every row is created over HTTP,
/// the same discipline SMO's ScorecardEndToEndTests (B5) used.
/// </summary>
public sealed class ScheduleEndToEndTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // A Monday, so the CPM math below is easy to hand-verify against the Fri–Sat default.
    private static readonly DateOnly ProjectStart = new(2024, 1, 1);

    [Fact]
    public async Task SavingAScheduleAndAskingForItsCriticalPathReturnsTheRightTasks()
    {
        await using var host = await PmoTestHost.StartAsync();
        var client = host.CreateClient(Tenant, "PM");

        var portfolio = await client.PostAndReadAsync<PortfolioResponse>("/api/pmo/portfolios",
            new PortfolioWriteModel("Digital transformation", null));
        var program = await client.PostAndReadAsync<ProgramResponse>("/api/pmo/programs",
            new ProgramWriteModel(portfolio.Id, "Core platform", null));
        var project = await client.PostAndReadAsync<ProjectResponse>("/api/pmo/projects",
            new ProjectWriteModel(program.Id, "Rebuild checkout", null, ProjectStart, "Draft"));

        // A(5d) and B(2d) both feed C(2d): A is the long pole, B has slack.
        var a = await client.PostAndReadAsync<TaskResponse>("/api/pmo/tasks",
            new TaskWriteModel(project.Id, "Design", null, "1.1", DurationDays: 5, IsMilestone: false, PercentComplete: 0m, AssigneeUserId: null));
        var b = await client.PostAndReadAsync<TaskResponse>("/api/pmo/tasks",
            new TaskWriteModel(project.Id, "Procurement", null, "1.2", DurationDays: 2, IsMilestone: false, PercentComplete: 0m, AssigneeUserId: null));
        var c = await client.PostAndReadAsync<TaskResponse>("/api/pmo/tasks",
            new TaskWriteModel(project.Id, "Integrate", null, "1.3", DurationDays: 2, IsMilestone: false, PercentComplete: 0m, AssigneeUserId: null));

        await client.PostAndReadAsync<DependencyResponse>("/api/pmo/dependencies",
            new DependencyWriteModel(project.Id, a.Id, c.Id, "FS", LagDays: 0));
        await client.PostAndReadAsync<DependencyResponse>("/api/pmo/dependencies",
            new DependencyWriteModel(project.Id, b.Id, c.Id, "FS", LagDays: 0));

        // Asking for the critical path.
        var criticalPath = await client.GetFromJsonAsync<CriticalPathResponse>($"/api/pmo/projects/{project.Id}/critical-path");

        Assert.NotNull(criticalPath);
        Assert.Equal(new[] { "Design", "Integrate" }, criticalPath!.Tasks.Select(t => t.Name));
        Assert.DoesNotContain(criticalPath.Tasks, t => t.Name == "Procurement");
        Assert.All(criticalPath.Tasks, t => Assert.True(t.IsCritical));
        Assert.All(criticalPath.Tasks, t => Assert.Equal(0, t.TotalFloatDays));

        // The dates are calendar-aware: Jan 1 2024 is a Monday, so Design (5 working days
        // starting Monday) spans the following weekend and Integrate starts the Monday after.
        var design = criticalPath.Tasks.Single(t => t.Name == "Design");
        Assert.Equal(new DateOnly(2024, 1, 1), design.ScheduleStart);
        Assert.Equal(new DateOnly(2024, 1, 7), design.ScheduleFinish);

        var integrate = criticalPath.Tasks.Single(t => t.Name == "Integrate");
        Assert.Equal(new DateOnly(2024, 1, 8), integrate.ScheduleStart);
        Assert.Equal(new DateOnly(2024, 1, 9), integrate.ScheduleFinish);
        Assert.Equal(new DateOnly(2024, 1, 9), criticalPath.ProjectFinish);

        // The full schedule aggregate agrees, and also reports Procurement's slack.
        var schedule = await client.GetFromJsonAsync<ScheduleResponse>($"/api/pmo/projects/{project.Id}/schedule");
        Assert.NotNull(schedule);
        Assert.Equal(3, schedule!.Tasks.Count);
        Assert.Equal(2, schedule.Dependencies.Count);
        Assert.Equal(new DateOnly(2024, 1, 1), schedule.CalendarAnchor);
        Assert.Equal(new DateOnly(2024, 1, 9), schedule.ProjectFinish);

        var procurement = schedule.Tasks.Single(t => t.Name == "Procurement");
        Assert.False(procurement.IsCritical);
        Assert.Equal(3, procurement.TotalFloatDays);
    }

    [Fact]
    public async Task ADependencyThatWouldCreateACycleIsRejectedAndNothingIsWritten()
    {
        await using var host = await PmoTestHost.StartAsync();
        var client = host.CreateClient(Tenant, "PM");

        var portfolio = await client.PostAndReadAsync<PortfolioResponse>("/api/pmo/portfolios", new PortfolioWriteModel("P", null));
        var program = await client.PostAndReadAsync<ProgramResponse>("/api/pmo/programs", new ProgramWriteModel(portfolio.Id, "Prog", null));
        var project = await client.PostAndReadAsync<ProjectResponse>("/api/pmo/projects",
            new ProjectWriteModel(program.Id, "Project", null, ProjectStart, "Draft"));

        var a = await client.PostAndReadAsync<TaskResponse>("/api/pmo/tasks",
            new TaskWriteModel(project.Id, "A", null, null, 2, false, 0m, null));
        var b = await client.PostAndReadAsync<TaskResponse>("/api/pmo/tasks",
            new TaskWriteModel(project.Id, "B", null, null, 2, false, 0m, null));

        await client.PostAndReadAsync<DependencyResponse>("/api/pmo/dependencies",
            new DependencyWriteModel(project.Id, a.Id, b.Id, "FS", 0));

        // B already depends on A; A depending on B too would be a cycle.
        var cyclic = await client.PostAsJsonAsync("/api/pmo/dependencies",
            new DependencyWriteModel(project.Id, b.Id, a.Id, "FS", 0));
        Assert.Equal(HttpStatusCode.BadRequest, cyclic.StatusCode);

        var dependencies = await client.GetFromJsonAsync<List<DependencyResponse>>($"/api/pmo/dependencies?projectId={project.Id}");
        Assert.Single(dependencies!);
    }

    [Fact]
    public async Task DeletingATaskAlsoRemovesTheDependenciesThatReferencedItAndReschedulesTheRest()
    {
        await using var host = await PmoTestHost.StartAsync();
        var client = host.CreateClient(Tenant, "PM");

        var portfolio = await client.PostAndReadAsync<PortfolioResponse>("/api/pmo/portfolios", new PortfolioWriteModel("P", null));
        var program = await client.PostAndReadAsync<ProgramResponse>("/api/pmo/programs", new ProgramWriteModel(portfolio.Id, "Prog", null));
        var project = await client.PostAndReadAsync<ProjectResponse>("/api/pmo/projects",
            new ProjectWriteModel(program.Id, "Project", null, ProjectStart, "Draft"));

        var a = await client.PostAndReadAsync<TaskResponse>("/api/pmo/tasks",
            new TaskWriteModel(project.Id, "A", null, null, 3, false, 0m, null));
        var b = await client.PostAndReadAsync<TaskResponse>("/api/pmo/tasks",
            new TaskWriteModel(project.Id, "B", null, null, 2, false, 0m, null));

        await client.PostAndReadAsync<DependencyResponse>("/api/pmo/dependencies",
            new DependencyWriteModel(project.Id, a.Id, b.Id, "FS", 0));

        var delete = await client.DeleteAsync($"/api/pmo/tasks/{a.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var dependencies = await client.GetFromJsonAsync<List<DependencyResponse>>($"/api/pmo/dependencies?projectId={project.Id}");
        Assert.Empty(dependencies!);

        var remaining = await client.GetFromJsonAsync<TaskResponse>($"/api/pmo/tasks/{b.Id}");
        Assert.Equal(ProjectStart, remaining!.ScheduleStart);
    }

    [Fact]
    public async Task DraggingATaskToALaterDateReschedulesItsDependentsInPlace()
    {
        // F8's own acceptance line, verified over real HTTP: A(3d) -> B(2d), B starts right
        // after A finishes. "Dragging" B is a PUT with a ConstraintStart floor — B's dependent,
        // C, must move out with it even though nothing about C or the dependency graph changed.
        await using var host = await PmoTestHost.StartAsync();
        var client = host.CreateClient(Tenant, "PM");

        var portfolio = await client.PostAndReadAsync<PortfolioResponse>("/api/pmo/portfolios", new PortfolioWriteModel("P", null));
        var program = await client.PostAndReadAsync<ProgramResponse>("/api/pmo/programs", new ProgramWriteModel(portfolio.Id, "Prog", null));
        var project = await client.PostAndReadAsync<ProjectResponse>("/api/pmo/projects",
            new ProjectWriteModel(program.Id, "Project", null, ProjectStart, "Draft"));

        var a = await client.PostAndReadAsync<TaskResponse>("/api/pmo/tasks",
            new TaskWriteModel(project.Id, "A", null, null, 3, false, 0m, null));
        var b = await client.PostAndReadAsync<TaskResponse>("/api/pmo/tasks",
            new TaskWriteModel(project.Id, "B", null, null, 2, false, 0m, null));
        var c = await client.PostAndReadAsync<TaskResponse>("/api/pmo/tasks",
            new TaskWriteModel(project.Id, "C", null, null, 1, false, 0m, null));

        await client.PostAndReadAsync<DependencyResponse>("/api/pmo/dependencies",
            new DependencyWriteModel(project.Id, a.Id, b.Id, "FS", 0));
        await client.PostAndReadAsync<DependencyResponse>("/api/pmo/dependencies",
            new DependencyWriteModel(project.Id, b.Id, c.Id, "FS", 0));

        // Before the drag: A [Jan1-Jan3], B starts Jan4, C starts Jan5 (all working days).
        var before = await client.GetFromJsonAsync<TaskResponse>($"/api/pmo/tasks/{b.Id}");
        Assert.Equal(new DateOnly(2024, 1, 4), before!.ScheduleStart);

        // Drag B two weeks out.
        var dragged = new DateOnly(2024, 1, 18);
        var updateResponse = await client.PutAsJsonAsync($"/api/pmo/tasks/{b.Id}",
            new TaskWriteModel(project.Id, "B", null, null, 2, false, 0m, null, ConstraintStart: dragged));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var movedB = await client.GetFromJsonAsync<TaskResponse>($"/api/pmo/tasks/{b.Id}");
        Assert.Equal(dragged, movedB!.ScheduleStart);
        Assert.Equal(dragged, movedB.ConstraintStart);

        // C had no drag applied to it directly, but B's move must carry it along.
        var movedC = await client.GetFromJsonAsync<TaskResponse>($"/api/pmo/tasks/{c.Id}");
        Assert.Equal(new DateOnly(2024, 1, 22), movedC!.ScheduleStart); // Jan 19-20 (Fri-Sat) is the weekend.

        // A is upstream of the dragged task — unaffected.
        var untouchedA = await client.GetFromJsonAsync<TaskResponse>($"/api/pmo/tasks/{a.Id}");
        Assert.Equal(ProjectStart, untouchedA!.ScheduleStart);
    }

    [Fact]
    public async Task AMilestoneMustHaveZeroDurationAndANonMilestoneMustHaveAtLeastOneDay()
    {
        await using var host = await PmoTestHost.StartAsync();
        var client = host.CreateClient(Tenant, "PM");

        var portfolio = await client.PostAndReadAsync<PortfolioResponse>("/api/pmo/portfolios", new PortfolioWriteModel("P", null));
        var program = await client.PostAndReadAsync<ProgramResponse>("/api/pmo/programs", new ProgramWriteModel(portfolio.Id, "Prog", null));
        var project = await client.PostAndReadAsync<ProjectResponse>("/api/pmo/projects",
            new ProjectWriteModel(program.Id, "Project", null, ProjectStart, "Draft"));

        var badMilestone = await client.PostAsJsonAsync("/api/pmo/tasks",
            new TaskWriteModel(project.Id, "Kickoff", null, null, DurationDays: 1, IsMilestone: true, PercentComplete: 0m, AssigneeUserId: null));
        Assert.Equal(HttpStatusCode.BadRequest, badMilestone.StatusCode);

        var zeroDurationTask = await client.PostAsJsonAsync("/api/pmo/tasks",
            new TaskWriteModel(project.Id, "Build", null, null, DurationDays: 0, IsMilestone: false, PercentComplete: 0m, AssigneeUserId: null));
        Assert.Equal(HttpStatusCode.BadRequest, zeroDurationTask.StatusCode);
    }
}
