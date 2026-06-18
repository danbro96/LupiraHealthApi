namespace LupiraHealthApi.Dtos.Devices;

/// <summary>Register a device against a health record. <c>Kind</c> is a <c>DeviceKind</c> name (case-insensitive).</summary>
public record RegisterDeviceRequest(Guid HealthRecordId, string Kind, string Label, string? ExternalId);
