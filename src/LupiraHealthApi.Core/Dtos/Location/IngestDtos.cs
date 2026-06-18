namespace LupiraHealthApi.Dtos.Location;

/// <summary>A per-row rejection within an otherwise-accepted batch (permanent — the uploader should drop + log it).</summary>
public record IngestReject(long? Seq, string Reason);

/// <summary>The outcome of a location ingest batch. Idempotent re-uploads show up as <see cref="Duplicates"/>; the
/// uploader advances past <see cref="HighWaterSeq"/>. When tracking is paused the body is discarded and
/// <see cref="Paused"/> is true.</summary>
public record LocationIngestReceipt(
    int Submitted,
    int Inserted,
    int Duplicates,
    int Rejected,
    long? HighWaterSeq,
    IReadOnlyList<IngestReject> Rejects,
    bool Paused = false)
{
    public static LocationIngestReceipt PausedReceipt { get; } = new(0, 0, 0, 0, null, [], true);
}

/// <summary>The resume cursor for a device: the highest accepted seq + its timestamp (from the latest-snapshot table).</summary>
public record LocationCursor(Guid DeviceId, long? LastSeq, DateTimeOffset? LastTs);
