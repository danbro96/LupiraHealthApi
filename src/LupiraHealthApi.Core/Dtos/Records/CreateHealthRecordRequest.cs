namespace LupiraHealthApi.Dtos.Records;

/// <summary>Create a health record. The caller is granted <c>owner</c>.</summary>
public record CreateHealthRecordRequest(string Slug, string? DisplayName);
