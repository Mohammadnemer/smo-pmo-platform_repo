using Microsoft.EntityFrameworkCore;

namespace SmoPmo.Pmo;

/// <summary>
/// Dev-only demo data: two portfolios, a few programs and projects, a flagship project with
/// a real task/dependency network (so its CPM-computed schedule has an actual critical path
/// to look at), and RAID items spread across all four categories/severities — so F6's
/// portfolio/program/project lists and the project workspace have real data to render
/// against instead of an empty tenant, the same call F4 made for the scorecard.
///
/// Only ever called from Program.cs behind an <c>IsDevelopment()</c> check — never runs
/// against a real tenant. Idempotent: a portfolio already existing for the current tenant
/// means a prior run already seeded it.
/// </summary>
public static class PmoDemoSeed
{
    public static async Task SeedAsync(PmoDbContext db, CancellationToken ct = default)
    {
        if (await db.Portfolios.AnyAsync(ct))
        {
            return;
        }

        var platformPortfolio = new Portfolio
        {
            Id = Guid.NewGuid(),
            Name = "Digital Platform Portfolio",
            Description = "Modernizing the core platform and its data foundations."
        };
        var cxPortfolio = new Portfolio
        {
            Id = Guid.NewGuid(),
            Name = "Customer Experience Portfolio",
            Description = "Programs that touch how customers reach and use support."
        };
        db.Portfolios.AddRange(platformPortfolio, cxPortfolio);

        var coreProgram = new Program
        {
            Id = Guid.NewGuid(),
            PortfolioId = platformPortfolio.Id,
            Name = "Core Platform Program",
            Description = "Tenant-facing platform capabilities."
        };
        var dataProgram = new Program
        {
            Id = Guid.NewGuid(),
            PortfolioId = platformPortfolio.Id,
            Name = "Data & Analytics Program",
            Description = "Reporting and analytics infrastructure."
        };
        var cxProgram = new Program
        {
            Id = Guid.NewGuid(),
            PortfolioId = cxPortfolio.Id,
            Name = "CX Modernization Program",
            Description = "Omnichannel support experience."
        };
        db.Programs.AddRange(coreProgram, dataProgram, cxProgram);

        var onboardingProject = new Project
        {
            Id = Guid.NewGuid(),
            ProgramId = coreProgram.Id,
            Name = "Tenant Onboarding Revamp",
            Description = "Rebuild the new-tenant onboarding flow end to end.",
            StartDate = new DateOnly(2026, 1, 5),
            Status = "Active"
        };
        var selfServiceProject = new Project
        {
            Id = Guid.NewGuid(),
            ProgramId = coreProgram.Id,
            Name = "Employee Self-Service Portal",
            Description = "Internal portal for common HR/IT requests.",
            StartDate = new DateOnly(2026, 2, 1),
            Status = "Draft"
        };
        var reportingProject = new Project
        {
            Id = Guid.NewGuid(),
            ProgramId = dataProgram.Id,
            Name = "Executive Reporting Warehouse",
            Description = "Consolidated warehouse feeding the executive dashboard.",
            StartDate = new DateOnly(2026, 3, 1),
            Status = "Draft"
        };
        var omnichannelProject = new Project
        {
            Id = Guid.NewGuid(),
            ProgramId = cxProgram.Id,
            Name = "Omnichannel Support Launch",
            Description = "Unify chat, email and phone support into one queue.",
            StartDate = new DateOnly(2026, 1, 15),
            Status = "Active"
        };
        db.Projects.AddRange(onboardingProject, selfServiceProject, reportingProject, omnichannelProject);

        // Flagship project: a diamond-shaped network (T3's branch is longer than T4's) so the
        // CPM engine actually produces a partial critical path once recomputed below, rather
        // than every task trivially being critical the way a single chain would be.
        var t1 = NewTask(onboardingProject.Id, "Requirements & Scoping", "1.0", 5);
        var t2 = NewTask(onboardingProject.Id, "Design Tenant Data Model", "2.0", 8);
        var t3 = NewTask(onboardingProject.Id, "Build Onboarding API", "3.0", 10);
        var t4 = NewTask(onboardingProject.Id, "Build Onboarding UI", "3.1", 7);
        var t5 = NewTask(onboardingProject.Id, "Integration Testing", "4.0", 4);
        var t6 = NewTask(onboardingProject.Id, "Go-Live", "5.0", 0, isMilestone: true);
        db.Tasks.AddRange(t1, t2, t3, t4, t5, t6);
        db.Dependencies.AddRange(
            NewDependency(onboardingProject.Id, t1.Id, t2.Id),
            NewDependency(onboardingProject.Id, t2.Id, t3.Id),
            NewDependency(onboardingProject.Id, t2.Id, t4.Id),
            NewDependency(onboardingProject.Id, t3.Id, t5.Id),
            NewDependency(onboardingProject.Id, t4.Id, t5.Id),
            NewDependency(onboardingProject.Id, t5.Id, t6.Id));

        // Simpler two-task chains for the other projects, so their workspaces show a
        // computed schedule too rather than an empty task list.
        var s1 = NewTask(selfServiceProject.Id, "Catalog Common Requests", "1.0", 4);
        var s2 = NewTask(selfServiceProject.Id, "Build Request Portal", "2.0", 9);
        db.Tasks.AddRange(s1, s2);
        db.Dependencies.Add(NewDependency(selfServiceProject.Id, s1.Id, s2.Id));

        var r1 = NewTask(reportingProject.Id, "Define Reporting Model", "1.0", 6);
        var r2 = NewTask(reportingProject.Id, "Build Warehouse Pipeline", "2.0", 12);
        db.Tasks.AddRange(r1, r2);
        db.Dependencies.Add(NewDependency(reportingProject.Id, r1.Id, r2.Id));

        var o1 = NewTask(omnichannelProject.Id, "Vendor Selection", "1.0", 5);
        var o2 = NewTask(omnichannelProject.Id, "Queue Integration", "2.0", 8);
        db.Tasks.AddRange(o1, o2);
        db.Dependencies.Add(NewDependency(omnichannelProject.Id, o1.Id, o2.Id));

        // Saved before the RaidItems below: RaidItem's Portfolio/Program/ProjectId are bare
        // Guid columns with no HasOne(...) configured in PmoDbContext (exactly one of three
        // optional parents, not a single navigable relationship), so EF's change tracker has
        // no dependency edge telling it to insert parents first — without a separate save
        // here it can batch the RaidItem insert ahead of its parent row and hit the FK
        // constraint the database (correctly) still enforces.
        await db.SaveChangesAsync(ct);

        db.RaidItems.AddRange(
            new RaidItem
            {
                Id = Guid.NewGuid(),
                PortfolioId = platformPortfolio.Id,
                Title = "Platform-wide skills gap in the new stack",
                Description = "Delivery teams are still ramping up on the new platform tooling.",
                Category = "Risk",
                Severity = "High",
                Status = "Open"
            },
            new RaidItem
            {
                Id = Guid.NewGuid(),
                ProgramId = coreProgram.Id,
                Title = "Shared sandbox environment contention",
                Description = "Onboarding and self-service teams are blocking each other in the shared sandbox.",
                Category = "Issue",
                Severity = "Medium",
                Status = "Mitigating"
            },
            new RaidItem
            {
                Id = Guid.NewGuid(),
                ProjectId = onboardingProject.Id,
                Title = "Legacy data migration may slip the go-live date",
                Description = "The legacy tenant export format is undocumented; discovery is taking longer than planned.",
                Category = "Risk",
                Severity = "High",
                Status = "Open",
                DueDate = new DateOnly(2026, 2, 15)
            },
            new RaidItem
            {
                Id = Guid.NewGuid(),
                ProjectId = onboardingProject.Id,
                Title = "Confirm Entra tenant provisioning timeline",
                Description = "Need a committed date from Identity so onboarding testing can be scheduled.",
                Category = "Action",
                Severity = "Medium",
                Status = "Open",
                DueDate = new DateOnly(2026, 1, 30)
            },
            new RaidItem
            {
                Id = Guid.NewGuid(),
                ProjectId = onboardingProject.Id,
                Title = "Sandbox environment intermittent outages",
                Description = "Two outages in the last week have cost the API team two lost days.",
                Category = "Issue",
                Severity = "Critical",
                Status = "Open"
            },
            new RaidItem
            {
                Id = Guid.NewGuid(),
                ProjectId = onboardingProject.Id,
                Title = "Adopt phased rollout by region",
                Description = "Go live in GCC first, then expand — reduces blast radius of the data migration risk above.",
                Category = "Decision",
                Severity = "Low",
                Status = "Closed"
            });

        await db.SaveChangesAsync(ct);

        // Every task/dependency write recomputes the whole project's CPM in the real
        // endpoints (PmoEndpoints); the seed does the same so ScheduleStart/Finish/
        // TotalFloatDays/IsCritical are populated exactly the way a real save would leave
        // them, instead of sitting null until someone edits the project through the API.
        await ScheduleQuery.RecomputeAsync(db, onboardingProject.Id, ct);
        await ScheduleQuery.RecomputeAsync(db, selfServiceProject.Id, ct);
        await ScheduleQuery.RecomputeAsync(db, reportingProject.Id, ct);
        await ScheduleQuery.RecomputeAsync(db, omnichannelProject.Id, ct);
    }

    private static TaskItem NewTask(
        Guid projectId, string name, string wbs, int durationDays, bool isMilestone = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = name,
            WbsCode = wbs,
            DurationDays = durationDays,
            IsMilestone = isMilestone,
            PercentComplete = 0m
        };

    private static Dependency NewDependency(Guid projectId, Guid predecessorId, Guid successorId) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            PredecessorTaskId = predecessorId,
            SuccessorTaskId = successorId,
            Type = "FS",
            LagDays = 0
        };
}
