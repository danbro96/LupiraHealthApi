namespace LupiraHealthApi.Dtos.Records;

/// <summary>Create a health record. The caller is granted <c>owner</c>.</summary>
public sealed class CreateHealthRecordRequest
{
    public required string Slug { get; set; }
    public string? DisplayName { get; set; }
}
