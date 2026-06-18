using System.Globalization;
using System.Text.Json;
using LupiraHealthApi.Domain.Telemetry;
using LupiraHealthApi.Dtos.Location;
using LupiraHealthApi.Dtos.Ring;
using LupiraHealthApi.Telemetry;
using Npgsql;
using NpgsqlTypes;

namespace LupiraHealthApi.Application.Telemetry;

/// <summary>Ingests batched ring point-samples and device-computed summaries (NDJSON). Same idempotent merge as
/// location: device-assigned <c>seq</c> in the PK + <c>ON CONFLICT DO NOTHING</c>, monthly partitions pre-created on
/// demand. ids are stamped from the authenticated device key.</summary>
public sealed class RingIngestService(NpgsqlDataSource db, PartitionManager partitions)
{
    private const int MaxRows = 10_000;
    public int RetentionDays { get; init; } = 400;

    public async Task<OpResult<RingIngestReceipt>> IngestSamplesAsync(Guid principalId, Guid deviceId, Stream body, CancellationToken ct = default)
    {
        var (now, maxFuture, minPast) = Window(RetentionDays);
        var accepted = new List<RingSampleRow>();
        var rejects = new List<IngestReject>();
        var submitted = 0;

        using var reader = new StreamReader(body);
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            submitted++;
            if (submitted > MaxRows) { rejects.Add(new IngestReject(null, "batch_too_large")); break; }
            var (row, reason, seq) = ParseSample(line, maxFuture, minPast);
            if (row is not null) accepted.Add(row);
            else rejects.Add(new IngestReject(seq, reason!));
        }

