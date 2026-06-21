using System.Net;
using System.Net.Http.Json;
using LupiraHealthApi.Dtos.Records;
using Xunit;

namespace LupiraHealthApi.Server.Tests;

/// <summary>Generic REST surface: identity (/api/me) + health-record container CRUD.</summary>
public sealed class RestEndpointsTests(HealthApiTestFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Me_returns_the_dev_user()
    {
        var api = Factory.ApiClient("alice@x.test");
        var me = await GetMeAsync(api);
        Assert.Equal("alice@x.test", me.Email);
        Assert.NotEqual(Guid.Empty, me.Id);
    }

    [Fact]
    public async Task Bootstrap_creates_personal_record_and_is_idempotent()
    {
        var api = Factory.ApiClient("alice@x.test");
        var first = await BootstrapAsync(api);
        var second = await BootstrapAsync(api);

        Assert.Equal("personal", first.Slug);
        Assert.Equal(first.Id, second.Id);
        Assert.Single((await api.GetFromJsonAsync<List<HealthRecordDto>>("/api/records"))!);
    }

    [Fact]
    public async Task Create_record_then_listed()
    {
        var api = Factory.ApiClient("alice@x.test");
        var resp = await api.PostAsJsonAsync("/api/records", new CreateHealthRecordRequest { Slug = "travel", DisplayName = "Travel Health" });
        resp.EnsureSuccessStatusCode();
        var created = await resp.Content.ReadFromJsonAsync<HealthRecordDto>();

        var list = await api.GetFromJsonAsync<List<HealthRecordDto>>("/api/records");
        Assert.Contains(list!, r => r.Id == created!.Id && r.Slug == "travel");
    }

    [Fact]
    public async Task Create_record_with_empty_slug_is_400()
    {
        var api = Factory.ApiClient("alice@x.test");
        var resp = await api.PostAsJsonAsync("/api/records", new CreateHealthRecordRequest { Slug = "   ", DisplayName = "Bad" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Records_are_isolated_per_user()
    {
        var a = Factory.ApiClient("a@x.test");
        var recA = await BootstrapAsync(a);

        var b = Factory.ApiClient("b@x.test");
        await BootstrapAsync(b);
        var bList = await b.GetFromJsonAsync<List<HealthRecordDto>>("/api/records");
        Assert.DoesNotContain(bList!, r => r.Id == recA.Id);
    }
}
