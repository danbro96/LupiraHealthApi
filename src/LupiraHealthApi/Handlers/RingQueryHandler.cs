using LupiraHealthApi.Application.Telemetry;
using LupiraHealthApi.Auth;
using LupiraHealthApi.Http;

namespace LupiraHealthApi.Handlers;

/// <summary>The owner-facing ring read surface (OIDC-authed).</summary>
public sealed class RingQueryHandler(CurrentUser user, RingQueryService q)
{
    public async Task<IResult> DownsampleAsync(string kind, DateTimeOffset? from, DateTimeOffset? to, int? bucketSeconds, Guid? deviceId, CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        if (!RingMetrics.TryParse(kind, out var metric)) return Problems.BadRequest("Unknown ring metric.");
        var t = to ?? DateTimeOffset.UtcNow;
        var f = from ?? t.AddDays(-1);
        var bucket = TimeSpan.FromSeconds(bucketSeconds is > 0 ? bucketSeconds.Value : 60);
        return TypedResults.Ok((await q.DownsampleAsync(u.Id, deviceId, metric, f, t, bucket, ct)).Value);
    }

    public async Task<IResult> SummariesAsync(DateTimeOffset? from, DateTimeOffset? to, int? kind, Guid? deviceId, CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        var t = to ?? DateTimeOffset.UtcNow;
        var f = from ?? t.AddDays(-30);
        return TypedResults.Ok((await q.SummariesAsync(u.Id, deviceId, (short?)kind, f, t, ct)).Value);
    }
}
