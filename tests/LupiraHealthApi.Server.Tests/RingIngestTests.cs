using Xunit;

namespace LupiraHealthApi.Server.Tests;

public sealed class RingIngestTests(HealthApiTestFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Ingest_samples_then_dedup()
    {
        var api = Factory.ApiClient("alice@x.test");
        var (_, key, _) = await SetupDeviceAsync(api);
        var now = DateTimeOffset.UtcNow;
        var batch = Enumerable.Range(0, 6).Select(i => RingSample(i + 1, "hr", now.AddMinutes(-6 + i), 60 + i)).ToArray();

        Assert.Equal(6, (await IngestRingAsync(key, batch)).Inserted);
        var second = await IngestRingAsync(key, batch);
        Assert.Equal(0, second.Inserted);
        Assert.Equal(6, second.Duplicates);
    }

    [Fact]
    public async Task Sample_unknown_kind_is_rejected()
    {
        var api = Factory.ApiClient("alice@x.test");
        var (_, key, _) = await SetupDeviceAsync(api);
        Assert.Equal("unknown_kind", (await IngestRingAsync(key, [RingSample(1, "bloodpressure", DateTimeOffset.UtcNow.AddMinutes(-1), 120)])).Rejects[0].Reason);
    }

    [Fact]
    public async Task Sample_missing_seq_is_rejected()
    {
        var api = Factory.ApiClient("alice@x.test");
        var (_, key, _) = await SetupDeviceAsync(api);
        var line = $"{{\"kind\":\"hr\",\"ts\":\"{DateTimeOffset.UtcNow.AddMinutes(-1):O}\",\"value\":60}}";
        Assert.Equal("missing_seq", (await IngestRingAsync(key, [line])).Rejects[0].Reason);
    }

    [Fact]
    public async Task Sample_missing_value_is_rejected()
    {
        var api = Factory.ApiClient("alice@x.test");
        var (_, key, _) = await SetupDeviceAsync(api);
        var line = $"{{\"seq\":1,\"kind\":\"hr\",\"ts\":\"{DateTimeOffset.UtcNow.AddMinutes(-1):O}\"}}";
        Assert.Equal("missing_value", (await IngestRingAsync(key, [line])).Rejects[0].Reason);
    }

    [Fact]
    public async Task Sample_invalid_json_is_rejected()
    {
        var api = Factory.ApiClient("alice@x.test");
        var (_, key, _) = await SetupDeviceAsync(api);
        Assert.Equal("invalid_json", (await IngestRingAsync(key, ["{nope"])).Rejects[0].Reason);
    }

    [Fact]
    public async Task Sample_ts_out_of_range_is_rejected()
    {
        var api = Factory.ApiClient("alice@x.test");
        var (_, key, _) = await SetupDeviceAsync(api);
        Assert.Equal("ts_out_of_range", (await IngestRingAsync(key, [RingSample(1, "hr", DateTimeOffset.UtcNow.AddHours(1), 60)])).Rejects[0].Reason);
    }

    [Fact]
    public async Task Ingest_summaries_then_dedup()
    {
        var api = Factory.ApiClient("alice@x.test");
        var (_, key, _) = await SetupDeviceAsync(api);
        var now = DateTimeOffset.UtcNow;
        var line = DeviceSummary(1, 1, now.AddHours(-8), now.AddHours(-1), "{\"deepMin\":92,\"remMin\":75}");

        Assert.Equal(1, (await IngestSummariesAsync(key, [line])).Inserted);
        Assert.Equal(0, (await IngestSummariesAsync(key, [line])).Inserted);
    }

    [Fact]
    public async Task Summary_missing_kind_is_rejected()
    {
        var api = Factory.ApiClient("alice@x.test");
        var (_, key, _) = await SetupDeviceAsync(api);
        var now = DateTimeOffset.UtcNow;
        var line = $"{{\"seq\":1,\"periodStart\":\"{now.AddHours(-2):O}\",\"periodEnd\":\"{now.AddHours(-1):O}\",\"payload\":{{}}}}";
        Assert.Equal("missing_kind", (await IngestSummariesAsync(key, [line])).Rejects[0].Reason);
    }

    [Fact]
    public async Task Summary_missing_payload_is_rejected()
    {
        var api = Factory.ApiClient("alice@x.test");
        var (_, key, _) = await SetupDeviceAsync(api);
        var now = DateTimeOffset.UtcNow;
        var line = $"{{\"seq\":1,\"kind\":1,\"periodStart\":\"{now.AddHours(-2):O}\",\"periodEnd\":\"{now.AddHours(-1):O}\"}}";
        Assert.Equal("missing_payload", (await IngestSummariesAsync(key, [line])).Rejects[0].Reason);
    }
}
