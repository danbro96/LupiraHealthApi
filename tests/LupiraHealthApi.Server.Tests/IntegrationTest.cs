using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using LupiraHealthApi.Application.Telemetry;
using LupiraHealthApi.Dtos.Devices;
using LupiraHealthApi.Dtos.Location;
using LupiraHealthApi.Dtos.Me;
using LupiraHealthApi.Dtos.Records;
using LupiraHealthApi.Dtos.Ring;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LupiraHealthApi.Server.Tests;

/// <summary>Base for integration tests: shares the container fixture, resets all state before each test, and provides
/// REST + NDJSON-ingest helpers. Serial within the "integration" collection.</summary>
[Collection("integration")]
public abstract class IntegrationTest(HealthApiTestFactory factory) : IAsyncLifetime
{
    protected readonly HealthApiTestFactory Factory = factory;

    public async Task InitializeAsync() => await Factory.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ---- REST fixture helpers ----

    protected static async Task<MeDto> GetMeAsync(HttpClient api) => (await api.GetFromJsonAsync<MeDto>("/api/me"))!;
    protected static async Task<Guid> GetMyIdAsync(HttpClient api) => (await GetMeAsync(api)).Id;

    protected static async Task<HealthRecordDto> BootstrapAsync(HttpClient api)
    {
        var resp = await api.PostAsync("/api/me/bootstrap", null);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<HealthRecordDto>())!;
    }

    protected static async Task<Guid> CreateRecordAsync(HttpClient api, string slug = "personal")
    {
        var resp = await api.PostAsJsonAsync("/api/records", new CreateHealthRecordRequest(slug, slug));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<HealthRecordDto>())!.Id;
    }

    protected static async Task<RegisterDeviceResponse> RegisterDeviceAsync(HttpClient api, Guid recordId, string kind = "Phone", string label = "My Phone")
    {
        var resp = await api.PostAsJsonAsync("/api/devices", new RegisterDeviceRequest(recordId, kind, label, null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<RegisterDeviceResponse>())!;
    }

    /// <summary>Bootstraps the caller, registers a device, and returns its principal id + ingest-key client.</summary>
    protected async Task<(Guid Pid, HttpClient Key, Guid DeviceId)> SetupDeviceAsync(HttpClient api, string kind = "Phone")
    {
        var pid = await GetMyIdAsync(api);
        var record = await BootstrapAsync(api);
        var reg = await RegisterDeviceAsync(api, record.Id, kind);
        return (pid, Factory.DeviceKeyClient(reg.ApiKey), reg.Device.Id);
    }

    // ---- NDJSON ingest helpers ----

    protected static Task<HttpResponseMessage> PostNdjson(HttpClient client, string url, IEnumerable<string> lines)
    {
        var content = new StringContent(string.Join('\n', lines), Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-ndjson");
        return client.PostAsync(url, content);
    }

    protected static async Task<LocationIngestReceipt> IngestLocationAsync(HttpClient key, IEnumerable<string> lines) =>
        (await (await PostNdjson(key, "/api/ingest/location", lines)).Content.ReadFromJsonAsync<LocationIngestReceipt>())!;

    protected static async Task<RingIngestReceipt> IngestRingAsync(HttpClient key, IEnumerable<string> lines) =>
        (await (await PostNdjson(key, "/api/ingest/ring", lines)).Content.ReadFromJsonAsync<RingIngestReceipt>())!;

    protected static async Task<RingIngestReceipt> IngestSummariesAsync(HttpClient key, IEnumerable<string> lines) =>
        (await (await PostNdjson(key, "/api/ingest/summaries", lines)).Content.ReadFromJsonAsync<RingIngestReceipt>())!;

    /// <summary>Triggers the rollup directly (the maintenance BackgroundService is disabled in tests).</summary>
    protected async Task RollupAsync(Guid pid, Guid deviceId, DateOnly day)
    {
        using var scope = Factory.Services.CreateScope();
        var trips = scope.ServiceProvider.GetRequiredService<TripVisitService>();
        await trips.RollupDayAsync(pid, deviceId, day);
    }

    // ---- payload builders ----

    /// <summary>Builds one NDJSON location-fix line (doubles formatted invariant).</summary>
    protected static string Fix(long seq, DateTimeOffset ts, double lat, double lon, double accuracy = 5, double? speed = null,
        string provider = "gps", string activity = "walk", bool isMock = false)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"{{\"seq\":{seq},\"ts\":\"{ts:O}\",\"lat\":{lat.ToString(CultureInfo.InvariantCulture)},\"lon\":{lon.ToString(CultureInfo.InvariantCulture)}");
        sb.Append(CultureInfo.InvariantCulture, $",\"accuracy_m\":{accuracy.ToString(CultureInfo.InvariantCulture)}");
        if (speed is not null) sb.Append(CultureInfo.InvariantCulture, $",\"speed_mps\":{speed.Value.ToString(CultureInfo.InvariantCulture)}");
        sb.Append(CultureInfo.InvariantCulture, $",\"provider\":\"{provider}\",\"activity\":\"{activity}\"");
        if (isMock) sb.Append(",\"is_mock\":true");
        sb.Append('}');
        return sb.ToString();
    }

    protected static string RingSample(long seq, string kind, DateTimeOffset ts, double value) =>
        string.Create(CultureInfo.InvariantCulture, $"{{\"seq\":{seq},\"kind\":\"{kind}\",\"ts\":\"{ts:O}\",\"value\":{value.ToString(CultureInfo.InvariantCulture)}}}");

    protected static string DeviceSummary(long seq, int kind, DateTimeOffset periodStart, DateTimeOffset periodEnd, string payloadJson) =>
        string.Create(CultureInfo.InvariantCulture, $"{{\"seq\":{seq},\"kind\":{kind},\"periodStart\":\"{periodStart:O}\",\"periodEnd\":\"{periodEnd:O}\",\"payload\":{payloadJson}}}");

    /// <summary>ISO-8601 query-string escaper for from/to params.</summary>
    protected static string Q(DateTimeOffset ts) => Uri.EscapeDataString(ts.ToString("O"));

    /// <summary>A safe in-the-past day anchor that never crosses midnight or lands in the future regardless of run time.</summary>
    protected static (DateOnly Day, DateTimeOffset Base) SafeDay()
    {
        var now = DateTimeOffset.UtcNow;
        var day = now.Hour >= 2 ? DateOnly.FromDateTime(now.UtcDateTime) : DateOnly.FromDateTime(now.UtcDateTime).AddDays(-1);
        return (day, new DateTimeOffset(day.Year, day.Month, day.Day, 0, 30, 0, TimeSpan.Zero));
    }
}
