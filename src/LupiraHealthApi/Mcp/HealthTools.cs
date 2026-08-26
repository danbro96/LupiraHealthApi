using System.ComponentModel;
using LupiraHealthApi.Auth;
using LupiraHealthApi.Core.Application;
using LupiraHealthApi.Core.Application.Results;
using LupiraHealthApi.Core.Application.Telemetry;
using LupiraHealthApi.Core.Domain.Telemetry;
using LupiraHealthApi.Core.Dtos.Devices;
using LupiraHealthApi.Core.Dtos.Me;
using LupiraHealthApi.Core.Dtos.Records;
using LupiraHealthApi.Core.Dtos.Ring;
using ModelContextProtocol;
using ModelContextProtocol.Server;

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
    [Description("Resolve who the caller is in this service, returning their local id, email and display name. " +
        "The id is the key every other tool here scopes to, so call this first when you need to reason about " +
        "ownership or correlate with another Lupira service. Identity is taken from the bearer principal and " +
        "provisioned on first use, so this succeeds even for a caller with no health data yet.")]
    public async Task<MeDto> WhoAmI(CancellationToken ct = default)
    {
        var me = await user.GetAsync(ct);
        return new MeDto { Id = me.Id, Email = me.Email, DisplayName = me.DisplayName };
    }

    [McpServerTool(Name = "list_health_records")]
    [Description("List the health records the caller owns. A record is the container that devices feed and that " +
        "vitals and summaries hang off, so this is the starting point for finding the record id other tools take. " +
        "Returns every record with no filter. It does not return the devices or the readings themselves — use " +
        "list_devices, read_vitals and read_summaries for those.")]
    public async Task<List<HealthRecordDto>> ListHealthRecords(CancellationToken ct = default)
    {
        var me = await user.GetAsync(ct);
        return Require(await records.ListAsync(me.Id, ct));
    }

    [McpServerTool(Name = "list_devices")]
    [Description("List the devices — rings, watches, scales and the like — that feed the caller's health records. " +
        "Pass a record id to see just that record's devices; omit it and the results are aggregated across every " +
        "record the caller owns, which is usually what you want when answering 'what is tracking me'. Use the " +
        "device ids returned here to narrow read_vitals or read_summaries to one device.")]
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
    [Description("Read one ring vital over a time range, downsampled into fixed-width buckets that each carry " +
        "avg, min, max and a sample count. Choosing the metric is required; the range defaults to the last 24 " +
        "hours and buckets to 60 seconds, so widen the bucket when asking about days or weeks or the result set " +
        "gets large. Readings from every device are included unless you narrow to one. Because it returns buckets " +
        "rather than raw samples, it answers trends and ranges — not the exact value at an instant.")]
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
    [Description("Read the summaries the devices computed themselves — sleep sessions, daily totals and similar — " +
        "over a time range, defaulting to the last 30 days. These are the device's own conclusions rather than " +
        "anything derived here, and each carries its raw JSON payload, so read that for fields this API does not " +
        "model. Kind is the device-assigned smallint, so discover the kinds in use by calling without it first. " +
        "For continuous measurements rather than device conclusions, use read_vitals.")]
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
        return Require(await ring.SummariesAsync(me.Id, deviceId, (short?) kind, f, t, ct));
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
