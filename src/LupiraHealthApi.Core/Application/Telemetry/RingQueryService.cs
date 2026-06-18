using LupiraHealthApi.Domain.Telemetry;
using LupiraHealthApi.Dtos.Ring;
using Npgsql;
using NpgsqlTypes;

namespace LupiraHealthApi.Application.Telemetry;

/// <summary>Read API over a principal's own ring telemetry. Downsampling is computed on read via <c>date_bin</c> (no
/// extension). Every query hard-filters <c>principal_id = caller</c>.</summary>
public sealed class RingQueryService(NpgsqlDataSource db)
{
    public async Task<OpResult<List<RingBucketDto>>> DownsampleAsync(Guid pid, Guid? deviceId, RingMetric metric, DateTimeOffset from, DateTimeOffset to, TimeSpan bucket, CancellationToken ct = default)
    {
        const string sql = """
            SELECT date_bin(@bucket, ts, @from) AS b, avg(value), min(value), max(value), count(*)
            FROM telemetry.ring_sample
            WHERE principal_id = @pid AND kind = @kind AND ts >= @from AND ts < @to AND (@did::uuid IS NULL OR device_id = @did)
            GROUP BY b
            ORDER BY b
            """;
        var result = new List<RingBucketDto>();
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("pid", pid);
        cmd.Parameters.AddWithValue("did", (object?)deviceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("kind", (short)metric);
        cmd.Parameters.AddWithValue("from", NpgsqlDbType.TimestampTz, from.UtcDateTime);
        cmd.Parameters.AddWithValue("to", NpgsqlDbType.TimestampTz, to.UtcDateTime);
        cmd.Parameters.AddWithValue("bucket", bucket);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            result.Add(new RingBucketDto(new DateTimeOffset(r.GetFieldValue<DateTime>(0), TimeSpan.Zero),
                Db.Double0(r, 1), Db.Double0(r, 2), Db.Double0(r, 3), r.GetInt64(4)));
        return OpResult<List<RingBucketDto>>.Ok(result);
    }

    public async Task<OpResult<List<DeviceSummaryDto>>> SummariesAsync(Guid pid, Guid? deviceId, short? kind, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        const string sql = """
            SELECT device_id, kind, period_start, period_end, payload
            FROM telemetry.device_summary
            WHERE principal_id = @pid AND period_start >= @from AND period_start < @to
              AND (@did::uuid IS NULL OR device_id = @did)
              AND (@kind::smallint IS NULL OR kind = @kind)
            ORDER BY period_start
            """;
        var result = new List<DeviceSummaryDto>();
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("pid", pid);
        cmd.Parameters.AddWithValue("did", (object?)deviceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("kind", (object?)kind ?? DBNull.Value);
        cmd.Parameters.AddWithValue("from", NpgsqlDbType.TimestampTz, from.UtcDateTime);
        cmd.Parameters.AddWithValue("to", NpgsqlDbType.TimestampTz, to.UtcDateTime);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            result.Add(new DeviceSummaryDto(r.GetGuid(0), r.GetInt16(1),
                new DateTimeOffset(r.GetFieldValue<DateTime>(2), TimeSpan.Zero),
                new DateTimeOffset(r.GetFieldValue<DateTime>(3), TimeSpan.Zero),
                r.GetFieldValue<string>(4)));
        return OpResult<List<DeviceSummaryDto>>.Ok(result);
    }
}
