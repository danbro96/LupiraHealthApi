using LupiraHealthApi.Dtos.Ring;
using LupiraHealthApi.Handlers;

namespace LupiraHealthApi.Endpoints;

/// <summary>The owner-facing ring read surface (OIDC-authed).</summary>
public static class RingQueryEndpoints
{
    public static IEndpointRouteBuilder MapRingQuery(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/health").RequireAuthorization("ApiPolicy").WithTags("Ring");

        g.MapGet("/ring", (string kind, DateTimeOffset? from, DateTimeOffset? to, int? bucketSeconds, Guid? deviceId, RingQueryHandler h, CancellationToken ct) => h.DownsampleAsync(kind, from, to, bucketSeconds, deviceId, ct))
            .WithSummary("Downsampled ring metric (avg/min/max/count per bucket).").Produces<List<RingBucketDto>>(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status400BadRequest);

        g.MapGet("/summaries", (DateTimeOffset? from, DateTimeOffset? to, int? kind, Guid? deviceId, RingQueryHandler h, CancellationToken ct) => h.SummariesAsync(from, to, kind, deviceId, ct))
            .WithSummary("Device-computed summaries (sleep sessions, daily totals…).").Produces<List<DeviceSummaryDto>>(StatusCodes.Status200OK);
        return app;
    }
}
