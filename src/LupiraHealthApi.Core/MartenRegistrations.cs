using LupiraHealthApi.Domain;
using LupiraHealthApi.Domain.Telemetry;
using Marten;
using Weasel.Core;

namespace LupiraHealthApi.Domain;

/// <summary>Configures the Marten store for the Health API in the <c>health</c> schema: plain documents only (phase 1
/// has no event-sourced aggregates). The high-frequency time-series lives in a separate <c>telemetry</c> schema owned
/// by raw Npgsql (<see cref="LupiraHealthApi.Telemetry.TelemetrySchema"/>), which Marten's schema-diff never touches.
/// Enums serialize as strings.</summary>
public static class MartenRegistrations
{
    public static StoreOptions UseLupiraHealth(this StoreOptions opts)
    {
        opts.DatabaseSchemaName = "health";
        opts.UseSystemTextJsonForSerialization(EnumStorage.AsString);

        // Identity + ownership container + devices.
        opts.Schema.For<Principal>().Index(x => x.AuthentikSub).Index(x => x.Email);
        opts.Schema.For<HealthRecord>().Index(x => x.OwnerPrincipalId);
        opts.Schema.For<Device>().Index(x => x.HealthRecordId);
        opts.Schema.For<DeviceApiKey>().Index(x => x.PrincipalId).Index(x => x.DeviceId);

        // Derived location intelligence + caches (materialized by the rollup; survive raw telemetry drop).
        opts.Schema.For<LocationVisit>().Index(x => x.PrincipalId);
        opts.Schema.For<LocationTrip>().Index(x => x.PrincipalId);
        opts.Schema.For<DailyLocationSummary>().Index(x => x.PrincipalId);
        opts.Schema.For<PlaceLabel>();
        opts.Schema.For<TrackingState>().Index(x => x.PrincipalId);
        opts.Schema.For<LocationRollupCheckpoint>();

        return opts;
    }
}
