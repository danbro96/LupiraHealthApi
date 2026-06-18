namespace LupiraHealthApi.Dtos.Location;

/// <summary>Latest-known location for a device.</summary>
public record CurrentFixDto(Guid DeviceId, DateTimeOffset Ts, double Lat, double Lon, double? AccuracyM, double? SpeedMps, string? Activity, int? BatteryPct);

/// <summary>A point on a (raw or thinned) track.</summary>
public record TrackPointDto(Guid DeviceId, DateTimeOffset Ts, double Lat, double Lon, double? AccuracyM, double? AltitudeM, double? HeadingDeg, double? SpeedMps, string? Activity, string? Provider);

/// <summary>Distance + speed stats over a time range.</summary>
public record TrackStatsDto(double DistanceM, double? AvgSpeedMps, double? MaxSpeedMps, long SampleCount);

/// <summary>A coarse "where was I at T" answer — a place label + coarsened coordinate, never the raw fix. Synergy-safe.</summary>
public record PlaceLabelAtDto(DateTimeOffset Ts, string? Label, double Lat, double Lon, string Source);

/// <summary>A materialized stay-point.</summary>
public record LocationVisitDto(Guid Id, DateTimeOffset ArriveTs, DateTimeOffset DepartTs, double Lat, double Lon, double RadiusM, int SampleCount, string? PlaceLabel);

/// <summary>A materialized trip between stays.</summary>
public record LocationTripDto(Guid Id, DateTimeOffset StartTs, DateTimeOffset EndTs, double DistanceM, double DurationS, string DominantActivity, double AvgSpeedMps, double MaxSpeedMps);

/// <summary>A place visited on a day, with dwell minutes.</summary>
public record VisitedPlaceDto(string? Label, double Lat, double Lon, double Minutes);

/// <summary>Per-day location rollup.</summary>
public record DailyLocationSummaryDto(DateOnly Date, double DistanceM, double TimeInMotionS, double TimeStationaryS, int VisitCount, IReadOnlyList<VisitedPlaceDto> Places);

/// <summary>Per-device tracking kill-switch state.</summary>
public record TrackingStateDto(Guid DeviceId, bool Paused, DateTimeOffset? PausedAt, string? Reason);

/// <summary>Pause tracking for a device (optional human-readable reason).</summary>
public record PauseTrackingRequest(string? Reason);
