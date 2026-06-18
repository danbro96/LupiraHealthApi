using LupiraHealthApi.Application;
using LupiraHealthApi.Auth;
using LupiraHealthApi.Dtos.Me;
using LupiraHealthApi.Dtos.Records;
using LupiraHealthApi.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LupiraHealthApi.Handlers;

public sealed class MeHandler(CurrentUser user, HealthRecordService records)
{
    public async Task<Results<Ok<MeDto>, UnauthorizedHttpResult>> GetAsync(CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        return TypedResults.Ok(new MeDto(u.Id, u.Email, u.DisplayName));
    }

    public async Task<Results<Ok<HealthRecordDto>, ProblemHttpResult, UnauthorizedHttpResult>> BootstrapAsync(CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        return OpResultMap.OkProblem(await records.BootstrapPersonalAsync(u.Id, ct));
    }
}
