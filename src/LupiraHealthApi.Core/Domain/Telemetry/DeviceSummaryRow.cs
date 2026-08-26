namespace LupiraHealthApi.Core.Domain.Telemetry;

/// <summary>A single validated device-computed summary (sleep session, daily totals…) from an ingest batch.</summary>
public sealed record DeviceSummaryRow(long Seq, short Kind, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd, string PayloadJson);
