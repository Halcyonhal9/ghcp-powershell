#pragma warning disable GHCP001 // experimental SDK surfaces covered by issue #28
using System.Collections;
using System.Management.Automation;
using GitHub.Copilot;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.EndToEnd;

[Trait("Category", "EndToEnd")]
[Collection("EndToEnd")]
public class ProviderAndOAuthTests : IAsyncLifetime
{
    private readonly List<string> sessionIds = [];
    private PowerShell ps = null!;

    public Task InitializeAsync()
    {
        ps = PowerShell.Create();
        ps.AddCommand("Import-Module").AddParameter("Name", E2eModule.ResolveManifest());
        ps.Invoke();
        ps.Commands.Clear();

        ps.AddCommand("New-CopilotClient");
        ps.Invoke();
        ps.Commands.Clear();

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        try
        {
            ps.Commands.Clear();
            ps.AddCommand("Close-CopilotSession");
            ps.Invoke();
        }
        catch { }

        foreach (var sessionId in sessionIds.Distinct(StringComparer.Ordinal))
        {
            try
            {
                ps.Commands.Clear();
                ps.AddCommand("Remove-CopilotSession")
                    .AddParameter("SessionId", sessionId)
                    .AddParameter("Confirm", false);
                ps.Invoke();
            }
            catch { }
        }

        try
        {
            ps.Commands.Clear();
            ps.AddCommand("Stop-CopilotClient").AddParameter("Force", true);
            ps.Invoke();
        }
        catch { }

        ps.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task NamedProvider_BearerTokenCallbackReachesLoopbackEndpoint()
    {
        const string token = "byok-fixture-token";
        await using var server = OpenAiTestServer.Start();

        ps.AddCommand("New-CopilotNamedProvider")
            .AddParameter("Name", "fixture")
            .AddParameter("BaseUrl", new Uri(server.BaseUri, "v1").ToString().TrimEnd('/'))
            .AddParameter("Type", "openai")
            .AddParameter("WireApi", "completions")
            .AddParameter(
                "BearerTokenProvider",
                ScriptBlock.Create("param($tokenArgs) 'byok-fixture-token'"));
        var providerResults = ps.Invoke();
        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        var provider = Assert.IsType<NamedProviderConfig>(
            Assert.Single(providerResults).BaseObject);
        ps.Commands.Clear();

        var model = new ProviderModelConfig
        {
            Id = "model",
            Provider = "fixture",
            WireModel = "fixture-model"
        };

        var sessionId = $"byok-{Guid.NewGuid():N}";
        sessionIds.Add(sessionId);
        ps.AddCommand("New-CopilotSession")
            .AddParameter("SessionId", sessionId)
            .AddParameter("Providers", new[] { provider })
            .AddParameter("ProviderModels", new[] { model })
            .AddParameter("Model", "fixture/model")
            .AddParameter("AvailableTools", Array.Empty<string>());
        var sessionResults = ps.Invoke();
        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        Assert.IsType<CopilotSession>(Assert.Single(sessionResults).BaseObject);

        ps.Commands.Clear();
        ps.AddCommand("Send-CopilotMessage")
            .AddParameter("Prompt", "Reply with the fixture response.")
            .AddParameter("Timeout", TimeSpan.FromSeconds(60));
        var messageResults = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        var result = Assert.IsType<CopilotMessageResult>(Assert.Single(messageResults).BaseObject);
        Assert.Contains("byok-ok", result.Content);
        Assert.Contains(server.Requests, request =>
            request.Path.EndsWith("/chat/completions", StringComparison.Ordinal) &&
            request.Authorization == $"Bearer {token}");
    }

    [Fact]
    public async Task McpOAuth_HostCallbackConnectsProtectedServer()
    {
        const string token = "mcp-fixture-token";
        const string serverName = "oauth-fixture";
        var sessionId = $"mcp-oauth-{Guid.NewGuid():N}";
        sessionIds.Add(sessionId);
        await using var server = OAuthMcpTestServer.Start(token);
        var handler = ScriptBlock.Create(
            """
            param($context)
            [GitHub.Copilot.McpAuthResult]::FromToken(
                [GitHub.Copilot.McpAuthToken]@{
                    AccessToken = 'mcp-fixture-token'
                    TokenType = 'Bearer'
                    ExpiresIn = 3600
                })
            """);

        ps.AddCommand("New-CopilotSession")
            .AddParameter("SessionId", sessionId)
            .AddParameter("OnMcpAuthRequest", handler)
            .AddParameter("McpOAuthTokenStorage", McpOAuthTokenStorageMode.InMemory)
            .AddParameter("McpServers", new Hashtable
            {
                [serverName] = new Hashtable
                {
                    ["Url"] = server.McpUri.ToString(),
                    ["Tools"] = "*",
                    ["OauthClientId"] = "fixture-client",
                    ["OauthPublicClient"] = true,
                    ["OauthGrantType"] = "AuthorizationCode"
                }
            });
        var sessionResults = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        var session = Assert.IsType<CopilotSession>(Assert.Single(sessionResults).BaseObject);

        ps.Commands.Clear();
        ps.AddCommand("Send-CopilotMessage")
            .AddParameter("Prompt", "Reply exactly: mcp-ready. Do not call tools.")
            .AddParameter("Timeout", TimeSpan.FromSeconds(60));
        ps.Invoke();
        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));

        var foundTool = await WaitForToolAsync(
            session,
            serverName,
            "whoami",
            TimeSpan.FromSeconds(30));

        Assert.True(foundTool);
        Assert.Contains(server.Requests, request => request.Authorization is null);
        Assert.Contains(server.Requests, request => request.Authorization == $"Bearer {token}");
    }

    private static async Task<bool> WaitForToolAsync(
        CopilotSession session,
        string serverName,
        string toolName,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        Exception? lastError = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                var tools = await session.Rpc.Mcp.ListToolsAsync(serverName);
                if (tools.Tools.Any(tool => tool.Name == toolName))
                    return true;
            }
            catch (Exception ex)
            {
                lastError = ex;
                await Task.Delay(200);
            }
        }

        throw new TimeoutException(
            $"MCP server '{serverName}' did not become ready within {timeout}.",
            lastError);
    }
}
