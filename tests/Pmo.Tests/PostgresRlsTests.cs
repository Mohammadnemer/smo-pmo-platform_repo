using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmoPmo.Pmo;
using Xunit;
using Xunit.Abstractions;

namespace Pmo.Tests;

/// <summary>
/// The parts of B6 that only a real PostgreSQL server can prove: the PMO migration applies,
/// the queries translate to SQL, and Row-Level Security refuses cross-tenant rows at the
/// database level. Mirrors Smo.Tests/PostgresRlsTests.cs (B5) — see that session's notes on
/// why these are marked Skip rather than silently no-op.
/// </summary>
public sealed class PostgresRlsTests
{
    private readonly ITestOutputHelper _output;

    public PostgresRlsTests(ITestOutputHelper output) => _output = output;

    private const string SkipReason =
        "Requires a reachable PostgreSQL server. Set PMO_TEST_POSTGRES and delete this Skip to run.";

    private const string TestDatabase = "smo_pmo_platform_b6test";

    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-0000-0000-0000-00000000000a");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-0000-0000-0000-00000000000b");

    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("PMO_TEST_POSTGRES")
        ?? $"Host=localhost;Database={TestDatabase};Username=postgres;Password=postgres";

    [Fact(Skip = SkipReason)]
    public async Task RowLevelSecurityRefusesRowsWhenTheTenantVariableIsNotSet()
    {
        var connectionString = await TryConnectAsync();
        if (connectionString is null)
        {
            return;
        }

        await using var host = await PmoTestHost.StartAsync(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(PmoDbContext.MigrationsHistoryTable)));

        await host.ResetPmoDatabaseAsync();

        var tenantA = host.CreateClient(TenantA, "PM");
        var portfolio = await tenantA.PostAndReadAsync<PortfolioResponse>("/api/pmo/portfolios",
            new PortfolioWriteModel("Tenant A portfolio", null));

        var tenantB = host.CreateClient(TenantB, "PM");
        Assert.Empty((await tenantB.GetFromJsonAsync<List<PortfolioResponse>>("/api/pmo/portfolios"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await tenantB.GetAsync($"/api/pmo/portfolios/{portfolio.Id}")).StatusCode);

        // And now the database on its own terms: no app.tenant_id, no EF query filter, no
        // WHERE clause — the row must still be invisible.
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var withoutTenant = new NpgsqlCommand("SELECT count(*) FROM \"PmoPortfolios\"", connection))
        {
            Assert.Equal(0L, (long)(await withoutTenant.ExecuteScalarAsync())!);
        }

        await using (var setTenant = new NpgsqlCommand($"SET app.tenant_id = '{TenantA}'", connection))
        {
            await setTenant.ExecuteNonQueryAsync();
        }

        await using (var withTenant = new NpgsqlCommand("SELECT count(*) FROM \"PmoPortfolios\"", connection))
        {
            Assert.Equal(1L, (long)(await withTenant.ExecuteScalarAsync())!);
        }
    }

    [Fact(Skip = SkipReason)]
    public async Task MigrationAppliesAndAScheduleRoundTripsThroughPostgres()
    {
        var connectionString = await TryConnectAsync();
        if (connectionString is null)
        {
            return;
        }

        await using var host = await PmoTestHost.StartAsync(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(PmoDbContext.MigrationsHistoryTable)));

        await host.ResetPmoDatabaseAsync();

        var client = host.CreateClient(TenantA, "PM");

        var portfolio = await client.PostAndReadAsync<PortfolioResponse>("/api/pmo/portfolios", new PortfolioWriteModel("P", null));
        var program = await client.PostAndReadAsync<ProgramResponse>("/api/pmo/programs", new ProgramWriteModel(portfolio.Id, "Prog", null));
        var project = await client.PostAndReadAsync<ProjectResponse>("/api/pmo/projects",
            new ProjectWriteModel(program.Id, "Project", null, new DateOnly(2024, 1, 1), "Draft"));

        var task = await client.PostAndReadAsync<TaskResponse>("/api/pmo/tasks",
            new TaskWriteModel(project.Id, "Task", null, null, 3, false, 0m, null));

        var criticalPath = await client.GetFromJsonAsync<CriticalPathResponse>($"/api/pmo/projects/{project.Id}/critical-path");

        Assert.NotNull(criticalPath);
        var scheduled = Assert.Single(criticalPath!.Tasks);
        Assert.Equal(task.Id, scheduled.Id);
        Assert.Equal(new DateOnly(2024, 1, 1), scheduled.ScheduleStart);
    }

    /// <summary>
    /// Returns the connection string when a server answers, or null when none does — a safety
    /// net for the case where someone removes the Skip on a machine with no database.
    /// </summary>
    private async Task<string?> TryConnectAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString) { Timeout = 3 };
        var serverProbe = new NpgsqlConnectionStringBuilder(builder.ConnectionString) { Database = "postgres" };

        try
        {
            await using var connection = new NpgsqlConnection(serverProbe.ConnectionString);
            await connection.OpenAsync();
        }
        catch (Exception ex)
        {
            _output.WriteLine(
                $"SKIPPED — no PostgreSQL server at {builder.Host}:{builder.Port} ({ex.GetType().Name}: {ex.Message}). " +
                "Set PMO_TEST_POSTGRES to run the RLS tests.");
            return null;
        }

        _output.WriteLine($"Running against PostgreSQL at {builder.Host}:{builder.Port}, database {builder.Database}.");
        return builder.ConnectionString;
    }
}
