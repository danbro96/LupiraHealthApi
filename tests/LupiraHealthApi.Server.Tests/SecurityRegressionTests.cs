using System.Net;
using System.Net.Http.Json;
using LupiraHealthApi.Dtos.Devices;
using LupiraHealthApi.Dtos.Location;
using LupiraHealthApi.Dtos.Ring;
using Xunit;

namespace LupiraHealthApi.Server.Tests;

/// <summary>Principal isolation: a user can only see their own telemetry, and can only register/list devices on a
/// record they own. Telemetry queries hard-filter principal_id = caller; record-scoped device ops are ownership-checked.</summary>
public sealed class SecurityRegressionTests(HealthApiTestFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task UserB_cannot_see_user_As_location()
    {
        var a = Factory.ApiClient("a@x.test");
        var recA = await BootstrapAsync(a);
        var regA = await RegisterDeviceAsync(a, recA.Id);
        var keyA = Factory.DeviceKeyClient(regA.ApiKey);

        var now = DateTimeOffset.UtcNow;
        await IngestLocationAsync(keyA, [Fix(1, now.AddMinutes(-2), 59.30, 18.00), Fix(2, now.AddMinutes(-1), 59.31, 18.01)]);

        Assert.Single((await a.GetFromJsonAsync<List<CurrentFixDto>>("/api/location/current"))!);

        var b = Factory.ApiClient("b@x.test");
        await BootstrapAsync(b);
        Assert.Empty((await b.GetFromJsonAsync<List<CurrentFixDto>>("/api/location/current"))!);
        Assert.Empty((await b.GetFromJsonAsync<List<TrackPointDto>>($"/api/location/track?from={Q(now.AddHours(-1))}&to={Q(now.AddMinutes(1))}"))!);
    }

    [Fact]
    public async Task UserB_cannot_see_user_As_ring_data()
    {
        var a = Factory.ApiClient("a@x.test");
        var recA = await BootstrapAsync(a);
        var regA = await RegisterDeviceAsync(a, recA.Id, "SmartRing");
        var now = DateTimeOffset.UtcNow;
        await IngestRingAsync(Factory.DeviceKeyClient(regA.ApiKey), [RingSample(1, "hr", now.AddMinutes(-1), 62)]);

        var b = Factory.ApiClient("b@x.test");
        await BootstrapAsync(b);
        Assert.Empty((await b.GetFromJsonAsync<List<RingBucketDto>>($"/api/health/ring?kind=hr&from={Q(now.AddHours(-1))}&to={Q(now.AddMinutes(1))}"))!);
    }

    [Fact]
    public async Task Foreign_device_id_in_query_returns_empty_not_others_data()
    {
        var a = Factory.ApiClient("a@x.test");
        var recA = await BootstrapAsync(a);
        var regA = await RegisterDeviceAsync(a, recA.Id);
        var now = DateTimeOffset.UtcNow;
        await IngestLocationAsync(Factory.DeviceKeyClient(regA.ApiKey), [Fix(1, now.AddMinutes(-1), 59.30, 18.00)]);

        // B passes A's real device id — the principal_id hard-filter means it still matches nothing.
        var b = Factory.ApiClient("b@x.test");
        await BootstrapAsync(b);
        Assert.Empty((await b.GetFromJsonAsync<List<CurrentFixDto>>($"/api/location/current?deviceId={regA.Device.Id}"))!);
    }

    [Fact]
    public async Task UserB_cannot_list_or_register_devices_on_user_As_record()
    {
        var a = Factory.ApiClient("a@x.test");
        var recA = await BootstrapAsync(a);
        await RegisterDeviceAsync(a, recA.Id);

        var b = Factory.ApiClient("b@x.test");
        await BootstrapAsync(b);

        Assert.Equal(HttpStatusCode.Forbidden, (await b.GetAsync($"/api/devices?recordId={recA.Id}")).StatusCode);
        var forge = await b.PostAsJsonAsync("/api/devices", new RegisterDeviceRequest(recA.Id, "Phone", "Hijack", null));
        Assert.Equal(HttpStatusCode.Forbidden, forge.StatusCode);
    }
}
