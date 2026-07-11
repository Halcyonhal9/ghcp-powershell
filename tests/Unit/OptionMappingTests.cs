#pragma warning disable GHCP001 // asserting experimental SDK options (EnableCitations)
using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using GitHub.Copilot;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.Unit;

/// <summary>
/// Behavioral coverage for cmdlet parameter → SDK config mapping. Shape tests
/// elsewhere prove parameters exist; these prove the values actually land on
/// the SDK option objects.
/// </summary>
[Trait("Category", "Unit")]
public class OptionMappingTests
{
    private static NewCopilotSessionCmdlet CreateSessionCmdlet() => new()
    {
        Model = "test-model",
        ReasoningEffort = "high",
        SystemMessage = "be terse",
        AutoApprove = true,
        InfiniteSessions = true,
        LargeOutput = new LargeToolOutputConfig
        {
            Enabled = true,
            MaxSizeBytes = 2048,
            OutputDirectory = "/tmp/copilot-output"
        },
        Memory = new MemoryConfiguration { Enabled = false },
        AvailableTools = ["builtin:bash"],
        ExcludedTools = ["builtin:web"],
        EnableConfigDiscovery = true,
        Agent = "my-agent",
        CustomAgents =
        [
            new CustomAgentConfig
            {
                Name = "custom-agent",
                Prompt = "Be concise",
                Tools = ["builtin:read_file"]
            }
        ],
        DefaultAgent = new DefaultAgentConfig { ExcludedTools = ["builtin:bash"] },
        CustomAgentsLocalOnly = true,
        SkillDirectories = ["/skills"],
        DisabledSkills = ["skip-me"],
        EnableCitations = true,
        ExcludedBuiltInAgents = ["agent-x"],
        MaxAiCredits = 25.5,
        McpServers = new Hashtable { ["srv"] = new Hashtable { ["Command"] = "cmd" } },
        McpOAuthTokenStorage = McpOAuthTokenStorageMode.InMemory,
        Provider = new ProviderConfig
        {
            BaseUrl = "http://localhost:8080",
            Type = "openai"
        },
        Providers =
        [
            new NamedProviderConfig
            {
                Name = "local",
                BaseUrl = "http://localhost:8081",
                Type = "openai"
            }
        ],
        ProviderModels =
        [
            new ProviderModelConfig
            {
                Id = "test-model",
                Provider = "local",
                ModelId = "wire-model"
            }
        ],
        Hooks = new SessionHooks(),
        RemoteSession = "export",
        Commands =
        [
            new CommandDefinition
            {
                Name = "test",
                Description = "test command",
                Handler = _ => Task.CompletedTask
            }
        ],
        Tool = [new ScriptBlockToolFunction("t1", "d", ScriptBlock.Create("1"))]
    };

    [Fact]
    public void ApplyCommonOptions_MapsEveryParameterToSessionConfig()
    {
        var cmdlet = CreateSessionCmdlet();
        var config = new SessionConfig();

        cmdlet.ApplyCommonOptions(config);

        Assert.True(config.Streaming);
        Assert.NotNull(config.OnPermissionRequest);
        Assert.NotNull(config.OnUserInputRequest);
        Assert.Equal("test-model", config.Model);
        Assert.Equal("high", config.ReasoningEffort);
        Assert.Equal("be terse", config.SystemMessage!.Content);
        Assert.True(config.InfiniteSessions!.Enabled);
        Assert.Equal(2048, config.LargeOutput!.MaxSizeBytes);
        Assert.False(config.Memory!.Enabled);
        Assert.Equal(["builtin:bash"], config.AvailableTools);
        Assert.Equal(["builtin:web"], config.ExcludedTools);
        Assert.True(config.EnableConfigDiscovery);
        Assert.Equal("my-agent", config.Agent);
        Assert.Equal("custom-agent", Assert.Single(config.CustomAgents!).Name);
        Assert.Equal(["builtin:bash"], config.DefaultAgent!.ExcludedTools);
        Assert.True(config.CustomAgentsLocalOnly);
        Assert.Equal(["/skills"], config.SkillDirectories);
        Assert.Equal(["skip-me"], config.DisabledSkills);
        Assert.True(config.EnableCitations);
        Assert.Equal(["agent-x"], config.ExcludedBuiltInAgents);
        Assert.Equal(25.5, config.SessionLimits!.MaxAiCredits);
        Assert.IsType<McpStdioServerConfig>(config.McpServers!["srv"]);
        Assert.Equal(McpOAuthTokenStorageMode.InMemory, config.McpOAuthTokenStorage);
        Assert.Equal("http://localhost:8080", config.Provider!.BaseUrl);
        Assert.Equal("local", Assert.Single(config.Providers!).Name);
        Assert.Equal("test-model", Assert.Single(config.Models!).Id);
        Assert.Same(cmdlet.Hooks, config.Hooks);
        Assert.Equal(GitHub.Copilot.Rpc.RemoteSessionMode.Export, config.RemoteSession);
        Assert.Equal("test", Assert.Single(config.Commands!).Name);
        Assert.Single(config.Tools!);
        Assert.Equal("t1", config.Tools!.Single().Name);
    }

