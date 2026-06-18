using LupiraHealthApi.Handlers;

namespace LupiraHealthApi.Endpoints;

/// <summary>The telemetry ingest surface — DeviceKey-authed (the future mobile uploader). NDJSON bodies; principal/device
/// are stamped from the key.</summary>
public static class IngestEndpoints
{
    public static IEndpointRouteBuilder MapIngest(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/ingest").RequireAuthorization("IngestPolicy").WithTags("Ingest");

        g.MapPost("/location", (LocationIngestHandler h, CancellationToken ct) => h.IngestAsync(ct))
            .WithSummary("Ingest a batch of GPS fixes (NDJSON, one fix per line).")
            .Accepts<string>("application/x-ndjson");
        g.MapGet("/location/cursor", (LocationIngestHandler h, CancellationToken ct) => h.CursorAsync(ct))
            .WithSummary("The device's resume cursor (last accepted seq + ts).");
        g.MapGet("/location/state", (LocationIngestHandler h, CancellationToken ct) => h.StateAsync(ct))
            .WithSummary("Whether tracking is paused for this device (the uploader should stop collecting if so).");

        g.MapPost("/ring", (RingIngestHandler h, CancellationToken ct) => h.SamplesAsync(ct))
            .WithSummary("Ingest a batch of ring point-samples (NDJSON).")
            .Accepts<string>("application/x-ndjson");
        g.MapPost("/summaries", (RingIngestHandler h, CancellationToken ct) => h.SummariesAsync(ct))
            .WithSummary("Ingest a batch of device-computed summaries (NDJSON).")
            .Accepts<string>("application/x-ndjson");
        return app;
    }
}
