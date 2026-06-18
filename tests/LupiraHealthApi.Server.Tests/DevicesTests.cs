using System.Net;
using System.Net.Http.Json;
using LupiraHealthApi.Dtos.Devices;
using Xunit;

namespace LupiraHealthApi.Server.Tests;

public sealed class DevicesTests(HealthApiTestFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Register_then_list_rename_retire_lifecycle()
    {
        var api = Factory.ApiClient("alice@x.test");
        var record = await BootstrapAsync(api);

        var reg = await RegisterDeviceAsync(api, record.Id, "SmartRing", "Oura");
        Assert.False(string.IsNullOrWhiteSpace(reg.ApiKey));
        Assert.Contains('.', reg.ApiKey);
        Assert.Equal("SmartRing", reg.Device.Kind);

        Assert.Single((await api.GetFromJsonAsync<List<DeviceDto>>($"/api/devices?recordId={record.Id}"))!);

        var rename = await api.PutAsJsonAsync($"/api/devices/{reg.Device.Id}", new RenameDeviceRequest("Oura Ring 4"));
        rename.EnsureSuccessStatusCode();
        Assert.Equal("Oura Ring 4", (await rename.Content.ReadFromJsonAsync<DeviceDto>())!.Label);

        Assert.Equal(HttpStatusCode.NoContent, (await api.DeleteAsync($"/api/devices/{reg.Device.Id}")).StatusCode);
    }

    [Fact]
    public async Task Register_with_empty_label_is_400()
    {
        var api = Factory.ApiClient("alice@x.test");
        var record = await BootstrapAsync(api);
        var resp = await api.PostAsJsonAsync("/api/devices", new RegisterDeviceRequest(record.Id, "Phone", "  ", null));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Register_with_unknown_kind_is_400()
    {
        var api = Factory.ApiClient("alice@x.test");
        var record = await BootstrapAsync(api);
        var resp = await api.PostAsJsonAsync("/api/devices", new RegisterDeviceRequest(record.Id, "Toaster", "Kitchen", null));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Register_without_write_access_is_403()
    {
        var a = Factory.ApiClient("a@x.test");
        var recA = await BootstrapAsync(a);

        var b = Factory.ApiClient("b@x.test");
        await BootstrapAsync(b);
        var resp = await b.PostAsJsonAsync("/api/devices", new RegisterDeviceRequest(recA.Id, "Phone", "Forged", null));
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task List_devices_without_read_access_is_403()
    {
        var a = Factory.ApiClient("a@x.test");
        var recA = await BootstrapAsync(a);

        var b = Factory.ApiClient("b@x.test");
        await BootstrapAsync(b);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.GetAsync($"/api/devices?recordId={recA.Id}")).StatusCode);
    }

    [Fact]
    public async Task Rename_missing_device_is_404()
    {
        var api = Factory.ApiClient("alice@x.test");
        await BootstrapAsync(api);
        var resp = await api.PutAsJsonAsync($"/api/devices/{Guid.NewGuid()}", new RenameDeviceRequest("X"));
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Rename_with_empty_label_is_400()
    {
        var api = Factory.ApiClient("alice@x.test");
        var record = await BootstrapAsync(api);
        var reg = await RegisterDeviceAsync(api, record.Id);
        var resp = await api.PutAsJsonAsync($"/api/devices/{reg.Device.Id}", new RenameDeviceRequest("  "));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Retire_missing_device_is_404()
    {
        var api = Factory.ApiClient("alice@x.test");
        await BootstrapAsync(api);
        Assert.Equal(HttpStatusCode.NotFound, (await api.DeleteAsync($"/api/devices/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Retire_twice_is_404_on_second()
    {
        var api = Factory.ApiClient("alice@x.test");
        var record = await BootstrapAsync(api);
        var reg = await RegisterDeviceAsync(api, record.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await api.DeleteAsync($"/api/devices/{reg.Device.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.DeleteAsync($"/api/devices/{reg.Device.Id}")).StatusCode);
    }

    [Fact]
    public async Task Retired_device_key_can_no_longer_ingest()
    {
        var api = Factory.ApiClient("alice@x.test");
        var record = await BootstrapAsync(api);
        var reg = await RegisterDeviceAsync(api, record.Id);
        var key = Factory.DeviceKeyClient(reg.ApiKey);

        Assert.Equal(HttpStatusCode.Accepted, (await PostNdjson(key, "/api/ingest/location", [Fix(1, DateTimeOffset.UtcNow.AddMinutes(-1), 59.3, 18.0)])).StatusCode);
        await api.DeleteAsync($"/api/devices/{reg.Device.Id}");
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostNdjson(key, "/api/ingest/location", [Fix(2, DateTimeOffset.UtcNow, 59.3, 18.0)])).StatusCode);
    }
}
