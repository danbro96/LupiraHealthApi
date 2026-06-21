using System.Net;
using System.Net.Http.Json;
using System.Text;
using LupiraHealthApi.Domain;
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

        var reg = await RegisterDeviceAsync(api, record.Id, DeviceKind.SmartRing, "Oura");
        Assert.False(string.IsNullOrWhiteSpace(reg.ApiKey));
        Assert.Contains('.', reg.ApiKey);
        Assert.Equal(DeviceKind.SmartRing, reg.Device.Kind);

        Assert.Single((await api.GetFromJsonAsync<List<DeviceDto>>($"/devices?recordId={record.Id}"))!);

        var rename = await api.PutAsJsonAsync($"/devices/{reg.Device.Id}", new RenameDeviceRequest { Label = "Oura Ring 4" });
        rename.EnsureSuccessStatusCode();
        Assert.Equal("Oura Ring 4", (await rename.Content.ReadFromJsonAsync<DeviceDto>())!.Label);

        Assert.Equal(HttpStatusCode.NoContent, (await api.DeleteAsync($"/devices/{reg.Device.Id}")).StatusCode);
    }

    [Fact]
    public async Task Register_with_empty_label_is_400()
    {
        var api = Factory.ApiClient("alice@x.test");
        var record = await BootstrapAsync(api);
        var resp = await api.PostAsJsonAsync("/devices", new RegisterDeviceRequest { HealthRecordId = record.Id, Kind = DeviceKind.Phone, Label = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Register_with_unknown_kind_is_400()
    {
        var api = Factory.ApiClient("alice@x.test");
        var record = await BootstrapAsync(api);
        // Unknown DeviceKind name is rejected at deserialization (JsonStringEnumConverter).
        var json = $$"""{"healthRecordId":"{{record.Id}}","kind":"Toaster","label":"Kitchen"}""";
        var resp = await api.PostAsync("/devices", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Register_without_write_access_is_403()
    {
        var a = Factory.ApiClient("a@x.test");
        var recA = await BootstrapAsync(a);

        var b = Factory.ApiClient("b@x.test");
        await BootstrapAsync(b);
        var resp = await b.PostAsJsonAsync("/devices", new RegisterDeviceRequest { HealthRecordId = recA.Id, Kind = DeviceKind.Phone, Label = "Forged" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task List_devices_without_read_access_is_403()
    {
        var a = Factory.ApiClient("a@x.test");
        var recA = await BootstrapAsync(a);

        var b = Factory.ApiClient("b@x.test");
        await BootstrapAsync(b);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.GetAsync($"/devices?recordId={recA.Id}")).StatusCode);
    }

    [Fact]
    public async Task Rename_missing_device_is_404()
    {
        var api = Factory.ApiClient("alice@x.test");
        await BootstrapAsync(api);
        var resp = await api.PutAsJsonAsync($"/devices/{Guid.NewGuid()}", new RenameDeviceRequest { Label = "X" });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Rename_with_empty_label_is_400()
    {
        var api = Factory.ApiClient("alice@x.test");
        var record = await BootstrapAsync(api);
        var reg = await RegisterDeviceAsync(api, record.Id);
        var resp = await api.PutAsJsonAsync($"/devices/{reg.Device.Id}", new RenameDeviceRequest { Label = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Retire_missing_device_is_404()
    {
        var api = Factory.ApiClient("alice@x.test");
        await BootstrapAsync(api);
        Assert.Equal(HttpStatusCode.NotFound, (await api.DeleteAsync($"/devices/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Retire_twice_is_404_on_second()
    {
        var api = Factory.ApiClient("alice@x.test");
        var record = await BootstrapAsync(api);
        var reg = await RegisterDeviceAsync(api, record.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await api.DeleteAsync($"/devices/{reg.Device.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.DeleteAsync($"/devices/{reg.Device.Id}")).StatusCode);
    }

    [Fact]
    public async Task Retired_device_key_can_no_longer_ingest()
    {
        var api = Factory.ApiClient("alice@x.test");
        var record = await BootstrapAsync(api);
        var reg = await RegisterDeviceAsync(api, record.Id);
        var key = Factory.DeviceKeyClient(reg.ApiKey);

        Assert.Equal(HttpStatusCode.Accepted, (await PostNdjson(key, "/ingest/ring", [RingSample(1, "hr", DateTimeOffset.UtcNow.AddMinutes(-1), 60)])).StatusCode);
        await api.DeleteAsync($"/devices/{reg.Device.Id}");
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostNdjson(key, "/ingest/ring", [RingSample(2, "hr", DateTimeOffset.UtcNow, 61)])).StatusCode);
    }
}
