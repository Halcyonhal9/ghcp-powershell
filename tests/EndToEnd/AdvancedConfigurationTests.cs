#pragma warning disable GHCP001 // experimental SDK surfaces covered by issue #28
using System.Management.Automation;
using GitHub.Copilot;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.EndToEnd;

[Trait("Category", "EndToEnd")]
[Collection("EndToEnd")]
public class AdvancedConfigurationTests : IAsyncLifetime
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
    public void NewCopilotSession_AcceptsRemainingSharedConfiguration()
    {
        var sessionId = $"advanced-config-{Guid.NewGuid():N}";
        sessionIds.Add(sessionId);
        var customAgent = new CustomAgentConfig
        {
            Name = "advanced-agent",
            DisplayName = "Advanced Agent",
            Prompt = "Reply concisely.",
            Tools = []
        };
        var defaultAgent = new DefaultAgentConfig { ExcludedTools = ["builtin:bash"] };
        var infiniteSessions = new InfiniteSessionConfig
        {
            Enabled = false,
            BackgroundCompactionThreshold = 0.7,
            BufferExhaustionThreshold = 0.9
        };
        var largeOutput = new LargeToolOutputConfig { Enabled = false };
        var memory = new MemoryConfiguration { Enabled = false };
        var hooks = new SessionHooks();
        var command = new CommandDefinition
        {
            Name = "advanced",
            Description = "Advanced configuration smoke command",
            Handler = _ => Task.CompletedTask
        };
        Func<McpAuthContext, Task<McpAuthResult?>> mcpAuth = _ =>
            Task.FromResult<McpAuthResult?>(McpAuthResult.Cancel());
        Func<ElicitationContext, Task<ElicitationResult>> elicitation = _ =>
            Task.FromResult<ElicitationResult>(null!);
        Func<ExitPlanModeRequest, ExitPlanModeInvocation, Task<ExitPlanModeResult>> exitPlan =
            (_, _) => Task.FromResult<ExitPlanModeResult>(null!);
        Func<AutoModeSwitchRequest, AutoModeSwitchInvocation, Task<AutoModeSwitchResponse>> autoMode =
            (_, _) => Task.FromResult(AutoModeSwitchResponse.No);

        ps.AddCommand("New-CopilotSession")
            .AddParameter("SessionId", sessionId)
            .AddParameter("AvailableTools", Array.Empty<string>())
            .AddParameter("CustomAgents", new[] { customAgent })
            .AddParameter("DefaultAgent", defaultAgent)
            .AddParameter("CustomAgentsLocalOnly", true)
            .AddParameter("InfiniteSessionConfig", infiniteSessions)
            .AddParameter("LargeOutput", largeOutput)
            .AddParameter("Memory", memory)
            .AddParameter("McpOAuthTokenStorage", McpOAuthTokenStorageMode.InMemory)
            .AddParameter("Hooks", hooks)
            .AddParameter("Commands", new[] { command })
            .AddParameter("OnMcpAuthRequestDelegate", mcpAuth)
            .AddParameter("OnElicitationRequestDelegate", elicitation)
            .AddParameter("OnExitPlanModeRequestDelegate", exitPlan)
            .AddParameter("OnAutoModeSwitchRequestDelegate", autoMode)
            .AddParameter("RemoteSession", "off");

        var results = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        var session = Assert.IsType<CopilotSession>(Assert.Single(results).BaseObject);
        Assert.Equal(sessionId, session.SessionId);
    }

    [Fact]
    public void NewCopilotSession_AcceptsSingularProviderConfiguration()
    {
        var sessionId = $"provider-config-{Guid.NewGuid():N}";
        sessionIds.Add(sessionId);
        var provider = new ProviderConfig
        {
            Type = "openai",
            WireApi = "completions",
            BaseUrl = "http://127.0.0.1:1/v1",
            ModelId = "fixture-model"
        };

        ps.AddCommand("New-CopilotSession")
            .AddParameter("SessionId", sessionId)
            .AddParameter("Provider", provider)
            .AddParameter("AvailableTools", Array.Empty<string>());

        var results = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        Assert.IsType<CopilotSession>(Assert.Single(results).BaseObject);
    }

    [Fact]
    public void NewCopilotSession_AcceptsNamedProviderRegistry()
    {
        var sessionId = $"provider-registry-{Guid.NewGuid():N}";
        sessionIds.Add(sessionId);
        var providers = new[]
        {
            new NamedProviderConfig
            {
                Name = "fixture",
                Type = "openai",
                WireApi = "completions",
                BaseUrl = "http://127.0.0.1:1/v1"
            }
        };
        var models = new[]
        {
            new ProviderModelConfig
            {
                Id = "model",
                Provider = "fixture",
                WireModel = "fixture-model"
            }
        };

        ps.AddCommand("New-CopilotSession")
            .AddParameter("SessionId", sessionId)
            .AddParameter("Providers", providers)
            .AddParameter("ProviderModels", models)
            .AddParameter("AvailableTools", Array.Empty<string>());

        var results = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        Assert.IsType<CopilotSession>(Assert.Single(results).BaseObject);
    }

    [Fact]
    public void SendCopilotMessage_AgentModePlanInvokesExitPlanHandler()
    {
        var sessionId = $"plan-mode-{Guid.NewGuid():N}";
        sessionIds.Add(sessionId);
        ps.AddScript(
            $$"""
            $null = New-CopilotSession -SessionId '{{sessionId}}' -AutoApprove -MaxAiCredits 50 -OnExitPlanModeRequest {
                param($request, $invocation)
                [GitHub.Copilot.ExitPlanModeResult]@{
                    Approved = $true
                    SelectedAction = 'exit_only'
                    Feedback = 'Approved by the PowerShell E2E test'
                }
            }
            Send-CopilotMessage `
                -Prompt 'Create a brief plan for adding a greeting.txt file, then request approval with exit_plan_mode.' `
                -AgentMode Plan `
                -Timeout (New-TimeSpan -Seconds 120)
            """);
        var results = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        var result = Assert.IsType<CopilotMessageResult>(results[^1].BaseObject);
        Assert.Contains(result.Events, item => item is ExitPlanModeRequestedEvent);
        Assert.Contains(result.Events, item => item is ExitPlanModeCompletedEvent);
    }

    [Fact]
    public void ResumeCopilotSession_AcceptsAdvancedConfiguration()
    {
        var sessionId = $"advanced-resume-{Guid.NewGuid():N}";
        sessionIds.Add(sessionId);
        var customAgent = new CustomAgentConfig
        {
            Name = "resume-agent",
            Prompt = "Reply concisely.",
            Tools = []
        };

        try
        {
            ps.AddCommand("New-CopilotSession")
                .AddParameter("SessionId", sessionId)
                .AddParameter("AvailableTools", Array.Empty<string>())
                .AddParameter("MaxAiCredits", 50);
            ps.Invoke();
            Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));

            ps.Commands.Clear();
            ps.AddCommand("Send-CopilotMessage")
                .AddParameter("Prompt", "Reply exactly: resume-seed")
                .AddParameter("Timeout", TimeSpan.FromSeconds(60));
            ps.Invoke();
            Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));

            ps.Commands.Clear();
            ps.AddCommand("Close-CopilotSession");
            ps.Invoke();
            Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));

            ps.Commands.Clear();
            ps.AddCommand("Resume-CopilotSession")
                .AddParameter("SessionId", sessionId)
                .AddParameter("CustomAgents", new[] { customAgent })
                .AddParameter("CustomAgentsLocalOnly", true)
                .AddParameter("InfiniteSessionConfig", new InfiniteSessionConfig { Enabled = false })
                .AddParameter("LargeOutput", new LargeToolOutputConfig { Enabled = false })
                .AddParameter("Memory", new MemoryConfiguration { Enabled = false })
                .AddParameter("McpOAuthTokenStorage", McpOAuthTokenStorageMode.InMemory)
                .AddParameter("RemoteSession", "off");
            var results = ps.Invoke();

            Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
            var session = Assert.IsType<CopilotSession>(Assert.Single(results).BaseObject);
            Assert.Equal(sessionId, session.SessionId);
        }
        finally
        {
            ps.Commands.Clear();
            ps.AddCommand("Close-CopilotSession");
            ps.Invoke();

            ps.Commands.Clear();
            ps.AddCommand("Remove-CopilotSession")
                .AddParameter("SessionId", sessionId)
                .AddParameter("Confirm", false);
            ps.Invoke();
        }
    }
}
