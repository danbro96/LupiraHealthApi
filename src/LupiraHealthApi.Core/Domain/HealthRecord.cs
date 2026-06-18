namespace LupiraHealthApi.Domain;

/// <summary>A health record — the per-principal container all health data belongs to (plain document, not versioned).
/// Phase 1: a record is owned solely by its creating principal (<see cref="OwnerPrincipalId"/>); there is no sharing.
/// Every child resource (devices, telemetry) carries the <c>HealthRecordId</c> (or the owner's principal id) and
/// inherits this ownership check.</summary>
public sealed class HealthRecord
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string? DisplayName { get; set; }
    public Guid OwnerPrincipalId { get; set; }
}
