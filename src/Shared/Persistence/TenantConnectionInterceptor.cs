using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmoPmo.Shared.Multitenancy;

namespace SmoPmo.Shared.Persistence;

/// <summary>
/// Issues <c>SET app.tenant_id = …</c> on every connection a module opens, which is
/// what makes the PostgreSQL RLS policies bite (architecture-mvp.md §4).
///
/// This lives in /Shared because it is a non-negotiable every module needs and must
/// not re-implement: without it the RLS predicate falls back to the all-zero guid and
/// the database returns nothing.
/// </summary>
public sealed class TenantConnectionInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _tenantContext;

    public TenantConnectionInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ApplyTenant(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await ApplyTenantAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private void ApplyTenant(DbConnection connection)
    {
        using var command = CreateCommand(connection);
        command.ExecuteNonQuery();
    }

    private async Task ApplyTenantAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CreateCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();

        // set_config is used rather than SET so the value can be parameterised —
        // string-formatting a tenant id into DDL-ish SQL is exactly the kind of
        // shortcut that turns a tenancy bug into an injection bug.
        command.CommandText = "SELECT set_config('app.tenant_id', @tenant_id, false)";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@tenant_id";
        parameter.Value = _tenantContext.TenantId.ToString();
        command.Parameters.Add(parameter);

        return command;
    }
}
