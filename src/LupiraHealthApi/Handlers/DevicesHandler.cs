using LupiraHealthApi.Application;
using LupiraHealthApi.Auth;
using LupiraHealthApi.Dtos.Devices;
using LupiraHealthApi.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LupiraHealthApi.Handlers;

public sealed class DevicesHandler(CurrentUser user, DeviceService devices)
{
    public async Task<Results<Ok<List<DeviceDto>>, ProblemHttpResult, UnauthorizedHttpResult>> ListAsync(Guid recordId, CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        return OpResultMap.OkProblem(await devices.ListAsync(u.Id, recordId, ct));
    }

    public async Task<Results<Ok<RegisterDeviceResponse>, ProblemHttpResult, UnauthorizedHttpResult>> RegisterAsync(RegisterDeviceRequest body, CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        return OpResultMap.OkProblem(await devices.RegisterAsync(u.Id, body, ct));
    }

    public async Task<Results<Ok<DeviceDto>, NotFound, ProblemHttpResult, UnauthorizedHttpResult>> RenameAsync(Guid id, RenameDeviceRequest body, CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        return OpResultMap.OkNotFoundProblem(await devices.RenameAsync(u.Id, id, body, ct));
    }

    public async Task<Results<NoContent, NotFound, ProblemHttpResult, UnauthorizedHttpResult>> RetireAsync(Guid id, CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        return OpResultMap.NoContentNotFoundProblem(await devices.RetireAsync(u.Id, id, ct));
    }
}
