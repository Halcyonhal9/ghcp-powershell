#pragma warning disable GHCP001 // asserting experimental SDK options (EnableCitations)
using System.Collections;
using System.Management.Automation;
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
        AvailableTools = ["builtin:bash"],
        ExcludedTools = ["builtin:web"],
        EnableConfigDiscovery = true,
        Agent = "my-agent",
        SkillDirectories = ["/skills"],
        DisabledSkills = ["skip-me"],
        EnableCitations = true,
        ExcludedBuiltInAgents = ["agent-x"],
        MaxAiCredits = 25.5,
        McpServers = new Hashtable { ["srv"] = new Hashtable { ["Command"] = "cmd" } },
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
        Assert.Equal(["builtin:bash"], config.AvailableTools);
        Assert.Equal(["builtin:web"], config.ExcludedTools);
        Assert.True(config.EnableConfigDiscovery);
        Assert.Equal("my-agent", config.Agent);
        Assert.Equal(["/skills"], config.SkillDirectories);
        Assert.Equal(["skip-me"], config.DisabledSkills);
        Assert.True(config.EnableCitations);
        Assert.Equal(["agent-x"], config.ExcludedBuiltInAgents);
        Assert.Equal(25.5, config.SessionLimits!.MaxAiCredits);
        Assert.IsType<McpStdioServerConfig>(config.McpServers!["srv"]);
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
        Assert.Null(config.AvailableTools);
        Assert.Null(config.EnableCitations);
        Assert.Null(config.SessionLimits);
        Assert.Null(config.McpServers);
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
            Environment = new Hashtable { ["FOO"] = new PSObject("bar") }
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
    }

    [Fact]
    public void BuildOptions_NoCliAnywhere_ThrowsWithModuleGuidance()
    {
        var original = System.Environment.GetEnvironmentVariable("COPILOT_CLI_PATH");
        try
        {
            System.Environment.SetEnvironmentVariable("COPILOT_CLI_PATH", null);

            var ex = Assert.Throws<FileNotFoundException>(
                () => new NewCopilotClientCmdlet().BuildOptions(() => null));

            Assert.Contains("-CliPath", ex.Message);
            Assert.Contains("github.com/Halcyonhal9/ghcp-powershell/releases", ex.Message);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("COPILOT_CLI_PATH", original);
        }
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
        var original = System.Environment.GetEnvironmentVariable("COPILOT_CLI_PATH");
        try
        {
            System.Environment.SetEnvironmentVariable("COPILOT_CLI_PATH", "/from/process/env");

            var options = new NewCopilotClientCmdlet().BuildOptions(() => null);

            Assert.Null(options.Connection);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("COPILOT_CLI_PATH", original);
        }
    }

    [Fact]
    public void BuildOptions_BundledCliResolved_UsesStdioConnection()
    {
        var options = new NewCopilotClientCmdlet().BuildOptions(() => "/bundled/copilot");

        var stdio = Assert.IsType<StdioRuntimeConnection>(options.Connection);
        Assert.Equal("/bundled/copilot", stdio.Path);
    }
}
