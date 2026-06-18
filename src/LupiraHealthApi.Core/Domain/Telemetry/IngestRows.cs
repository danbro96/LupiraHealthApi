namespace LupiraHealthApi.Domain.Telemetry;

/// <summary>A single validated GPS fix from an ingest batch (in-memory; principal/device are stamped server-side, not
/// carried here).</summary>
public sealed record LocationFix(
    long Seq,
    DateTimeOffset Ts,
    double Lat,
    double Lon,
    double? AccuracyM,
    double? AltitudeM,
    double? VerticalAccM,
    double? HeadingDeg,
    double? HeadingAccDeg,
    double? SpeedMps,
    double? SpeedAccMps,
    LocationProvider Provider,
    MotionActivity Activity,
    short? ActivityConf,
    short? BatteryPct,
    bool? IsMoving,
    bool IsMock);

/// <summary>A single validated ring point-sample from an ingest batch.</summary>
public sealed record RingSampleRow(long Seq, RingMetric Kind, DateTimeOffset Ts, decimal Value);

/// <summary>A single validated device-computed summary (sleep session, daily totals…) from an ingest batch.</summary>
public sealed record DeviceSummaryRow(long Seq, short Kind, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd, string PayloadJson);
