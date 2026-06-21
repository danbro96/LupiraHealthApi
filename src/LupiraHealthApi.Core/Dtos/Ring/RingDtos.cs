namespace LupiraHealthApi.Dtos.Ring;

/// <summary>A per-row rejection within an otherwise-accepted ingest batch (permanent — the uploader should drop + log it).</summary>
public record IngestReject(long? Seq, string Reason);

/// <summary>The outcome of a ring-sample (or device-summary) ingest batch. Idempotent re-uploads show as duplicates.</summary>
public sealed class RingIngestReceipt
{
    public required int Submitted { get; set; }
    public required int Inserted { get; set; }
    public required int Duplicates { get; set; }
    public required int Rejected { get; set; }
    public long? HighWaterSeq { get; set; }
    public required IReadOnlyList<IngestReject> Rejects { get; set; }
}

/// <summary>One downsampled bucket of a ring metric over a time range.</summary>
public sealed class RingBucketDto
{
    public required DateTimeOffset BucketTs { get; set; }
    public required double Avg { get; set; }
    public required double Min { get; set; }
    public required double Max { get; set; }
    public required long Count { get; set; }
}

/// <summary>A device-computed summary (sleep session, daily totals…). <see cref="Payload"/> is the raw JSON the device sent.</summary>
public sealed class DeviceSummaryDto
{
    public required Guid DeviceId { get; set; }
    public required int Kind { get; set; }
    public required DateTimeOffset PeriodStart { get; set; }
    public required DateTimeOffset PeriodEnd { get; set; }
    public required string Payload { get; set; }
}
