using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using LupiraHealthApi.Domain;
using LupiraHealthApi.Dtos.Devices;
using LupiraHealthApi.Dtos.Me;
using LupiraHealthApi.Dtos.Records;
using LupiraHealthApi.Dtos.Ring;
using Xunit;

namespace LupiraHealthApi.IntegrationTests;

/// <summary>Base for integration tests: shares the container fixture, resets all state before each test, and provides
/// REST + NDJSON-ingest helpers. Serial within the "integration" collection.</summary>
[Collection("integration")]
public abstract class IntegrationTest(HealthApiTestFactory factory) : IAsyncLifetime
{
    protected readonly HealthApiTestFactory Factory = factory;

    public async Task InitializeAsync() => await Factory.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ---- REST fixture helpers ----

    protected static async Task<MeDto> GetMeAsync(HttpClient api) => (await api.GetFromJsonAsync<MeDto>("/me"))!;
    protected static async Task<Guid> GetMyIdAsync(HttpClient api) => (await GetMeAsync(api)).Id;

    protected static async Task<HealthRecordDto> BootstrapAsync(HttpClient api)
    {
        var resp = await api.PostAsync("/me/bootstrap", null);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<HealthRecordDto>())!;
    }

    protected static async Task<RegisterDeviceResponse> RegisterDeviceAsync(HttpClient api, Guid recordId, DeviceKind kind = DeviceKind.SmartRing, string label = "My Ring")
    {
        var resp = await api.PostAsJsonAsync("/devices", new RegisterDeviceRequest { HealthRecordId = recordId, Kind = kind, Label = label });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<RegisterDeviceResponse>())!;
    }

    /// <summary>Bootstraps the caller, registers a device, and returns its principal id + ingest-key client.</summary>
    protected async Task<(Guid Pid, HttpClient Key, Guid DeviceId)> SetupDeviceAsync(HttpClient api, DeviceKind kind = DeviceKind.SmartRing)
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

    protected static async Task<RingIngestReceipt> IngestRingAsync(HttpClient key, IEnumerable<string> lines) =>
        (await (await PostNdjson(key, "/ingest/ring", lines)).Content.ReadFromJsonAsync<RingIngestReceipt>())!;

    protected static async Task<RingIngestReceipt> IngestSummariesAsync(HttpClient key, IEnumerable<string> lines) =>
        (await (await PostNdjson(key, "/ingest/summaries", lines)).Content.ReadFromJsonAsync<RingIngestReceipt>())!;

    // ---- payload builders ----

    protected static string RingSample(long seq, string kind, DateTimeOffset ts, double value) =>
        string.Create(CultureInfo.InvariantCulture, $"{{\"seq\":{seq},\"kind\":\"{kind}\",\"ts\":\"{ts:O}\",\"value\":{value.ToString(CultureInfo.InvariantCulture)}}}");

    protected static string DeviceSummary(long seq, int kind, DateTimeOffset periodStart, DateTimeOffset periodEnd, string payloadJson) =>
        string.Create(CultureInfo.InvariantCulture, $"{{\"seq\":{seq},\"kind\":{kind},\"periodStart\":\"{periodStart:O}\",\"periodEnd\":\"{periodEnd:O}\",\"payload\":{payloadJson}}}");

    /// <summary>ISO-8601 query-string escaper for from/to params.</summary>
    protected static string Q(DateTimeOffset ts) => Uri.EscapeDataString(ts.ToString("O"));
}
