using LupiraHealthApi.Domain;
using LupiraHealthApi.Dtos.Records;
using Marten;

namespace LupiraHealthApi.Application;

/// <summary>Lists and creates the health-record containers a principal owns. Phase 1 has no sharing — a record belongs
/// solely to its creator. (Co-owner grant/revoke is deferred to phase 2.)</summary>
public sealed class HealthRecordService(IDocumentSession session)
{
    public async Task<OpResult<List<HealthRecordDto>>> ListAsync(Guid principalId, CancellationToken ct = default)
    {
        var records = await session.Query<HealthRecord>().Where(r => r.OwnerPrincipalId == principalId).ToListAsync(ct);
        return OpResult<List<HealthRecordDto>>.Ok(records.Select(r => new HealthRecordDto(r.Id, r.Slug, r.DisplayName)).ToList());
    }

    public async Task<OpResult<HealthRecordDto>> CreateAsync(Guid principalId, CreateHealthRecordRequest r, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(r.Slug)) return OpResult<HealthRecordDto>.Invalid("Slug is required.");

        var record = new HealthRecord { Id = Guid.NewGuid(), Slug = r.Slug.Trim(), DisplayName = r.DisplayName, OwnerPrincipalId = principalId };
        session.Store(record);
        await session.SaveChangesAsync(ct);
        return OpResult<HealthRecordDto>.Ok(new HealthRecordDto(record.Id, record.Slug, record.DisplayName));
    }

    /// <summary>Ensures the caller has a <c>personal</c> health record; idempotent (a second call creates nothing).</summary>
    public async Task<OpResult<HealthRecordDto>> BootstrapPersonalAsync(Guid principalId, CancellationToken ct = default)
    {
        var existing = (await ListAsync(principalId, ct)).Value!;
        var personal = existing.FirstOrDefault(r => r.Slug == "personal")
            ?? (await CreateAsync(principalId, new CreateHealthRecordRequest("personal", "My Health"), ct)).Value!;
        return OpResult<HealthRecordDto>.Ok(personal);
    }
}
