namespace LupiraHealthApi.Core.Dtos.Ring;

/// <summary>A per-row rejection within an otherwise-accepted ingest batch (permanent — the uploader should drop + log it).</summary>
public record IngestReject(long? Seq, string Reason);
