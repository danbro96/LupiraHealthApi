using LupiraHealthApi.Dtos.Devices;
using LupiraHealthApi.Handlers;

namespace LupiraHealthApi.Endpoints;

public static class DevicesEndpoints
{
    public static IEndpointRouteBuilder MapDevices(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/devices").RequireAuthorization("ApiPolicy").WithTags("Devices");

        g.MapGet("/", (Guid recordId, DevicesHandler h, CancellationToken ct) => h.ListAsync(recordId, ct))
            .WithSummary("List the devices registered to a health record.")
            .Produces<List<DeviceDto>>(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status403Forbidden);

        g.MapPost("/", (RegisterDeviceRequest body, DevicesHandler h, CancellationToken ct) => h.RegisterAsync(body, ct))
            .WithSummary("Register a device; returns the one-time ingest API key.")
            .Produces<RegisterDeviceResponse>(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status403Forbidden);

        g.MapPut("/{id:guid}", (Guid id, RenameDeviceRequest body, DevicesHandler h, CancellationToken ct) => h.RenameAsync(id, body, ct))
            .WithSummary("Rename a device.")
            .Produces<DeviceDto>(StatusCodes.Status200OK).Produces(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status403Forbidden);

        g.MapDelete("/{id:guid}", (Guid id, DevicesHandler h, CancellationToken ct) => h.RetireAsync(id, ct))
            .WithSummary("Retire a device (revokes its ingest keys).")
            .Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status403Forbidden);
        return app;
    }
}