        var inserted = 0;
        if (accepted.Count > 0)
        {
            await using var conn = await db.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);
            foreach (var month in accepted.Select(s => s.Ts).Distinct())
                await partitions.EnsureAsync(conn, tx, "ring_sample", PartitionInterval.Monthly, month, ct);
            inserted = await InsertSamplesAsync(conn, tx, principalId, deviceId, accepted, ct);
            await tx.CommitAsync(ct);
        }

        var highWater = await MaxSeqAsync("ring_sample", principalId, deviceId, ct);
        return OpResult<RingIngestReceipt>.Ok(new RingIngestReceipt(submitted, inserted, accepted.Count - inserted, rejects.Count, highWater, rejects));
    }

    public async Task<OpResult<RingIngestReceipt>> IngestSummariesAsync(Guid principalId, Guid deviceId, Stream body, CancellationToken ct = default)
    {
        var (now, maxFuture, minPast) = Window(RetentionDays);
        var accepted = new List<DeviceSummaryRow>();
        var rejects = new List<IngestReject>();
        var submitted = 0;

        using var reader = new StreamReader(body);
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            submitted++;
            if (submitted > MaxRows) { rejects.Add(new IngestReject(null, "batch_too_large")); break; }
            var (row, reason, seq) = ParseSummary(line, maxFuture, minPast);
            if (row is not null) accepted.Add(row);
            else rejects.Add(new IngestReject(seq, reason!));
        }

        var inserted = 0;
        if (accepted.Count > 0)
        {
            await using var conn = await db.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);
            foreach (var month in accepted.Select(s => s.PeriodStart).Distinct())
                await partitions.EnsureAsync(conn, tx, "device_summary", PartitionInterval.Monthly, month, ct);
            inserted = await InsertSummariesAsync(conn, tx, principalId, deviceId, accepted, ct);
            await tx.CommitAsync(ct);
        }

        var highWater = await MaxSeqAsync("device_summary", principalId, deviceId, ct);
        return OpResult<RingIngestReceipt>.Ok(new RingIngestReceipt(submitted, inserted, accepted.Count - inserted, rejects.Count, highWater, rejects));
    }

    private static async Task<int> InsertSamplesAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid pid, Guid did, List<RingSampleRow> rows, CancellationToken ct)
    {
        var n = rows.Count;
        var kind = new short[n]; var ts = new DateTime[n]; var value = new decimal[n]; var seq = new long[n];
        for (var i = 0; i < n; i++) { kind[i] = (short)rows[i].Kind; ts[i] = rows[i].Ts.UtcDateTime; value[i] = rows[i].Value; seq[i] = rows[i].Seq; }

        const string sql = """
            INSERT INTO telemetry.ring_sample (principal_id, device_id, kind, ts, value, seq)
            SELECT @pid, @did, t.kind, t.ts, t.value, t.seq
            FROM unnest(@kind::smallint[], @ts::timestamptz[], @value::numeric[], @seq::bigint[]) AS t(kind, ts, value, seq)
            ON CONFLICT DO NOTHING
            """;
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("pid", pid);
        cmd.Parameters.AddWithValue("did", did);
        cmd.Parameters.Add(new NpgsqlParameter("kind", NpgsqlDbType.Array | NpgsqlDbType.Smallint) { Value = kind });
        cmd.Parameters.Add(new NpgsqlParameter("ts", NpgsqlDbType.Array | NpgsqlDbType.TimestampTz) { Value = ts });
        cmd.Parameters.Add(new NpgsqlParameter("value", NpgsqlDbType.Array | NpgsqlDbType.Numeric) { Value = value });
        cmd.Parameters.Add(new NpgsqlParameter("seq", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = seq });
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<int> InsertSummariesAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid pid, Guid did, List<DeviceSummaryRow> rows, CancellationToken ct)
    {
        var n = rows.Count;
        var kind = new short[n]; var ps = new DateTime[n]; var pe = new DateTime[n]; var payload = new string[n]; var seq = new long[n];
        for (var i = 0; i < n; i++) { kind[i] = rows[i].Kind; ps[i] = rows[i].PeriodStart.UtcDateTime; pe[i] = rows[i].PeriodEnd.UtcDateTime; payload[i] = rows[i].PayloadJson; seq[i] = rows[i].Seq; }

        const string sql = """
            INSERT INTO telemetry.device_summary (principal_id, device_id, kind, period_start, period_end, payload, seq)
            SELECT @pid, @did, t.kind, t.ps, t.pe, t.payload, t.seq
            FROM unnest(@kind::smallint[], @ps::timestamptz[], @pe::timestamptz[], @payload::jsonb[], @seq::bigint[]) AS t(kind, ps, pe, payload, seq)
            ON CONFLICT DO NOTHING
            """;
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("pid", pid);
        cmd.Parameters.AddWithValue("did", did);
        cmd.Parameters.Add(new NpgsqlParameter("kind", NpgsqlDbType.Array | NpgsqlDbType.Smallint) { Value = kind });
        cmd.Parameters.Add(new NpgsqlParameter("ps", NpgsqlDbType.Array | NpgsqlDbType.TimestampTz) { Value = ps });
        cmd.Parameters.Add(new NpgsqlParameter("pe", NpgsqlDbType.Array | NpgsqlDbType.TimestampTz) { Value = pe });
        cmd.Parameters.Add(new NpgsqlParameter("payload", NpgsqlDbType.Array | NpgsqlDbType.Jsonb) { Value = payload });
        cmd.Parameters.Add(new NpgsqlParameter("seq", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = seq });
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<long?> MaxSeqAsync(string table, Guid pid, Guid did, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand($"SELECT max(seq) FROM telemetry.{table} WHERE principal_id = @pid AND device_id = @did", conn);
        cmd.Parameters.AddWithValue("pid", pid);
        cmd.Parameters.AddWithValue("did", did);
        var v = await cmd.ExecuteScalarAsync(ct);
        return v is long l ? l : null;
    }

    private static (DateTimeOffset Now, DateTimeOffset MaxFuture, DateTimeOffset MinPast) Window(int retentionDays)
    {
        var now = DateTimeOffset.UtcNow;
        return (now, now.AddMinutes(5), now.AddDays(-retentionDays));
    }

    private static (RingSampleRow? Row, string? Reason, long? Seq) ParseSample(string line, DateTimeOffset maxFuture, DateTimeOffset minPast)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(line); } catch { return (null, "invalid_json", null); }
        using (doc)
        {
            var o = doc.RootElement;
            if (o.ValueKind != JsonValueKind.Object) return (null, "invalid_json", null);
            var seq = ReadLong(o, "seq");
            if (seq is null) return (null, "missing_seq", null);
            if (!RingMetrics.TryParse(ReadString(o, "kind"), out var metric)) return (null, "unknown_kind", seq);
            if (!TryReadTs(o, out var ts)) return (null, "invalid_ts", seq);
            if (ts > maxFuture || ts < minPast) return (null, "ts_out_of_range", seq);
            var value = ReadDouble(o, "value");
            if (value is null) return (null, "missing_value", seq);
            return (new RingSampleRow(seq.Value, metric, ts, (decimal)value.Value), null, seq);
        }
    }

    private static (DeviceSummaryRow? Row, string? Reason, long? Seq) ParseSummary(string line, DateTimeOffset maxFuture, DateTimeOffset minPast)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(line); } catch { return (null, "invalid_json", null); }
        using (doc)
        {
            var o = doc.RootElement;
            if (o.ValueKind != JsonValueKind.Object) return (null, "invalid_json", null);
            var seq = ReadLong(o, "seq");
            if (seq is null) return (null, "missing_seq", null);
            var kind = ReadShort(o, "kind");
            if (kind is null) return (null, "missing_kind", seq);
            if (!TryReadTsNamed(o, "periodStart", out var ps)) return (null, "invalid_period_start", seq);
            if (!TryReadTsNamed(o, "periodEnd", out var pe)) return (null, "invalid_period_end", seq);
            if (ps > maxFuture || ps < minPast) return (null, "ts_out_of_range", seq);
            if (!o.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object) return (null, "missing_payload", seq);
            return (new DeviceSummaryRow(seq.Value, kind.Value, ps, pe, payload.GetRawText()), null, seq);
        }
    }

    private static bool TryReadTs(JsonElement o, out DateTimeOffset ts) => TryReadTsNamed(o, "ts", out ts);

    private static bool TryReadTsNamed(JsonElement o, string name, out DateTimeOffset ts)
    {
        ts = default;
        var s = ReadString(o, name);
        return s is not null && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out ts);
    }

    private static double? ReadDouble(JsonElement o, string name) =>
        o.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetDouble(out var d) ? d : null;

    private static long? ReadLong(JsonElement o, string name) =>
        o.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out var v) ? v : null;

    private static short? ReadShort(JsonElement o, string name) =>
        o.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var v) ? (short)v : null;

    private static string? ReadString(JsonElement o, string name) =>
        o.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
}
