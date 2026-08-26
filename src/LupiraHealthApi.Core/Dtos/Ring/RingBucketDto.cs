namespace LupiraHealthApi.Core.Dtos.Ring;

/// <summary>One downsampled bucket of a ring metric over a time range.</summary>
public sealed class RingBucketDto
{
    public required DateTimeOffset BucketTs { get; set; }

    public required double Avg { get; set; }

    public required double Min { get; set; }

    public required double Max { get; set; }

    public required long Count { get; set; }
}
