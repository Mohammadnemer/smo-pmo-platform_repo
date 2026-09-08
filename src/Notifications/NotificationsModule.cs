using Microsoft.Extensions.DependencyInjection;

namespace SmoPmo.Notifications;

/// <summary>
/// Notifications module composition root. Owns: in-app + email notifications; digests later.
///
/// Wired only from /Api (architecture-mvp.md §9) — each module exposes exactly one
/// registration entry point and /Api calls them in order. Intentionally empty in T1:
/// this is the skeleton seam. Filled in: B8 (notifications).
/// </summary>
public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services)
    {
        // No registrations yet (T1 skeleton).
        return services;
    }
}
