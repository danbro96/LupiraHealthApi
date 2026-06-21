namespace LupiraHealthApi.Dtos.Records;

/// <summary>A health record owned by the caller.</summary>
public sealed class HealthRecordDto
{
    public required Guid Id { get; set; }
    public required string Slug { get; set; }
    public string? DisplayName { get; set; }
}
