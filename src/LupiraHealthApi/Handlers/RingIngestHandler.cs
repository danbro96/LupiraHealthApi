using LupiraHealthApi.Application.Telemetry;
using LupiraHealthApi.Auth;

namespace LupiraHealthApi.Handlers;

/// <summary>Ring + device-summary ingest (DeviceKey-authed).</summary>
public sealed class RingIngestHandler(IHttpContextAccessor http, RingIngestService ingest)
{
    public async Task<IResult> SamplesAsync(CancellationToken ct)
    {
        var ctx = http.HttpContext!;
        var (pid, did) = DeviceKeyClaims.Get(ctx.User);
        var r = await ingest.IngestSamplesAsync(pid, did, ctx.Request.Body, ct);
        return TypedResults.Accepted((string?)null, r.Value);
    }

    public async Task<IResult> SummariesAsync(CancellationToken ct)
    {
        var ctx = http.HttpContext!;
        var (pid, did) = DeviceKeyClaims.Get(ctx.User);
        var r = await ingest.IngestSummariesAsync(pid, did, ctx.Request.Body, ct);
        return TypedResults.Accepted((string?)null, r.Value);
    }
}
