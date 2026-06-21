using System.Net;
using System.Net.Http.Json;
using LupiraHealthApi.Domain;
using LupiraHealthApi.Dtos.Ring;
using Xunit;

namespace LupiraHealthApi.Server.Tests;

public sealed class RingQueryTests(HealthApiTestFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Downsample_returns_buckets()
    {
        var api = Factory.ApiClient("alice@x.test");
        var (_, key, _) = await SetupDeviceAsync(api);
        var now = DateTimeOffset.UtcNow;
        await IngestRingAsync(key, Enumerable.Range(0, 6).Select(i => RingSample(i + 1, "hr", now.AddMinutes(-6 + i), 60 + i)));

        var buckets = await api.GetFromJsonAsync<List<RingBucketDto>>($"/health/ring?kind=hr&bucketSeconds=600&from={Q(now.AddHours(-1))}&to={Q(now.AddMinutes(1))}");
        Assert.NotNull(buckets);
        Assert.NotEmpty(buckets);
        Assert.Equal(6, buckets.Sum(b => b.Count));
        Assert.InRange(buckets.SelectMany(b => new[] { b.Min, b.Max }).Average(), 60, 70);
    }

    [Fact]
    public async Task Downsample_unknown_metric_is_400()
    {
        var api = Factory.ApiClient("alice@x.test");
        await BootstrapAsync(api);
        var resp = await api.GetAsync($"/health/ring?kind=bloodpressure&from={Q(DateTimeOffset.UtcNow.AddHours(-1))}&to={Q(DateTimeOffset.UtcNow)}");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Downsample_empty_range_is_empty()
    {
        var api = Factory.ApiClient("alice@x.test");
        await SetupDeviceAsync(api);
        var now = DateTimeOffset.UtcNow;
        Assert.Empty((await api.GetFromJsonAsync<List<RingBucketDto>>($"/health/ring?kind=hr&from={Q(now.AddHours(-1))}&to={Q(now)}"))!);
    }

    [Fact]
    public async Task Downsample_filters_by_device()
    {
        var api = Factory.ApiClient("alice@x.test");
        var record = await BootstrapAsync(api);
        var d1 = await RegisterDeviceAsync(api, record.Id, DeviceKind.SmartRing, "Ring1");
        var d2 = await RegisterDeviceAsync(api, record.Id, DeviceKind.SmartRing, "Ring2");
        var now = DateTimeOffset.UtcNow;
        await IngestRingAsync(Factory.DeviceKeyClient(d1.ApiKey), [RingSample(1, "hr", now.AddMinutes(-2), 60)]);
        await IngestRingAsync(Factory.DeviceKeyClient(d2.ApiKey), [RingSample(1, "hr", now.AddMinutes(-1), 90)]);

        var d1Buckets = await api.GetFromJsonAsync<List<RingBucketDto>>($"/health/ring?kind=hr&deviceId={d1.Device.Id}&bucketSeconds=3600&from={Q(now.AddHours(-1))}&to={Q(now.AddMinutes(1))}");
        Assert.Equal(1, d1Buckets!.Sum(b => b.Count));
    }

    [Fact]
    public async Task Summaries_query_returns_ingested_and_filters_by_kind()
    {
        var api = Factory.ApiClient("alice@x.test");
        var (_, key, _) = await SetupDeviceAsync(api);
        var now = DateTimeOffset.UtcNow;
        await IngestSummariesAsync(key,
        [
            DeviceSummary(1, 1, now.AddHours(-8), now.AddHours(-1), "{\"deepMin\":92}"),
            DeviceSummary(2, 2, now.AddHours(-8), now.AddHours(-1), "{\"steps\":8000}"),
        ]);

        var all = await api.GetFromJsonAsync<List<DeviceSummaryDto>>($"/health/summaries?from={Q(now.AddDays(-1))}&to={Q(now.AddMinutes(1))}");
        Assert.Equal(2, all!.Count);

        var sleep = await api.GetFromJsonAsync<List<DeviceSummaryDto>>($"/health/summaries?kind=1&from={Q(now.AddDays(-1))}&to={Q(now.AddMinutes(1))}");
        Assert.Single(sleep!);
        Assert.Equal(1, sleep![0].Kind);
    }

    [Fact]
    public async Task Summaries_empty_is_empty()
    {
        var api = Factory.ApiClient("alice@x.test");
        await SetupDeviceAsync(api);
        var now = DateTimeOffset.UtcNow;
        Assert.Empty((await api.GetFromJsonAsync<List<DeviceSummaryDto>>($"/health/summaries?from={Q(now.AddDays(-1))}&to={Q(now)}"))!);
    }
}
