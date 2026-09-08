using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmoPmo.Smo;
using Xunit;
using Xunit.Abstractions;

namespace Smo.Tests;

/// <summary>
/// The parts of B5 that only a real PostgreSQL server can prove: the SMO migration applies,
/// the queries translate to SQL (the in-memory provider will happily accept LINQ that Npgsql
/// cannot), and Row-Level Security refuses cross-tenant rows at the database level.
///
/// These are marked Skip deliberately. xunit 2.x has no dynamic skip, and a test that quietly
/// returns when no database answers reports green while proving nothing — worse than an obvious
/// skip. To run them: point SMO_TEST_POSTGRES at a server (or fix the local credentials) and
/// delete the Skip arguments below. See docs/sessions/B5.md → "Not verified".
/// </summary>
public sealed class PostgresRlsTests
{
    private readonly ITestOutputHelper _output;

    public PostgresRlsTests(ITestOutputHelper output) => _output = output;

    private const string SkipReason =
        "Requires a reachable PostgreSQL server. Set SMO_TEST_POSTGRES and delete this Skip to run.";

    private const string TestDatabase = "smo_pmo_platform_b5test";

    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-0000-0000-0000-00000000000a");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-0000-0000-0000-00000000000b");

    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("SMO_TEST_POSTGRES")
        ?? $"Host=localhost;Database={TestDatabase};Username=postgres;Password=postgres";

    [Fact(Skip = SkipReason)]
    public async Task MigrationAppliesAndTheScorecardRoundTripsThroughPostgres()
    {
        var connectionString = await TryConnectAsync();
        if (connectionString is null)
        {
            return;
        }


        await using var host = await SmoTestHost.StartAsync(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(SmoDbContext.MigrationsHistoryTable)));

        await host.ResetSmoDatabaseAsync();

        var client = host.CreateClient(TenantA, "StrategyOwner");

        var strategy = await client.PostAndReadAsync<StrategyResponse>("/api/smo/strategies", new StrategyWriteModel(
            "Vision 2030", "رؤية 2030", null, null, null, null,
            new DateOnly(2026, 1, 1), new DateOnly(2030, 12, 31)));

        var perspective = await client.PostAndReadAsync<PerspectiveResponse>("/api/smo/perspectives",
            new PerspectiveWriteModel(strategy.Id, "Financial", "مالي", null, null, 1, false));

        var objective = await client.PostAndReadAsync<ObjectiveResponse>("/api/smo/objectives",
            new ObjectiveWriteModel(perspective.Id, "Grow revenue", "زيادة الإيرادات", null, null, null, null, null, 1));

        var kpi = await client.PostAndReadAsync<KpiResponse>("/api/smo/kpis", new KpiWriteModel(
            objective.Id, "Revenue", null, null, null, "AED m", KpiDirection.HigherIsBetter,
            KpiFrequency.Monthly, 0m, 100m, null, null, null, "manual", null));

        await client.PostAndReadAsync<KpiMeasurementResponse>($"/api/smo/kpis/{kpi.Id}/measurements",
            new KpiMeasurementWriteModel(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 70m, "January"));
        await client.PostAndReadAsync<KpiMeasurementResponse>($"/api/smo/kpis/{kpi.Id}/measurements",
            new KpiMeasurementWriteModel(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28), 97m, "February"));

        await client.PostAndReadAsync<InitiativeResponse>("/api/smo/initiatives", new InitiativeWriteModel(
            objective.Id, "Pricing reset", null, null, null, "CFO", 1_000_000m, "AED", "+10% revenue",
            new DateOnly(2026, 4, 1), new DateOnly(2026, 12, 31), "Draft"));

        var scorecard = await client.GetFromJsonAsync<ScorecardResponse>($"/api/smo/strategies/{strategy.Id}/scorecard");

        Assert.NotNull(scorecard);
        var scorecardKpi = Assert.Single(Assert.Single(Assert.Single(scorecard.Perspectives).Objectives).Kpis);
        Assert.Equal(97m, scorecardKpi.Kpi.Actual);
        Assert.Equal(RagStatus.Green, scorecardKpi.Kpi.Rag);
        Assert.Equal(new[] { 70m, 97m }, scorecardKpi.Trend.Select(point => point.Value));
        Assert.Equal("رؤية 2030", scorecard.Strategy.NameAr);
    }

    [Fact(Skip = SkipReason)]
    public async Task RowLevelSecurityRefusesRowsWhenTheTenantVariableIsNotSet()
    {
        var connectionString = await TryConnectAsync();
        if (connectionString is null)
        {
            return;
        }


        await using var host = await SmoTestHost.StartAsync(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(SmoDbContext.MigrationsHistoryTable)));

        await host.ResetSmoDatabaseAsync();

        var tenantA = host.CreateClient(TenantA, "StrategyOwner");
        var strategy = await tenantA.PostAndReadAsync<StrategyResponse>("/api/smo/strategies",
            new StrategyWriteModel("Tenant A strategy", null, null, null, null, null, null, null));

        // Another tenant, same table, through the same API.
        var tenantB = host.CreateClient(TenantB, "StrategyOwner");
        Assert.Empty((await tenantB.GetFromJsonAsync<List<StrategyResponse>>("/api/smo/strategies"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await tenantB.GetAsync($"/api/smo/strategies/{strategy.Id}")).StatusCode);

        // And now the database on its own terms: no app.tenant_id, no EF query filter, no
        // WHERE clause — the row must still be invisible.
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var withoutTenant = new NpgsqlCommand("SELECT count(*) FROM \"SmoStrategies\"", connection))
        {
            Assert.Equal(0L, (long)(await withoutTenant.ExecuteScalarAsync())!);
        }

        await using (var setTenant = new NpgsqlCommand($"SET app.tenant_id = '{TenantA}'", connection))
        {
            await setTenant.ExecuteNonQueryAsync();
        }

        await using (var withTenant = new NpgsqlCommand("SELECT count(*) FROM \"SmoStrategies\"", connection))
        {
            Assert.Equal(1L, (long)(await withTenant.ExecuteScalarAsync())!);
        }

        await using (var asTenantB = new NpgsqlCommand($"SET app.tenant_id = '{TenantB}'", connection))
        {
            await asTenantB.ExecuteNonQueryAsync();
        }

        await using (var countForB = new NpgsqlCommand("SELECT count(*) FROM \"SmoStrategies\"", connection))
        {
            Assert.Equal(0L, (long)(await countForB.ExecuteScalarAsync())!);
        }
    }

    /// <summary>
    /// Returns the connection string when a server answers, or null when none does — a safety net
    /// for the case where someone removes the Skip on a machine with no database, so the failure
    /// message says why rather than being a connection-refused stack trace.
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
                "Set SMO_TEST_POSTGRES to run the RLS tests.");
            return null;
        }

        _output.WriteLine($"Running against PostgreSQL at {builder.Host}:{builder.Port}, database {builder.Database}.");
        return builder.ConnectionString;
    }
}
