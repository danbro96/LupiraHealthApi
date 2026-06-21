using LupiraHealthApi.Application.Telemetry;
using LupiraHealthApi.Auth;
using LupiraHealthApi.Dtos.Ring;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LupiraHealthApi.Handlers;

/// <summary>Ring + device-summary ingest (DeviceKey-authed).</summary>
public sealed class RingIngestHandler(IHttpContextAccessor http, RingIngestService ingest)
{
    public async Task<Results<Accepted<RingIngestReceipt>, UnauthorizedHttpResult>> SamplesAsync(CancellationToken ct)
    {
        var ctx = http.HttpContext!;
        var (pid, did) = DeviceKeyClaims.Get(ctx.User);
        var r = await ingest.IngestSamplesAsync(pid, did, ctx.Request.Body, ct);
        return TypedResults.Accepted((string?)null, r.Value!);
    }

    public async Task<Results<Accepted<RingIngestReceipt>, UnauthorizedHttpResult>> SummariesAsync(CancellationToken ct)
    {
        var ctx = http.HttpContext!;
        var (pid, did) = DeviceKeyClaims.Get(ctx.User);
        var r = await ingest.IngestSummariesAsync(pid, did, ctx.Request.Body, ct);
        return TypedResults.Accepted((string?)null, r.Value!);
    }
}
