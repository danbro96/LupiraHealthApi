using LupiraHealthApi.Domain;
using LupiraHealthApi.Dtos.Devices;

namespace LupiraHealthApi.Mappers;

internal static class DeviceMapper
{
    public static DeviceDto ToResponse(this Device d) =>
        new(d.Id, d.HealthRecordId, d.Kind.ToString(), d.Label, d.ExternalId, d.RegisteredAt, d.RetiredAt);
}
