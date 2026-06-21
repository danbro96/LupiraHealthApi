using LupiraHealthApi.Dtos.Ring;
using LupiraHealthApi.Handlers;

namespace LupiraHealthApi.Endpoints;

/// <summary>The telemetry ingest surface — DeviceKey-authed (the mobile uploader). NDJSON bodies; principal/device
/// are stamped from the key.</summary>
public static class IngestEndpoints
{
    public static IEndpointRouteBuilder MapIngest(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/ingest").RequireAuthorization("IngestPolicy").WithTags("Ingest");

        g.MapPost("/ring", (RingIngestHandler h, CancellationToken ct) => h.SamplesAsync(ct))
            .WithSummary("Ingest a batch of ring point-samples (NDJSON).")
            .Accepts<string>("application/x-ndjson").Produces<RingIngestReceipt>(StatusCodes.Status202Accepted);
        g.MapPost("/summaries", (RingIngestHandler h, CancellationToken ct) => h.SummariesAsync(ct))
            .WithSummary("Ingest a batch of device-computed summaries (NDJSON).")
            .Accepts<string>("application/x-ndjson").Produces<RingIngestReceipt>(StatusCodes.Status202Accepted);
        return app;
    }
}
