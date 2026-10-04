using Lupira.Hosting.Problems;
using LupiraHealthApi.Auth;
using LupiraHealthApi.Core.Application;
using LupiraHealthApi.Core.Dtos.Me;
using LupiraHealthApi.Core.Dtos.Records;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LupiraHealthApi.Handlers;

public sealed class MeHandler(CurrentUser user, HealthRecordService records)
{
    public async Task<Results<Ok<MeDto>, UnauthorizedHttpResult>> GetAsync(CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        return TypedResults.Ok(new MeDto { Id = u.Id, Email = u.Email, DisplayName = u.DisplayName });
    }

    public async Task<Results<Ok<HealthRecordDto>, ProblemHttpResult, UnauthorizedHttpResult>> BootstrapAsync(CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        return OpResultMap.OkProblem(await records.BootstrapPersonalAsync(u.Id, ct));
    }
}
