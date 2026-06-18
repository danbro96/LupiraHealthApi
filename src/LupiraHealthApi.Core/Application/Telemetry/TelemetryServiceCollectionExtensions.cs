using LupiraHealthApi.Application.Telemetry;
using LupiraHealthApi.Telemetry;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the telemetry subsystem (raw-Npgsql ingest/query + derived intelligence) into DI.</summary>
public static class TelemetryServiceCollectionExtensions
{
    public static IServiceCollection AddHealthTelemetry(this IServiceCollection services)
    {
        services.AddSingleton<PartitionManager>();
        services.AddScoped<TrackingStateService>();
        services.AddScoped<PlaceLabelService>();
        services.AddScoped<LocationIngestService>();
        services.AddScoped<LocationQueryService>();
        services.AddScoped<TripVisitService>();
        services.AddScoped<RingIngestService>();
        services.AddScoped<RingQueryService>();
        return services;
    }
}
