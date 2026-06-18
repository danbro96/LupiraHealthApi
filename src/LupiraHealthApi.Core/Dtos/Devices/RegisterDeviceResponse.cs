namespace LupiraHealthApi.Dtos.Devices;

/// <summary>Result of registering a device. <see cref="ApiKey"/> is the one-time plaintext ingest credential
/// (<c>{keyId:N}.{secret}</c>) — it is shown only here and never retrievable again; only its hash is stored.</summary>
public record RegisterDeviceResponse(DeviceDto Device, Guid KeyId, string ApiKey);
