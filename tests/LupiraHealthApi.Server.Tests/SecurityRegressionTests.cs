using System.Net;
using System.Net.Http.Json;
using LupiraHealthApi.Domain;
using LupiraHealthApi.Dtos.Devices;
using LupiraHealthApi.Dtos.Ring;
using Xunit;

namespace LupiraHealthApi.Server.Tests;

/// <summary>Principal isolation: a user can only see their own telemetry, and can only register/list devices on a
/// record they own. Ring queries hard-filter principal_id = caller; record-scoped device ops are ownership-checked.</summary>
public sealed class SecurityRegressionTests(HealthApiTestFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task UserB_cannot_see_user_As_ring_data()
    {
        var a = Factory.ApiClient("a@x.test");
        var recA = await BootstrapAsync(a);
        var regA = await RegisterDeviceAsync(a, recA.Id, DeviceKind.SmartRing);
        var now = DateTimeOffset.UtcNow;
        await IngestRingAsync(Factory.DeviceKeyClient(regA.ApiKey), [RingSample(1, "hr", now.AddMinutes(-1), 62)]);

        var b = Factory.ApiClient("b@x.test");
        await BootstrapAsync(b);
        Assert.Empty((await b.GetFromJsonAsync<List<RingBucketDto>>($"/health/ring?kind=hr&from={Q(now.AddHours(-1))}&to={Q(now.AddMinutes(1))}"))!);
    }

    [Fact]
    public async Task UserB_cannot_list_or_register_devices_on_user_As_record()
    {
        var a = Factory.ApiClient("a@x.test");
        var recA = await BootstrapAsync(a);
        await RegisterDeviceAsync(a, recA.Id);

        var b = Factory.ApiClient("b@x.test");
        await BootstrapAsync(b);

        Assert.Equal(HttpStatusCode.Forbidden, (await b.GetAsync($"/devices?recordId={recA.Id}")).StatusCode);
        var forge = await b.PostAsJsonAsync("/devices", new RegisterDeviceRequest { HealthRecordId = recA.Id, Kind = DeviceKind.SmartRing, Label = "Hijack" });
        Assert.Equal(HttpStatusCode.Forbidden, forge.StatusCode);
    }
}
