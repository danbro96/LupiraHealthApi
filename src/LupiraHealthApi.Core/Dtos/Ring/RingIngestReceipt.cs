namespace LupiraHealthApi.Core.Dtos.Ring;

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
