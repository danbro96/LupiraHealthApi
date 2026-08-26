namespace LupiraHealthApi.Core.Dtos.Ring;

/// <summary>A device-computed summary (sleep session, daily totals…). <see cref="Payload"/> is the raw JSON the device sent.</summary>
public sealed class DeviceSummaryDto
{
    public required Guid DeviceId { get; set; }
    public required int Kind { get; set; }
    public required DateTimeOffset PeriodStart { get; set; }
    public required DateTimeOffset PeriodEnd { get; set; }
    public required string Payload { get; set; }
}
