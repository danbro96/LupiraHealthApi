namespace LupiraHealthApi.Domain;

/// <summary>A registered device that feeds a health record (plain document — pure registration metadata, no clinical
/// audit value). Telemetry rows carry the <see cref="Id"/> by value; per-device ingest credentials live in
/// <see cref="DeviceApiKey"/>.</summary>
public sealed class Device
{
    public Guid Id { get; set; }
    public Guid HealthRecordId { get; set; }
    public DeviceKind Kind { get; set; }
    public string Label { get; set; } = "";
    public string? ExternalId { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public DateTimeOffset? RetiredAt { get; set; }
}
