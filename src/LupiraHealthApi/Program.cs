using Lupira.Auth.DeviceKeys.AspNetCore;
using Lupira.Auth.Jwt;
using Lupira.Hosting.Defaults;
using Lupira.Hosting.Health;
using Lupira.Hosting.LanEdge;
using Lupira.Hosting.Observability;
using Lupira.Hosting.OpenApi;
using Lupira.Hosting.Problems;
using Lupira.Identity.Marten.AspNetCore;
using Lupira.Mcp;
using Lupira.Postgres.Health;
using LupiraHealthApi.Auth;
using LupiraHealthApi.Core.Telemetry;
using LupiraHealthApi.Endpoints;
using LupiraHealthApi.Handlers;
using LupiraHealthApi.Mcp;
using LupiraHealthApi.Workers;
using Marten;
using Microsoft.AspNetCore.HttpOverrides;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// --- Bounded context (Marten document store on the `health` schema + the raw-Npgsql `telemetry` path for ring
// telemetry + the transport-neutral services). Connection string is read lazily inside AddHealthCore. ---
builder.Services.AddHealthCore();

// --- Host-only services: identity (claims -> PrincipalDirectory) + the thin REST/ingest handlers. ---
builder.Services.AddLupiraCurrentUser();
builder.Services.AddScoped<MeHandler>();
builder.Services.AddScoped<HealthRecordsHandler>();
builder.Services.AddScoped<DevicesHandler>();
builder.Services.AddScoped<RingIngestHandler>();
builder.Services.AddScoped<RingQueryHandler>();

// --- Read-only MCP surface (HealthTools) over the same Core services; LAN/WireGuard-only (see UseLanOnlySurfaces). ---
builder.Services.AddLupiraMcp().WithTools<HealthTools>();

// Background maintenance: pre-provision upcoming ring partitions (gated by config).
builder.Services.AddHostedService<RingMaintenanceService>();

// --- Auth: OIDC JWT for the REST surface (human reads/writes); per-device API key for /ingest (the mobile uploader).
//           One identity authority (Authentik); the OIDC `sub` is the only cross-service join key. ---
builder.AddLupiraJwt().AddLupiraDeviceKeys<MartenDeviceKeyStore>();
var apiSchemes = LupiraJwtSchemes.Api(builder.Environment);

builder.Services.AddAuthorizationBuilder()
    .AddLupiraApiPolicy(apiSchemes)
    .AddLupiraApiPolicy([DeviceKeyAuthHandler.SchemeName], "IngestPolicy");

builder.AddLupiraTelemetry("lupira-health-api");

builder.Services.AddLupiraHealth().AddReadyCheck<DatabaseReadyCheck>("postgres");

builder.AddLupiraDefaults(o =>
{
    o.CaseInsensitiveProperties = true;
    o.ForwardedHeaders = ForwardedHeaders.None;
});

builder.Services.AddLupiraProblems();

builder.Services.AddOpenApi("v1", options => options.AddLupiraConventions(o =>
{
    o.Title = "Lupira Health API";
    o.Description =
        "Health, wearables, and ring-metrics backend for Lupira. " +
        "Authenticate with a Bearer token issued by the OIDC provider (Authentik).";
}));

var app = builder.Build();

// One-shot schema apply (deploy step: `dotnet LupiraHealthApi.dll --apply-schema`). Applies the Marten `health`
// schema AND the raw `telemetry` schema (ring tables + initial partitions), which Marten's diff never touches.
if (args.Contains("--apply-schema"))
{
    var store = app.Services.GetRequiredService<IDocumentStore>();
    await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
    await TelemetrySchema.ApplyAsync(app.Services.GetRequiredService<NpgsqlDataSource>());
    Console.WriteLine("Schema applied.");
    return;
}

// LAN-only surfaces (/mcp + its discovery metadata): 404 anything arriving through the tunnel,
// before auth so a tunnelled probe never even receives a challenge.
app.UseLanOnlySurfaces("/mcp", "/.well-known/oauth-protected-resource");

app.UseLupiraDefaults();
app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

app.MapLupiraOpenApi(o => o.Title = "Lupira Health API");

// Health probes: /livez = liveness (no dependency checks); /readyz = readiness (Postgres reachable).
app.MapLupiraHealth();

// REST surface.
app.MapMe();
app.MapHealthRecords();
app.MapDevices();
app.MapIngest();
app.MapRingQuery();

// Agent surface: OIDC-gated (ApiPolicy excludes the DeviceKey scheme; in Dev X-Dev-User works too).
// RFC 9728 metadata lets MCP clients discover the Authentik issuer from the 401 challenge.
app.MapMcpResourceMetadata(app.Configuration["Auth:Oidc:Authority"]);
app.MapLupiraMcp();

app.Run();

// Exposes the implicit Program entry point to the integration test assembly (WebApplicationFactory<Program>).
public partial class Program;
