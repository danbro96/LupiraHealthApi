using System.Net;
using Xunit;

namespace LupiraHealthApi.Server.Tests;

/// <summary>The MCP transport is OIDC-gated (ApiPolicy) and LAN-only: a request bearing Cloudflare edge headers is
/// 404'd by the backstop before auth runs, so an ingress mistake never exposes it through the tunnel.</summary>
public sealed class McpEndpointTests(HealthApiTestFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Mcp_requires_authentication()
    {
        var resp = await Factory.AnonymousClient().PostAsync("/mcp", null);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Mcp_is_unreachable_through_the_cloudflare_tunnel()
    {
        var client = Factory.AnonymousClient();
        client.DefaultRequestHeaders.Add("CF-Ray", "test-ray");
        var resp = await client.PostAsync("/mcp", null);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
