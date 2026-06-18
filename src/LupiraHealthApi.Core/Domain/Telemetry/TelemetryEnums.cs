namespace LupiraHealthApi.Domain.Telemetry;

/// <summary>Source that produced a GPS fix. Stored as the <c>provider</c> smallint on <c>telemetry.location_point</c>.</summary>
public enum LocationProvider : short { Unknown = 0, Gps = 1, Network = 2, Fused = 3, Passive = 4 }

/// <summary>OS-reported motion classification. Stored as the <c>activity</c> smallint; the primary trip/visit segmentation signal.</summary>
public enum MotionActivity : short { Unknown = 0, Still = 1, Walk = 2, Run = 3, Cycle = 4, Vehicle = 5 }

/// <summary>Ring point-sample metric, discriminated by the <c>kind</c> smallint on <c>telemetry.ring_sample</c>.</summary>
public enum RingMetric : short { Unknown = 0, HeartRate = 1, Hrv = 2, Spo2 = 3, SkinTemp = 4, Steps = 5, Activity = 6 }
