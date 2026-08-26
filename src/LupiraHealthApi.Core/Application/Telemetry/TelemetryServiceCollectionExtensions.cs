using LupiraHealthApi.Core.Application.Telemetry;
using LupiraHealthApi.Core.Telemetry;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the ring telemetry subsystem (raw-Npgsql ingest/query) into DI.</summary>
public static class TelemetryServiceCollectionExtensions
{
    public static IServiceCollection AddHealthTelemetry(this IServiceCollection services)
    {
        services.AddSingleton<PartitionManager>();
        services.AddScoped<RingIngestService>();
        services.AddScoped<RingQueryService>();
        return services;
    }
}
