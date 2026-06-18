using LupiraHealthApi.Auth;
using LupiraHealthApi.Background;
using LupiraHealthApi.Domain;
using LupiraHealthApi.Endpoints;
using LupiraHealthApi.Handlers;
using LupiraHealthApi.Health;
using LupiraHealthApi.Telemetry;
using Marten;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// --- Bounded context (Marten document store on the `health` schema + the raw-Npgsql `telemetry` path + the
// transport-neutral services). Connection string is read lazily from ConnectionStrings:Postgres inside AddHealthCore. ---
builder.Services.AddHealthCore();

// --- Host-only services: identity (claims -> Core PrincipalDirectory) + the thin REST/ingest handlers. ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<MeHandler>();
builder.Services.AddScoped<HealthRecordsHandler>();
builder.Services.AddScoped<DevicesHandler>();
builder.Services.AddScoped<LocationIngestHandler>();
builder.Services.AddScoped<LocationQueryHandler>();
builder.Services.AddScoped<RingIngestHandler>();
builder.Services.AddScoped<RingQueryHandler>();

// Background maintenance: partition provisioning + nightly rollup + retention drop (gated by config).
builder.Services.AddHostedService<LocationMaintenanceService>();

// --- Auth: OIDC JWT for /api (human reads/writes); per-device API key for /api/ingest (the mobile uploader).
//           One identity authority (Authentik); the OIDC `sub` is the only cross-service join key. ---
var authBuilder = builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.Audience = builder.Configuration["Auth:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    })
    .AddScheme<AuthenticationSchemeOptions, DeviceKeyAuthHandler>(DeviceKeyAuthHandler.SchemeName, _ => { });

// Development-only: allow X-Dev-User header auth so the API can be exercised without Authentik.
if (builder.Environment.IsDevelopment())
    authBuilder.AddScheme<AuthenticationSchemeOptions, DevAuthHandler>(DevAuthHandler.SchemeName, _ => { });

string[] apiSchemes = builder.Environment.IsDevelopment()
    ? [JwtBearerDefaults.AuthenticationScheme, DevAuthHandler.SchemeName]
    : [JwtBearerDefaults.AuthenticationScheme];

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("ApiPolicy", p => p.AddAuthenticationSchemes(apiSchemes).RequireAuthenticatedUser())
    .AddPolicy("IngestPolicy", p => p.AddAuthenticationSchemes(DeviceKeyAuthHandler.SchemeName).RequireAuthenticatedUser());

// --- Observability: OpenTelemetry -> OpenObserve. Env-gated; the OTLP exporter reads OTEL_EXPORTER_OTLP_* itself. ---
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("lupira-health-api"))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation();
        t.AddHttpClientInstrumentation();
        t.AddSource(HealthTelemetry.ActivitySourceName);
        if (!string.IsNullOrWhiteSpace(otlpEndpoint)) t.AddOtlpExporter();
    })
    .WithMetrics(m =>
    {
        m.AddAspNetCoreInstrumentation();
        m.AddHttpClientInstrumentation();
        m.AddRuntimeInstrumentation();
        if (!string.IsNullOrWhiteSpace(otlpEndpoint)) m.AddOtlpExporter();
    });

builder.Logging.AddOpenTelemetry(o =>
{
    o.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("lupira-health-api"));
    o.IncludeScopes = true;
    o.IncludeFormattedMessage = true;
    if (!string.IsNullOrWhiteSpace(otlpEndpoint)) o.AddOtlpExporter();
});

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadyCheck>("postgres", tags: ["ready"]);

builder.Services.AddOpenApi();

var app = builder.Build();

// One-shot schema apply (deploy step: `dotnet LupiraHealthApi.dll --apply-schema`). Applies the Marten `health`
// schema AND the raw `telemetry` schema (tables + initial partitions), which Marten's diff never touches.
if (args.Contains("--apply-schema"))
{
    var store = app.Services.GetRequiredService<IDocumentStore>();
    await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
    await TelemetrySchema.ApplyAsync(app.Services.GetRequiredService<NpgsqlDataSource>());
    return;
}

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();   // /openapi/v1.json

// Health probes: /livez = liveness (no dependency checks); /readyz = readiness (Postgres reachable).
app.MapHealthChecks("/livez", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/readyz", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") })
    .DisableHttpMetrics();

// REST surface.
app.MapMe();
app.MapHealthRecords();
app.MapDevices();
app.MapIngest();
app.MapLocationQuery();
app.MapRingQuery();

app.Run();

// Exposes the implicit Program entry point to the integration test assembly (WebApplicationFactory<Program>).
public partial class Program;
