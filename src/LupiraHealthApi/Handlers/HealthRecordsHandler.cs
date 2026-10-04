using Lupira.Hosting.Problems;
using LupiraHealthApi.Auth;
using LupiraHealthApi.Core.Application;
using LupiraHealthApi.Core.Dtos.Records;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LupiraHealthApi.Handlers;

public sealed class HealthRecordsHandler(CurrentUser user, HealthRecordService records)
{
    public async Task<Results<Ok<List<HealthRecordDto>>, ProblemHttpResult, UnauthorizedHttpResult>> ListAsync(CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        return OpResultMap.OkProblem(await records.ListAsync(u.Id, ct));
    }

    public async Task<Results<Ok<HealthRecordDto>, ProblemHttpResult, UnauthorizedHttpResult>> CreateAsync(CreateHealthRecordRequest body, CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        return OpResultMap.OkProblem(await records.CreateAsync(u.Id, body, ct));
    }
}
