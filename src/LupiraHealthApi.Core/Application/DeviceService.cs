using LupiraHealthApi.Auth;
using LupiraHealthApi.Domain;
using LupiraHealthApi.Dtos.Devices;
using LupiraHealthApi.Mappers;
using Marten;

namespace LupiraHealthApi.Application;

/// <summary>Registers and manages devices on a health record (plain-doc CRUD). Registration mints a per-device ingest
/// API key (the plaintext is returned once); retiring a device revokes its keys. All operations are gated on the
/// caller's access to the device's record — and on mutation, re-checked against the device's own <c>HealthRecordId</c>.</summary>
public sealed class DeviceService(IDocumentSession session, AccessResolver access)
{
    public async Task<OpResult<List<DeviceDto>>> ListAsync(Guid principalId, Guid recordId, CancellationToken ct = default)
    {
        if (!await access.CanReadRecordAsync(principalId, recordId, ct)) return OpResult<List<DeviceDto>>.Forbidden("No access to this health record.");
        var devices = await session.Query<Device>().Where(d => d.HealthRecordId == recordId).ToListAsync(ct);
        return OpResult<List<DeviceDto>>.Ok(devices.Select(d => d.ToResponse()).ToList());
    }

    public async Task<OpResult<RegisterDeviceResponse>> RegisterAsync(Guid principalId, RegisterDeviceRequest r, CancellationToken ct = default)
    {
        if (!await access.CanWriteRecordAsync(principalId, r.HealthRecordId, ct)) return OpResult<RegisterDeviceResponse>.Forbidden("No write access to this health record.");
        if (string.IsNullOrWhiteSpace(r.Label)) return OpResult<RegisterDeviceResponse>.Invalid("Label is required.");
        if (!Enum.TryParse<DeviceKind>(r.Kind, ignoreCase: true, out var kind)) return OpResult<RegisterDeviceResponse>.Invalid("Unknown device kind.");

        var device = new Device
        {
            Id = Guid.NewGuid(),
            HealthRecordId = r.HealthRecordId,
            Kind = kind,
            Label = r.Label.Trim(),
            ExternalId = r.ExternalId,
            RegisteredAt = DateTimeOffset.UtcNow,
        };
        session.Store(device);

        var (keyId, secret, hash) = DeviceKeyHashing.Mint();
        session.Store(new DeviceApiKey
        {
            Id = keyId,
            PrincipalId = principalId,
            DeviceId = device.Id,
            KeyHash = hash,
            Scopes = ["ingest"],
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await session.SaveChangesAsync(ct);

        return OpResult<RegisterDeviceResponse>.Ok(new RegisterDeviceResponse(device.ToResponse(), keyId, DeviceKeyHashing.Format(keyId, secret)));
    }

    public async Task<OpResult<DeviceDto>> RenameAsync(Guid principalId, Guid deviceId, RenameDeviceRequest r, CancellationToken ct = default)
    {
        var device = await session.LoadAsync<Device>(deviceId, ct);
        if (device is null) return OpResult<DeviceDto>.NotFound();
        if (!await access.CanWriteRecordAsync(principalId, device.HealthRecordId, ct)) return OpResult<DeviceDto>.Forbidden("No write access to this device.");
        if (string.IsNullOrWhiteSpace(r.Label)) return OpResult<DeviceDto>.Invalid("Label is required.");
        device.Label = r.Label.Trim();
        session.Store(device);
        await session.SaveChangesAsync(ct);
        return OpResult<DeviceDto>.Ok(device.ToResponse());
    }

    public async Task<OpResult> RetireAsync(Guid principalId, Guid deviceId, CancellationToken ct = default)
    {
        var device = await session.LoadAsync<Device>(deviceId, ct);
        if (device is null || device.RetiredAt is not null) return OpResult.NotFound();
        if (!await access.CanWriteRecordAsync(principalId, device.HealthRecordId, ct)) return OpResult.Forbidden("No write access to this device.");
        device.RetiredAt = DateTimeOffset.UtcNow;
        session.Store(device);

        // Revoke the device's ingest keys so a retired device can no longer push.
        var keys = await session.Query<DeviceApiKey>().Where(k => k.DeviceId == deviceId && k.RevokedAt == null).ToListAsync(ct);
        foreach (var k in keys) { k.RevokedAt = DateTimeOffset.UtcNow; session.Store(k); }
        await session.SaveChangesAsync(ct);
        return OpResult.Ok();
    }
}
