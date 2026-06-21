using LupiraHealthApi.Domain;
using LupiraHealthApi.Dtos.Devices;

namespace LupiraHealthApi.Mappers;

internal static class DeviceMapper
{
    public static DeviceDto ToResponse(this Device d) =>
        new() { Id = d.Id, HealthRecordId = d.HealthRecordId, Kind = d.Kind, Label = d.Label, ExternalId = d.ExternalId, RegisteredAt = d.RegisteredAt, RetiredAt = d.RetiredAt };
}