    [Fact]
    public void ApplyCommonOptions_UnsetParametersLeaveConfigDefaults()
    {
        var config = new SessionConfig();

        new NewCopilotSessionCmdlet().ApplyCommonOptions(config);

        Assert.True(config.Streaming);
        Assert.Null(config.Model);
        Assert.Null(config.SystemMessage);
        Assert.Null(config.InfiniteSessions);
        Assert.Null(config.LargeOutput);
        Assert.Null(config.Memory);
        Assert.Null(config.AvailableTools);
        Assert.Null(config.EnableCitations);
        Assert.Null(config.SessionLimits);
        Assert.Null(config.McpServers);
        Assert.Null(config.McpOAuthTokenStorage);
        Assert.Null(config.Provider);
        Assert.Null(config.Providers);
        Assert.Null(config.Models);
        Assert.Null(config.Hooks);
        Assert.Null(config.OnMcpAuthRequest);
        Assert.Null(config.OnElicitationRequest);
        Assert.Null(config.OnExitPlanModeRequest);
        Assert.Null(config.OnAutoModeSwitchRequest);
        Assert.Null(config.RemoteSession);
        Assert.Null(config.Commands);
        Assert.Null(config.Tools);
        Assert.Null(config.EnableConfigDiscovery);
    }

    [Fact]
    public void ApplyCommonOptions_AutoApproveSelectsApproveAllHandler()
    {
        var approving = new SessionConfig();
        var interactive = new SessionConfig();

        new NewCopilotSessionCmdlet { AutoApprove = true }.ApplyCommonOptions(approving);
        new NewCopilotSessionCmdlet().ApplyCommonOptions(interactive);

        Assert.Same(PermissionHandler.ApproveAll, approving.OnPermissionRequest);
        Assert.NotSame(PermissionHandler.ApproveAll, interactive.OnPermissionRequest);
    }

    [Fact]
    public void ApplyCommonOptions_WorksIdenticallyForResumeConfig()
    {
        var cmdlet = new ResumeCopilotSessionCmdlet
        {
            SessionId = "s",
            Model = "resume-model",
            MaxAiCredits = 5
        };
        var config = new ResumeSessionConfig();

        cmdlet.ApplyCommonOptions(config);

        Assert.Equal("resume-model", config.Model);
        Assert.Equal(5, config.SessionLimits!.MaxAiCredits);
    }

