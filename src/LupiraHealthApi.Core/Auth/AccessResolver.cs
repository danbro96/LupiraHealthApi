using LupiraHealthApi.Domain;
using Marten;

namespace LupiraHealthApi.Auth;

/// <summary>Ownership check over health records. Phase 1 has no sharing: a principal may read and write only the
/// records it owns. Every child resource carries the record id and inherits this check.</summary>
public sealed class AccessResolver(IQuerySession session)
{
    public async Task<List<Guid>> AccessibleRecordIdsAsync(Guid principalId, CancellationToken ct = default) =>
        await session.Query<HealthRecord>().Where(r => r.OwnerPrincipalId == principalId).Select(r => r.Id).ToListAsync(ct) is { } l ? [.. l] : [];

    public async Task<bool> CanReadRecordAsync(Guid principalId, Guid recordId, CancellationToken ct = default) =>
        await session.Query<HealthRecord>().AnyAsync(r => r.Id == recordId && r.OwnerPrincipalId == principalId, ct);

    /// <summary>Write access == ownership in phase 1 (read and write are the same gate).</summary>
    public Task<bool> CanWriteRecordAsync(Guid principalId, Guid recordId, CancellationToken ct = default) =>
        CanReadRecordAsync(principalId, recordId, ct);
}
