using System.Net;
using System.Net.Http.Json;
using LupiraHealthApi.Dtos.Records;
using Xunit;

namespace LupiraHealthApi.IntegrationTests;

/// <summary>Cross-cutting authentication: unauthenticated requests are rejected, and each surface only accepts its own
/// auth scheme (OIDC for the REST surface, device key for /ingest).</summary>
public sealed class AccessTests(HealthApiTestFactory factory) : IntegrationTest(factory)
{
    [Theory]
    [InlineData("/me")]
    [InlineData("/records")]
    [InlineData("/devices?recordId=00000000-0000-0000-0000-000000000000")]
    [InlineData("/health/ring?kind=hr")]
    public async Task Unauthenticated_reads_are_rejected(string url)
    {
        var anon = Factory.AnonymousClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_writes_are_rejected()
    {
        var anon = Factory.AnonymousClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/records", new CreateHealthRecordRequest { Slug = "x", DisplayName = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync("/me/bootstrap", null)).StatusCode);
    }

    [Fact]
    public async Task Ingest_requires_a_device_key_not_an_api_token()
    {
        // An OIDC/dev-authed client (ApiPolicy) must not be able to hit the device-key-only ingest surface.
        var api = Factory.ApiClient("alice@x.test");
        await BootstrapAsync(api);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostNdjson(api, "/ingest/ring", [RingSample(1, "hr", DateTimeOffset.UtcNow.AddMinutes(-1), 60)])).StatusCode);
    }

    [Fact]
    public async Task Ingest_with_malformed_or_unknown_key_is_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostNdjson(Factory.DeviceKeyClient("garbage"), "/ingest/ring", [RingSample(1, "hr", DateTimeOffset.UtcNow, 60)])).StatusCode);
        var wellFormedUnknown = $"{Guid.NewGuid():N}.{new string('a', 64)}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostNdjson(Factory.DeviceKeyClient(wellFormedUnknown), "/ingest/ring", [RingSample(1, "hr", DateTimeOffset.UtcNow, 60)])).StatusCode);
    }

    [Fact]
    public async Task Api_endpoints_reject_a_device_key()
    {
        var api = Factory.ApiClient("alice@x.test");
        var record = await BootstrapAsync(api);
        var reg = await RegisterDeviceAsync(api, record.Id);
        var key = Factory.DeviceKeyClient(reg.ApiKey);
        Assert.Equal(HttpStatusCode.Unauthorized, (await key.GetAsync("/health/ring?kind=hr")).StatusCode);
    }
}
