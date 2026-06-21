namespace LupiraHealthApi.Domain.Telemetry;

/// <summary>Ring point-sample metric, discriminated by the <c>kind</c> smallint on <c>telemetry.ring_sample</c>.</summary>
public enum RingMetric : short { Unknown = 0, HeartRate = 1, Hrv = 2, Spo2 = 3, SkinTemp = 4, Steps = 5, Activity = 6 }
