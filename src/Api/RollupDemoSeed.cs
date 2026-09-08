using Microsoft.EntityFrameworkCore;
using SmoPmo.Pmo;
using SmoPmo.Rollup;
using SmoPmo.Smo;

namespace SmoPmo.Api;

/// <summary>
/// Dev-only demo data for X1: links a couple of the SMO/PMO demo seeds' initiatives to
/// their delivery vehicles, so the aggregate read (GET /api/rollup/initiatives/{id}/delivery)
/// has something real to return instead of an empty tenant.
///
/// This lives in /Api rather than as a RollupDemoSeed inside the Rollup module because
/// matching seed rows by name needs <see cref="SmoDbContext"/> and <see cref="PmoDbContext"/>
/// in scope at once, and a module may not project-reference another module
/// (architecture-mvp.md §9) — only the composition root may hold all three.
///
/// Only ever called from Program.cs behind an <c>IsDevelopment()</c> check — never runs
/// against a real tenant. Idempotent: any link already existing for the current tenant
/// means a prior run already seeded it.
/// </summary>
public static class RollupDemoSeed
{
    public static async Task SeedAsync(
        RollupDbContext rollupDb,
        SmoDbContext smoDb,
        PmoDbContext pmoDb,
        CancellationToken ct = default)
    {
        if (await rollupDb.Links.AnyAsync(ct))
        {
            return;
        }

        var platformRollout = await smoDb.Initiatives.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Name == "Unified Platform Rollout", ct);
        var customerSuccess = await smoDb.Initiatives.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Name == "Customer Success Revamp", ct);

        var coreProgram = await pmoDb.Programs.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Name == "Core Platform Program", ct);
        var onboardingProject = await pmoDb.Projects.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Name == "Tenant Onboarding Revamp", ct);
        var omnichannelProject = await pmoDb.Projects.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Name == "Omnichannel Support Launch", ct);

        // Mirrors the PRD's own example (§5.3): one initiative fulfilled by a primary
        // program plus a secondary project, proportionally weighted.
        if (platformRollout is not null && coreProgram is not null)
        {
            rollupDb.Links.Add(new InitiativeDeliveryLink
            {
                Id = Guid.NewGuid(),
                InitiativeId = platformRollout.Id,
                ProgramId = coreProgram.Id,
                IsPrimary = true,
                ContributionWeight = 70m
            });
        }

        if (platformRollout is not null && onboardingProject is not null)
        {
            rollupDb.Links.Add(new InitiativeDeliveryLink
            {
                Id = Guid.NewGuid(),
                InitiativeId = platformRollout.Id,
                ProjectId = onboardingProject.Id,
                IsPrimary = false,
                ContributionWeight = 30m
            });
        }

        if (customerSuccess is not null && omnichannelProject is not null)
        {
            rollupDb.Links.Add(new InitiativeDeliveryLink
            {
                Id = Guid.NewGuid(),
                InitiativeId = customerSuccess.Id,
                ProjectId = omnichannelProject.Id,
                IsPrimary = true,
                ContributionWeight = 100m
            });
        }

        await rollupDb.SaveChangesAsync(ct);
    }
}
