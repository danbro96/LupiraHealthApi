using Npgsql;

namespace LupiraHealthApi.Telemetry;

/// <summary>Owns the raw <c>telemetry</c> schema (ring tables + indexes), applied via the app's <c>--apply-schema</c>
/// one-shot after Marten's own apply. Marten's schema-diff only inspects the <c>health</c> schema, so it never touches
/// these tables. Ongoing partition create/drop is handled at runtime (ingest pre-creates on demand; the maintenance
/// service pre-provisions upcoming ones). All DDL is idempotent.</summary>
public static class TelemetrySchema
{
    public static async Task ApplyAsync(NpgsqlDataSource db, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(Ddl, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Wipes all telemetry rows (test isolation). TRUNCATE on a partitioned parent cascades to its partitions.</summary>
    public static async Task TruncateAllAsync(NpgsqlDataSource db, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("TRUNCATE telemetry.ring_sample, telemetry.device_summary", conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private const string Ddl = """
        CREATE SCHEMA IF NOT EXISTS telemetry;

        CREATE TABLE IF NOT EXISTS telemetry.ring_sample (
            principal_id uuid          NOT NULL,
            device_id    uuid          NOT NULL,
            kind         smallint      NOT NULL,
            ts           timestamptz   NOT NULL,
            value        numeric(10,3) NOT NULL,
            seq          bigint        NOT NULL,
            PRIMARY KEY (principal_id, device_id, kind, ts, seq)
        ) PARTITION BY RANGE (ts);

        CREATE INDEX IF NOT EXISTS ix_ring_sample_pid_kind_ts ON telemetry.ring_sample (principal_id, kind, ts);

        CREATE TABLE IF NOT EXISTS telemetry.device_summary (
            principal_id uuid        NOT NULL,
            device_id    uuid        NOT NULL,
            kind         smallint    NOT NULL,
            period_start timestamptz NOT NULL,
            period_end   timestamptz NOT NULL,
            payload      jsonb       NOT NULL,
            seq          bigint      NOT NULL,
            PRIMARY KEY (principal_id, device_id, kind, period_start, seq)
        ) PARTITION BY RANGE (period_start);

        CREATE INDEX IF NOT EXISTS ix_device_summary_pid_kind_start ON telemetry.device_summary (principal_id, kind, period_start);
        """;
}
