using System.Diagnostics;

namespace LupiraHealthApi.Domain;

/// <summary>Domain-specific tracing source, registered with OpenTelemetry in Program.cs. (Named to avoid colliding with
/// the <c>LupiraHealthApi.Domain.Telemetry</c> namespace that holds the time-series domain types.)</summary>
public static class HealthTelemetry
{
    public const string ActivitySourceName = "LupiraHealthApi.Health";
    public static readonly ActivitySource Source = new(ActivitySourceName);
}
