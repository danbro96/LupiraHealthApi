using LupiraHealthApi.Core.Domain;

namespace LupiraHealthApi.Core.Dtos.Devices;

/// <summary>Register a device against a health record. <c>Kind</c> is a <c>DeviceKind</c> name (case-insensitive).</summary>
public sealed class RegisterDeviceRequest
{
    public required Guid HealthRecordId { get; set; }
    public required DeviceKind Kind { get; set; }
    public required string Label { get; set; }
    public string? ExternalId { get; set; }
}
