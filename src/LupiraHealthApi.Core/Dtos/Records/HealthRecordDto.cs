namespace LupiraHealthApi.Dtos.Records;

/// <summary>A health record owned by the caller.</summary>
public record HealthRecordDto(Guid Id, string Slug, string? DisplayName);
