using LupiraHealthApi.Auth;
using LupiraHealthApi.Background;
using LupiraHealthApi.Domain;
using LupiraHealthApi.Endpoints;
using LupiraHealthApi.Handlers;
using LupiraHealthApi.Health;
using LupiraHealthApi.Telemetry;
using System.Text.Json.Serialization;
using Marten;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- Bounded context (Marten document store on the `health` schema + the raw-Npgsql `telemetry` path for ring
// telemetry + the transport-neutral services). Connection string is read lazily inside AddHealthCore. ---
builder.Services.AddHealthCore();

// --- Host-only services: identity (claims -> Core PrincipalDirectory) + the thin REST/ingest handlers. ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<MeHandler>();
builder.Services.AddScoped<HealthRecordsHandler>();
builder.Services.AddScoped<DevicesHandler>();
builder.Services.AddScoped<RingIngestHandler>();
builder.Services.AddScoped<RingQueryHandler>();

// --- Read-only MCP surface (HealthTools) over the same Core services; LAN/WireGuard-only (see UseMcpLanOnly). ---
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// Background maintenance: pre-provision upcoming ring partitions (gated by config).
builder.Services.AddHostedService<RingMaintenanceService>();

// --- Auth: OIDC JWT for the REST surface (human reads/writes); per-device API key for /ingest (the mobile uploader).
//           One identity authority (Authentik); the OIDC `sub` is the only cross-service join key. ---
var authBuilder = builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.Audience = builder.Configuration["Auth:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.Events = new JwtBearerEvents
        {
            // MCP auth spec: a 401 on /mcp advertises the RFC 9728 metadata so clients can discover the issuer.
            OnChallenge = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/mcp"))
                    ctx.Response.Headers.Append("WWW-Authenticate",
                        $"Bearer resource_metadata=\"{McpResourceMetadata.ResourceMetadataUrl(ctx.Request)}\"");
                return Task.CompletedTask;
            },
        };
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

// Emit enum names (not ints) on the wire across the API surface.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, context, _) =>
    {
        document.Info = new()
        {
            Title = "Lupira Health API",
            Version = "v1",
            Description =
                "Health, wearables, and ring-metrics backend for Lupira. " +
                "Authenticate with a Bearer token issued by the OIDC provider (Authentik).",
        };
        document.Components ??= new();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "OIDC bearer token. Send as `Authorization: Bearer <token>`.",
        };
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, _) =>
    {
        var endpointMetadata = context.Description.ActionDescriptor.EndpointMetadata;
        var requiresAuth = endpointMetadata.OfType<IAuthorizeData>().Any()
                        && !endpointMetadata.OfType<IAllowAnonymous>().Any();
        if (requiresAuth)
        {
            operation.Security ??= new List<OpenApiSecurityRequirement>();
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = new List<string>(),
            });
        }
        return Task.CompletedTask;
    });
});

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

// Backstop before auth: a /mcp request carrying Cloudflare edge headers came through the tunnel — 404 it.
app.UseMcpLanOnly();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
app.MapScalarApiReference("/scalar", o => o
        .WithTitle("Lupira Health API")
        .WithTheme(ScalarTheme.BluePlanet))
    .AllowAnonymous();

app.MapGet("/", () => TypedResults.Redirect("/scalar"))
   .ExcludeFromDescription()
   .AllowAnonymous();

// Health probes: /livez = liveness (no dependency checks); /readyz = readiness (Postgres reachable).
app.MapHealthChecks("/livez", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/readyz", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") })
    .DisableHttpMetrics();

// REST surface.
app.MapMe();
app.MapHealthRecords();
app.MapDevices();
app.MapIngest();
app.MapRingQuery();

// Agent surface: OIDC-gated (ApiPolicy excludes the DeviceKey scheme; in Dev X-Dev-User works too).
// RFC 9728 metadata lets MCP clients discover the Authentik issuer from the 401 challenge.
app.MapMcpResourceMetadata(app.Configuration["Auth:Authority"]);
app.MapMcp("/mcp").RequireAuthorization("ApiPolicy");

app.Run();

// Exposes the implicit Program entry point to the integration test assembly (WebApplicationFactory<Program>).
public partial class Program;