    [Fact]
    public void ApplyCommonOptions_MapsTypedInfiniteSessionConfig()
    {
        var infiniteSessions = new InfiniteSessionConfig
        {
            Enabled = false,
            BackgroundCompactionThreshold = 0.7,
            BufferExhaustionThreshold = 0.9
        };
        var config = new SessionConfig();

        new NewCopilotSessionCmdlet { InfiniteSessionConfig = infiniteSessions }
            .ApplyCommonOptions(config);

        Assert.Same(infiniteSessions, config.InfiniteSessions);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void CustomAgentsLocalOnly_PreservesOmittedAndExplicitBooleanValues(
        bool? parameterValue,
        bool? expected)
    {
        var state = InitialSessionState.CreateDefault2();
        state.Commands.Add(new SessionStateCmdletEntry(
            "Test-CopilotSessionConfig",
            typeof(TestCopilotSessionConfigCmdlet),
            null));
        using var ps = PowerShell.Create(state);
        ps.AddCommand("Test-CopilotSessionConfig");
        if (parameterValue is not null)
            ps.AddParameter("CustomAgentsLocalOnly", parameterValue.Value);

        var output = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        var config = Assert.IsType<SessionConfig>(Assert.Single(output).BaseObject);
        Assert.Equal(expected, config.CustomAgentsLocalOnly);
    }

    [Fact]
    public void ApplyCommonOptions_RejectsBothInfiniteSessionParameters()
    {
        var cmdlet = new NewCopilotSessionCmdlet
        {
            InfiniteSessions = true,
            InfiniteSessionConfig = new InfiniteSessionConfig()
        };

        var error = Assert.Throws<ArgumentException>(
            () => cmdlet.ApplyCommonOptions(new SessionConfig()));

        Assert.Contains("-InfiniteSessions", error.Message);
        Assert.Contains("-InfiniteSessionConfig", error.Message);
    }

    [Cmdlet(VerbsDiagnostic.Test, "CopilotSessionConfig")]
    public sealed class TestCopilotSessionConfigCmdlet : SessionConfigCmdletBase
    {
        protected override void EndProcessing()
        {
            var config = new SessionConfig();
            ApplyCommonOptions(config);
            WriteObject(config);
        }
    }

    [Fact]
    public void ApplyCommonOptions_MapsRawCallbackDelegates()
    {
        Func<McpAuthContext, Task<McpAuthResult?>> mcp = _ =>
            Task.FromResult<McpAuthResult?>(McpAuthResult.Cancel());
        Func<ElicitationContext, Task<ElicitationResult>> elicitation = _ =>
            Task.FromResult<ElicitationResult>(null!);
        Func<ExitPlanModeRequest, ExitPlanModeInvocation, Task<ExitPlanModeResult>> exitPlan =
            (_, _) => Task.FromResult<ExitPlanModeResult>(null!);
        Func<AutoModeSwitchRequest, AutoModeSwitchInvocation, Task<AutoModeSwitchResponse>> autoMode =
            (_, _) => Task.FromResult(AutoModeSwitchResponse.No);
        var cmdlet = new NewCopilotSessionCmdlet
        {
            OnMcpAuthRequestDelegate = mcp,
            OnElicitationRequestDelegate = elicitation,
            OnExitPlanModeRequestDelegate = exitPlan,
            OnAutoModeSwitchRequestDelegate = autoMode
        };
        var config = new SessionConfig();

        cmdlet.ApplyCommonOptions(config);

        Assert.Same(mcp, config.OnMcpAuthRequest);
        Assert.Same(elicitation, config.OnElicitationRequest);
        Assert.Same(exitPlan, config.OnExitPlanModeRequest);
        Assert.Same(autoMode, config.OnAutoModeSwitchRequest);
    }

    [Fact]
    public async Task ApplyCommonOptions_MapsScriptBlockCallback()
    {
        var cmdlet = new NewCopilotSessionCmdlet
        {
            OnMcpAuthRequest = ScriptBlock.Create(
                "[GitHub.Copilot.McpAuthResult]::Cancel()")
        };
        var config = new SessionConfig();

        cmdlet.ApplyCommonOptions(config);
        var result = await config.OnMcpAuthRequest!(null!);

        Assert.NotNull(result);
        Assert.True(result.Cancelled);
    }

    [Fact]
    public void ApplyCommonOptions_RejectsScriptBlockAndRawDelegateTogether()
    {
        var cmdlet = new NewCopilotSessionCmdlet
        {
            OnMcpAuthRequest = ScriptBlock.Create("$null"),
            OnMcpAuthRequestDelegate = _ => Task.FromResult<McpAuthResult?>(null)
        };

        var error = Assert.Throws<ArgumentException>(
            () => cmdlet.ApplyCommonOptions(new SessionConfig()));

        Assert.Contains("-OnMcpAuthRequest", error.Message);
        Assert.Contains("-OnMcpAuthRequestDelegate", error.Message);
    }

    [Fact]
    public void NewSessionBuildConfig_MapsCreateOnlyOptions()
    {
        var cloud = new CloudSessionOptions();
        var cmdlet = new NewCopilotSessionCmdlet
        {
            SessionId = "new-session",
            Cloud = cloud
        };

        var config = cmdlet.BuildConfig();

        Assert.Equal("new-session", config.SessionId);
        Assert.Same(cloud, config.Cloud);
    }

    [Fact]
    public void ResumeSessionBuildConfig_MapsResumeOnlyOptions()
    {
        var config = new ResumeCopilotSessionCmdlet
        {
            SessionId = "resume-session",
            ContinuePendingWork = true
        }.BuildConfig();

        Assert.True(config.ContinuePendingWork);
    }

    [Fact]
    public void BuildOptions_MapsClientParameters()
    {
        var cmdlet = new NewCopilotClientCmdlet
        {
            LogLevel = "debug",
            GitHubToken = "placeholder-token",
            UseLoggedInUser = true,
            OtlpEndpoint = "http://localhost:4318",
            TelemetrySourceName = "test-source",
            CliPath = "/custom/copilot",
            Environment = new Hashtable { ["FOO"] = new PSObject("bar") },
            EnableRemoteSessions = true
        };

        var options = cmdlet.BuildOptions();

        Assert.Equal(new CopilotLogLevel("debug"), options.LogLevel);
        Assert.Equal("placeholder-token", options.GitHubToken);
        Assert.True(options.UseLoggedInUser);
        Assert.Equal("http://localhost:4318", options.Telemetry!.OtlpEndpoint);
        Assert.Equal("test-source", options.Telemetry.SourceName);
        var stdio = Assert.IsType<StdioRuntimeConnection>(options.Connection);
        Assert.Equal("/custom/copilot", stdio.Path);
        Assert.Equal("bar", options.Environment!["FOO"]);
        Assert.True(options.EnableRemoteSessions);
    }

    [Fact]
    public void BuildOptions_CliUrlCreatesUriConnection()
    {
        var cmdlet = new NewCopilotClientCmdlet { CliUrl = "localhost:9000" };

        var options = cmdlet.BuildOptions();

        var uri = Assert.IsType<UriRuntimeConnection>(options.Connection);
        Assert.Equal("localhost:9000", uri.Url);
    }

    [Fact]
    public void BuildOptions_UnsetParametersLeaveDefaults()
    {
        var options = new NewCopilotClientCmdlet { CliPath = "/x" }.BuildOptions();

        Assert.Null(options.LogLevel);
        Assert.Null(options.GitHubToken);
        Assert.Null(options.UseLoggedInUser);
        Assert.Null(options.Telemetry);
        Assert.Null(options.Environment);
        Assert.False(options.EnableRemoteSessions);
    }

    /// <summary>Runs an action with COPILOT_CLI_PATH set to the given value, restoring afterwards.</summary>
    private static void WithCliPathEnv(string? value, Action action)
    {
        var original = System.Environment.GetEnvironmentVariable("COPILOT_CLI_PATH");
        try
        {
            System.Environment.SetEnvironmentVariable("COPILOT_CLI_PATH", value);
            action();
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("COPILOT_CLI_PATH", original);
        }
    }

    [Fact]
    public void BuildOptions_NoCliAnywhere_ThrowsWithModuleGuidance()
    {
        WithCliPathEnv(null, () =>
        {
            var ex = Assert.Throws<FileNotFoundException>(
                () => new NewCopilotClientCmdlet().BuildOptions(() => null));

            Assert.Contains("-CliPath", ex.Message);
            Assert.Contains("github.com/Halcyonhal9/ghcp-powershell/releases", ex.Message);
        });
    }

    [Fact]
    public void BuildOptions_EnvironmentParameterCliPathOverride_SkipsThrow()
    {
        var cmdlet = new NewCopilotClientCmdlet
        {
            Environment = new Hashtable { ["COPILOT_CLI_PATH"] = "/custom/copilot" }
        };

        var options = cmdlet.BuildOptions(() => null);

        // Connection stays unset; the SDK resolves COPILOT_CLI_PATH itself.
        Assert.Null(options.Connection);
        Assert.Equal("/custom/copilot", options.Environment!["COPILOT_CLI_PATH"]);
    }

    [Fact]
    public void BuildOptions_ProcessEnvCliPathOverride_SkipsThrow()
    {
        WithCliPathEnv("/from/process/env", () =>
        {
            var options = new NewCopilotClientCmdlet().BuildOptions(() => null);

            Assert.Null(options.Connection);
        });
    }

    [Fact]
    public void BuildOptions_BundledCliResolved_UsesStdioConnection()
    {
        var options = new NewCopilotClientCmdlet().BuildOptions(() => "/bundled/copilot");

        var stdio = Assert.IsType<StdioRuntimeConnection>(options.Connection);
        Assert.Equal("/bundled/copilot", stdio.Path);
    }
}
