using Lupira.Testing.Postgres;
using LupiraHealthApi.Core.Telemetry;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LupiraHealthApi.IntegrationTests;

/// <summary>
/// Hosts the real app against an ephemeral Postgres (Testcontainers). Runs in <c>Development</c> so the dev auth handler
/// is wired (<c>X-Dev-User</c> for <c>/api</c>); telemetry ingest uses a real per-device key minted via the API. Both the
/// Marten <c>health</c> schema and the raw <c>telemetry</c> schema are applied once; data is reset per test. The
/// background maintenance service is disabled so it never races the reset.
/// </summary>
public sealed class HealthApiTestFactory : LupiraApiFactory<Program>
{
    public IDocumentStore Store => Services.GetRequiredService<IDocumentStore>();

    public NpgsqlDataSource DataSource => Services.GetRequiredService<NpgsqlDataSource>();

    protected override string AuthentikSlug => "lupira-health";

    protected override void AddSettings(IDictionary<string, string?> settings) =>
        settings["Telemetry:MaintenanceEnabled"] = "false";

    protected override async Task ApplySchemaAsync()
    {
        await Store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
        await TelemetrySchema.ApplyAsync(DataSource);
    }

    protected override async Task ResetDataAsync()
    {
        await Store.Advanced.ResetAllData();
        await TelemetrySchema.TruncateAllAsync(DataSource);
    }
}
