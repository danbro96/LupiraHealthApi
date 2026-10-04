using Lupira.Testing.Mcp;
using Lupira.Testing.Postgres;
using Xunit;

namespace LupiraHealthApi.IntegrationTests;

[Collection("integration")]
public sealed class McpToolArgumentsTests(HealthApiTestFactory factory) : McpStrictArgumentsTests
{
    protected override string DeclaredToolName => "whoami";

    public override Task InitializeAsync() => factory.ResetAsync();

    protected override HttpClient CreateAuthenticatedClient() => factory.ApiClient("alice@x.test");
}
