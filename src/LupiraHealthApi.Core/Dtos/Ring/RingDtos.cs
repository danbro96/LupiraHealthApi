using LupiraHealthApi.Dtos.Location;

namespace LupiraHealthApi.Dtos.Ring;

/// <summary>The outcome of a ring-sample (or device-summary) ingest batch. Idempotent re-uploads show as duplicates.</summary>
public record RingIngestReceipt(int Submitted, int Inserted, int Duplicates, int Rejected, long? HighWaterSeq, IReadOnlyList<IngestReject> Rejects);

/// <summary>One downsampled bucket of a ring metric over a time range.</summary>
public record RingBucketDto(DateTimeOffset BucketTs, double Avg, double Min, double Max, long Count);

/// <summary>A device-computed summary (sleep session, daily totals…). <see cref="Payload"/> is the raw JSON the device sent.</summary>
public record DeviceSummaryDto(Guid DeviceId, int Kind, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd, string Payload);
