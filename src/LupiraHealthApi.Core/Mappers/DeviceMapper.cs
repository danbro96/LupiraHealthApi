using LupiraHealthApi.Core.Domain;
using LupiraHealthApi.Core.Dtos.Devices;

namespace LupiraHealthApi.Core.Mappers;

internal static class DeviceMapper
{
    public static DeviceDto ToResponse(this Device d) =>
        new() { Id = d.Id, HealthRecordId = d.HealthRecordId, Kind = d.Kind, Label = d.Label, ExternalId = d.ExternalId, RegisteredAt = d.RegisteredAt, RetiredAt = d.RetiredAt };
}
