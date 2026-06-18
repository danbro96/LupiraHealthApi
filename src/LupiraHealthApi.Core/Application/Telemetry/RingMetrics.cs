using LupiraHealthApi.Domain.Telemetry;

namespace LupiraHealthApi.Application.Telemetry;

/// <summary>Maps wire metric names (and aliases the mobile app sends) to <see cref="RingMetric"/>.</summary>
public static class RingMetrics
{
    public static bool TryParse(string? raw, out RingMetric metric)
    {
        metric = RingMetric.Unknown;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        switch (raw.Trim().ToLowerInvariant().Replace("_", ""))
        {
            case "hr": case "heartrate": metric = RingMetric.HeartRate; return true;
            case "hrv": metric = RingMetric.Hrv; return true;
            case "spo2": metric = RingMetric.Spo2; return true;
            case "skintemp": metric = RingMetric.SkinTemp; return true;
            case "steps": metric = RingMetric.Steps; return true;
            case "activity": metric = RingMetric.Activity; return true;
            default:
                return Enum.TryParse(raw, true, out metric) && Enum.IsDefined(typeof(RingMetric), metric);
        }
    }
}
