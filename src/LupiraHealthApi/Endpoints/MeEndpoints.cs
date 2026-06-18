using LupiraHealthApi.Dtos.Me;
using LupiraHealthApi.Dtos.Records;
using LupiraHealthApi.Handlers;

namespace LupiraHealthApi.Endpoints;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMe(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", (MeHandler h, CancellationToken ct) => h.GetAsync(ct))
            .RequireAuthorization("ApiPolicy").WithTags("Me")
            .WithSummary("The caller's resolved local identity (JIT-provisioned on first login).")
            .Produces<MeDto>(StatusCodes.Status200OK).Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/api/me/bootstrap", (MeHandler h, CancellationToken ct) => h.BootstrapAsync(ct))
            .RequireAuthorization("ApiPolicy").WithTags("Me")
            .WithSummary("Idempotently ensure the caller has a personal health record; returns it.")
            .Produces<HealthRecordDto>(StatusCodes.Status200OK).Produces(StatusCodes.Status401Unauthorized);
        return app;
    }
}
