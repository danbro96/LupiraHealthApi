using Lupira.Testing.Mcp;
using Lupira.Testing.Postgres;
using Xunit;

namespace LupiraHealthApi.IntegrationTests;

[Collection("integration")]
public sealed class McpAuthDiscoveryTests(HealthApiTestFactory factory) : McpResourceMetadataTests
{
    protected override string Issuer => factory.Authority!;

    protected override HttpMethod McpProbeMethod => HttpMethod.Post;

    protected override IReadOnlyList<string> TunnelledPaths =>
        ["/.well-known/oauth-protected-resource", "/.well-known/oauth-protected-resource/mcp"];

    public override Task InitializeAsync() => factory.ResetAsync();

    protected override HttpClient CreateAnonymousClient() => factory.AnonymousClient();
}
