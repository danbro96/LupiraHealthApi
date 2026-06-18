namespace LupiraHealthApi.Dtos.Devices;

/// <summary>A registered device.</summary>
public record DeviceDto(Guid Id, Guid HealthRecordId, string Kind, string Label, string? ExternalId, DateTimeOffset RegisteredAt, DateTimeOffset? RetiredAt);
