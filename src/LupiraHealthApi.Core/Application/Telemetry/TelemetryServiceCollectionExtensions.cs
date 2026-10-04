using Lupira.Postgres.Partitions;
using LupiraHealthApi.Core.Application.Telemetry;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the ring telemetry subsystem (raw-Npgsql ingest/query) into DI.</summary>
public static class TelemetryServiceCollectionExtensions
{
    public static IServiceCollection AddHealthTelemetry(this IServiceCollection services)
    {
        services.AddSingleton(new PartitionManager("telemetry"));
        services.AddScoped<RingIngestService>();
        services.AddScoped<RingQueryService>();
        return services;
    }
}
