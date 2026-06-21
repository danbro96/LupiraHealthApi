using LupiraHealthApi.Application;
using LupiraHealthApi.Application.Telemetry;
using LupiraHealthApi.Auth;
using LupiraHealthApi.Domain.Telemetry;
using LupiraHealthApi.Dtos.Devices;
using LupiraHealthApi.Dtos.Me;
using LupiraHealthApi.Dtos.Records;
using LupiraHealthApi.Dtos.Ring;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace LupiraHealthApi.Mcp;

/// <summary>
/// The agent's MCP surface — read-only. These tools call the SAME <see cref="HealthRecordService"/>/
/// <see cref="DeviceService"/>/<see cref="RingQueryService"/> as the REST handlers, so there is no second
/// source of truth. Identity comes from the bearer principal on the MCP transport (<see cref="CurrentUser"/>,
/// JIT-provisioned), so every query is hard-scoped to that user's own records, devices, and telemetry by the
/// services' ownership checks. Mutations and device-key ingest are deliberately out of scope.
/// </summary>
[McpServerToolType]
public sealed class HealthTools(CurrentUser user, HealthRecordService records, DeviceService devices, RingQueryService ring)
{
    [McpServerTool(Name = "whoami")]
    [Description("Resolve the calling user's identity in this service (id, email, display name).")]
    public async Task<MeDto> WhoAmI(CancellationToken ct = default)
    {
        var me = await user.GetAsync(ct);
        return new MeDto { Id = me.Id, Email = me.Email, DisplayName = me.DisplayName };
    }

    [McpServerTool(Name = "list_health_records")]
    [Description("List the health records the current user owns.")]
    public async Task<List<HealthRecordDto>> ListHealthRecords(CancellationToken ct = default)
    {
        var me = await user.GetAsync(ct);
        return Require(await records.ListAsync(me.Id, ct));
    }

    [McpServerTool(Name = "list_devices")]
    [Description("List devices (rings, watches, scales…) feeding the current user's health records.")]
    public async Task<List<DeviceDto>> ListDevices(
        [Description("Restrict to one health record. Omit to aggregate devices across all your records.")] Guid? recordId = null,
        CancellationToken ct = default)
    {
        var me = await user.GetAsync(ct);
        if (recordId is { } rid)
            return Require(await devices.ListAsync(me.Id, rid, ct));

        var all = new List<DeviceDto>();
        foreach (var record in Require(await records.ListAsync(me.Id, ct)))
            all.AddRange(Require(await devices.ListAsync(me.Id, record.Id, ct)));
        return all;
    }

    [McpServerTool(Name = "read_vitals")]
    [Description("Read a downsampled ring vital over a time range as avg/min/max/count buckets.")]
    public async Task<List<RingBucketDto>> ReadVitals(
        [Description("Which vital to read: HeartRate, Hrv, Spo2, SkinTemp, Steps, or Activity.")] RingMetric metric,
        [Description("Range start (ISO-8601). Defaults to 24h before 'to'.")] DateTimeOffset? from = null,
        [Description("Range end (ISO-8601). Defaults to now.")] DateTimeOffset? to = null,
        [Description("Bucket width in seconds (default 60).")] int? bucketSeconds = null,
        [Description("Restrict to one device. Omit to include all the user's devices.")] Guid? deviceId = null,
        CancellationToken ct = default)
    {
        if (metric == RingMetric.Unknown)
            throw new McpException("Choose a vital: HeartRate, Hrv, Spo2, SkinTemp, Steps, or Activity.");
        var me = await user.GetAsync(ct);
        var t = to ?? DateTimeOffset.UtcNow;
        var f = from ?? t.AddDays(-1);
        var bucket = TimeSpan.FromSeconds(bucketSeconds is > 0 ? bucketSeconds.Value : 60);
        return Require(await ring.DownsampleAsync(me.Id, deviceId, metric, f, t, bucket, ct));
    }

    [McpServerTool(Name = "read_summaries")]
    [Description("Read device-computed summaries (sleep sessions, daily totals…) over a time range; each carries a raw JSON payload.")]
    public async Task<List<DeviceSummaryDto>> ReadSummaries(
        [Description("Range start (ISO-8601). Defaults to 30 days before 'to'.")] DateTimeOffset? from = null,
        [Description("Range end (ISO-8601). Defaults to now.")] DateTimeOffset? to = null,
        [Description("Restrict to one device-summary kind (the device-assigned smallint). Omit for all kinds.")] int? kind = null,
        [Description("Restrict to one device. Omit to include all the user's devices.")] Guid? deviceId = null,
        CancellationToken ct = default)
    {
        var me = await user.GetAsync(ct);
        var t = to ?? DateTimeOffset.UtcNow;
        var f = from ?? t.AddDays(-30);
        return Require(await ring.SummariesAsync(me.Id, deviceId, (short?)kind, f, t, ct));
    }

    /// <summary>Unwraps a service outcome or surfaces it to the agent as an <see cref="McpException"/>.</summary>
    private static T Require<T>(OpResult<T> r) => r.Status switch
    {
        OpStatus.Ok => r.Value!,
        OpStatus.NotFound => throw new McpException("Not found, or you don't have access to it."),
        OpStatus.Forbidden => throw new McpException(r.Error ?? "You don't have permission to do that."),
        OpStatus.Invalid => throw new McpException(r.Error ?? "The request was invalid."),
        OpStatus.Conflict => throw new McpException(r.Error ?? "Conflict."),
        _ => throw new McpException("Unexpected error."),
    };
}
