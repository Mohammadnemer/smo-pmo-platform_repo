using Microsoft.Extensions.DependencyInjection;

namespace SmoPmo.Workflow;

/// <summary>
/// Workflow module composition root. Owns: state-machine engine (fixed flow in MVP, configurable later).
///
/// Wired only from /Api (architecture-mvp.md §9) — each module exposes exactly one
/// registration entry point and /Api calls them in order. Intentionally empty in T1:
/// this is the skeleton seam. Filled in: B7 (fixed approval flow), B9 (configurable engine).
/// </summary>
public static class WorkflowModule
{
    public static IServiceCollection AddWorkflowModule(this IServiceCollection services)
    {
        // No registrations yet (T1 skeleton).
        return services;
    }
}
