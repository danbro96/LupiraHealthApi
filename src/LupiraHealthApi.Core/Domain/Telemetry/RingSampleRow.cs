namespace LupiraHealthApi.Core.Domain.Telemetry;

/// <summary>A single validated ring point-sample from an ingest batch.</summary>
public sealed record RingSampleRow(long Seq, RingMetric Kind, DateTimeOffset Ts, decimal Value);
