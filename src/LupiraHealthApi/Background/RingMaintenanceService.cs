using LupiraHealthApi.Telemetry;
using Npgsql;

namespace LupiraHealthApi.Background;

/// <summary>Periodic maintenance for the ring telemetry store: pre-provision upcoming monthly partitions for
/// <c>ring_sample</c> + <c>device_summary</c> so ingest never blocks on DDL. Gated by
/// <c>Telemetry:MaintenanceEnabled</c> (disabled in tests so it never races the per-test reset).</summary>
public sealed class RingMaintenanceService(
    NpgsqlDataSource db,
    PartitionManager partitions,
    IConfiguration config,
    ILogger<RingMaintenanceService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!config.GetValue("Telemetry:MaintenanceEnabled", true)) return;

        try { await Task.Delay(TimeSpan.FromMinutes(2), ct); }
        catch (OperationCanceledException) { return; }

        while (!ct.IsCancellationRequested)
        {
            try { await RunOnceAsync(ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Ring maintenance pass failed."); }
            try { await Task.Delay(TimeSpan.FromHours(1), ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        await using var conn = await db.OpenConnectionAsync(ct);
        await partitions.EnsureRangeAsync(conn, "ring_sample", PartitionInterval.Monthly, now.AddMonths(-1), now.AddMonths(1), ct);
        await partitions.EnsureRangeAsync(conn, "device_summary", PartitionInterval.Monthly, now.AddMonths(-1), now.AddMonths(1), ct);
    }
}
