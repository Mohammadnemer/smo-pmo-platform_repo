using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmoPmo.Platform;
using SmoPmo.Shared.Auditing;
using SmoPmo.Shared.Messaging;
using SmoPmo.Shared.Multitenancy;
using SmoPmo.Smo;
using Xunit;

namespace Platform.Tests;

public sealed class MessageBusTests
{
    /// <summary>
    /// B4's acceptance line, still true after B5 moved strategy ownership to the SMO module:
    /// a command runs through a handler and the change lands in the audit log. The route is
    /// now cross-module — SMO's interceptor reports the change and Platform, which owns the
    /// audit table, writes the row.
    /// </summary>
    [Fact]
    public async Task CommandHandlerPersistsAnEntityAndTheChangeLandsInTheAuditLog()
    {
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();

        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IMessageBus, InProcessMessageBus>();
        services.AddSmoModule(options => options.UseInMemoryDatabase(databaseName));
        services.AddDbContext<PlatformDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddScoped<IDomainEventHandler<EntitiesChangedEvent>, EntitiesChangedEventHandler>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = Guid.NewGuid();

        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var strategyId = await messageBus.SendAsync(new CreateStrategyCommand("North Star", "Exec strategy"));

        var smoDbContext = scope.ServiceProvider.GetRequiredService<SmoDbContext>();
        var strategy = await smoDbContext.Strategies.SingleAsync();

        Assert.Equal(strategyId, strategy.Id);
        Assert.Equal("North Star", strategy.Name);
        Assert.Equal(tenantContext.TenantId, strategy.TenantId);

        var platformDbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var auditEntry = await platformDbContext.AuditEntries.SingleAsync();

        Assert.Equal("SMO", auditEntry.Module);
        Assert.Equal("Strategy", auditEntry.EntityName);
        Assert.Equal("Create", auditEntry.Action);
        Assert.Equal(strategy.Id, auditEntry.EntityId);
        Assert.Equal(tenantContext.TenantId, auditEntry.TenantId);
    }
}
