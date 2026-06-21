using LupiraHealthApi.Dtos.Records;
using LupiraHealthApi.Handlers;

namespace LupiraHealthApi.Endpoints;

public static class HealthRecordsEndpoints
{
    public static IEndpointRouteBuilder MapHealthRecords(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/records").RequireAuthorization("ApiPolicy").WithTags("HealthRecords");

        g.MapGet("/", (HealthRecordsHandler h, CancellationToken ct) => h.ListAsync(ct))
            .WithSummary("List the health records the caller owns.")
            .Produces<List<HealthRecordDto>>(StatusCodes.Status200OK);

        g.MapPost("/", (CreateHealthRecordRequest body, HealthRecordsHandler h, CancellationToken ct) => h.CreateAsync(body, ct))
            .WithSummary("Create a health record (the caller becomes its owner).")
            .Produces<HealthRecordDto>(StatusCodes.Status200OK);
        return app;
    }
}
