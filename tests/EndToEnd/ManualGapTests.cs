#pragma warning disable GHCP001 // experimental SDK surfaces covered by issue #28
using System.Collections;
using System.Collections.ObjectModel;
using System.Management.Automation;
using GitHub.Copilot;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.EndToEnd;

public sealed class ManualGapFactAttribute : FactAttribute
{
    public ManualGapFactAttribute(params string[] requirements)
    {
        foreach (var requirement in requirements)
        {
            var parts = requirement.Split('=', 2);
            var value = Environment.GetEnvironmentVariable(parts[0]);
            var satisfied = parts.Length == 1
                ? !string.IsNullOrWhiteSpace(value)
                : string.Equals(value, parts[1], StringComparison.OrdinalIgnoreCase);
            if (!satisfied)
            {
                Skip = $"Manual prerequisite not satisfied: {requirement}";
                return;
            }
        }
    }
}

[Trait("Category", "EndToEnd")]
[Trait("Mode", "Manual")]
[Collection("EndToEnd")]
public class ManualGapTests
{
    [ManualGapFact("COPILOT_UI_SERVER_URL")]
    public void ForegroundSession_RoundTripsAgainstUiServer()
    {
        var uiServerUrl = RequireEnvironment("COPILOT_UI_SERVER_URL");
        var sessionId = $"foreground-manual-{Guid.NewGuid():N}";
        using var ps = CreateShell();

        try
        {
            var client = Assert.IsType<CopilotClient>(Assert.Single(Invoke(
                ps,
                "New-CopilotClient",
                ("CliUrl", uiServerUrl))).BaseObject);
            var session = Assert.IsType<CopilotSession>(Assert.Single(Invoke(
                ps,
                "New-CopilotSession",
                ("Client", client),
                ("SessionId", sessionId),
                ("AvailableTools", Array.Empty<string>()))).BaseObject);

            Invoke(
                ps,
                "Set-CopilotForegroundSessionId",
                ("Client", client),
                ("SessionId", session.SessionId));
            var foreground = Invoke(
                ps,
                "Get-CopilotForegroundSessionId",
                ("Client", client));

            Assert.Equal(session.SessionId, Assert.Single(foreground).BaseObject);
        }
        finally
        {
            TryInvoke(ps, "Close-CopilotSession");
            TryInvoke(
                ps,
                "Remove-CopilotSession",
                ("SessionId", sessionId),
                ("Confirm", false));
            TryInvoke(ps, "Stop-CopilotClient", ("Force", true));
        }
    }

    [ManualGapFact("COPILOT_ALLOW_REMOTE_TESTS=true")]
    public void RemoteSession_ExportRequiresExplicitAuthorization()
    {
        RequireAuthorization("COPILOT_ALLOW_REMOTE_TESTS");
        var sessionId = $"remote-manual-{Guid.NewGuid():N}";
        using var ps = CreateShell();

        try
        {
            var client = Assert.IsType<CopilotClient>(Assert.Single(Invoke(
                ps,
                "New-CopilotClient",
                ("EnableRemoteSessions", true))).BaseObject);
            var session = Assert.IsType<CopilotSession>(Assert.Single(Invoke(
                ps,
                "New-CopilotSession",
                ("Client", client),
                ("SessionId", sessionId),
                ("RemoteSession", "export"),
                ("AvailableTools", Array.Empty<string>()))).BaseObject);

            var export = session.Rpc.Remote.EnableAsync(
                    GitHub.Copilot.Rpc.RemoteSessionMode.Export)
                .GetAwaiter().GetResult();
            Assert.False(string.IsNullOrWhiteSpace(export.Url));
            Assert.False(export.RemoteSteerable);

            var steering = session.Rpc.Remote.EnableAsync(
                    GitHub.Copilot.Rpc.RemoteSessionMode.On)
                .GetAwaiter().GetResult();
            Assert.False(string.IsNullOrWhiteSpace(steering.Url));
            Assert.True(steering.RemoteSteerable);

            session.Rpc.Remote.DisableAsync().GetAwaiter().GetResult();
        }
        finally
        {
            TryInvoke(ps, "Close-CopilotSession");
            TryInvoke(
                ps,
                "Remove-CopilotSession",
                ("SessionId", sessionId),
                ("Confirm", false));
            TryInvoke(ps, "Stop-CopilotClient", ("Force", true));
        }
    }

    [ManualGapFact(
        "COPILOT_ALLOW_CLOUD_TESTS=true",
        "COPILOT_CLOUD_TEST_OWNER",
        "COPILOT_CLOUD_TEST_REPOSITORY")]
    public void CloudSession_CreateRequiresExplicitAuthorization()
    {
        RequireAuthorization("COPILOT_ALLOW_CLOUD_TESTS");
        var owner = RequireEnvironment("COPILOT_CLOUD_TEST_OWNER");
        var repository = RequireEnvironment("COPILOT_CLOUD_TEST_REPOSITORY");
        var branch = Environment.GetEnvironmentVariable("COPILOT_CLOUD_TEST_BRANCH");
        var sessionId = $"cloud-manual-{Guid.NewGuid():N}";
        using var ps = CreateShell();

        try
        {
            var client = Assert.IsType<CopilotClient>(Assert.Single(Invoke(
                ps,
                "New-CopilotClient",
                ("EnableRemoteSessions", true))).BaseObject);
            var cloud = new CloudSessionOptions
            {
                Repository = new CloudSessionRepository
                {
                    Owner = owner,
                    Name = repository,
                    Branch = branch
                }
            };
            var session = Assert.IsType<CopilotSession>(Assert.Single(Invoke(
                ps,
                "New-CopilotSession",
                ("Client", client),
                ("SessionId", sessionId),
                ("Cloud", cloud),
                ("AvailableTools", Array.Empty<string>()))).BaseObject);

            Assert.False(string.IsNullOrWhiteSpace(session.SessionId));
            var metadata = Assert.IsType<SessionMetadata>(Assert.Single(Invoke(
                ps,
                "Get-CopilotSession",
                ("Client", client),
                ("SessionId", sessionId))).BaseObject);
            Assert.True(metadata.IsRemote);
            Assert.Equal($"{owner}/{repository}", metadata.Context?.Repository);
        }
        finally
        {
            TryInvoke(ps, "Close-CopilotSession");
            TryInvoke(
                ps,
                "Remove-CopilotSession",
                ("SessionId", sessionId),
                ("Confirm", false));
            TryInvoke(ps, "Stop-CopilotClient", ("Force", true));
        }
    }

    [ManualGapFact(
        "COPILOT_AUTO_MODE_TEST_URL",
        "COPILOT_AUTO_MODE_TEST_TOKEN")]
    public void AutoModeSwitch_UsesRateLimitFixture()
    {
        var apiUrl = RequireEnvironment("COPILOT_AUTO_MODE_TEST_URL");
        var token = RequireEnvironment("COPILOT_AUTO_MODE_TEST_TOKEN");
        var sessionId = $"auto-mode-manual-{Guid.NewGuid():N}";
        using var ps = CreateShell();

        try
        {
            var client = Assert.IsType<CopilotClient>(Assert.Single(Invoke(
                ps,
                "New-CopilotClient",
                ("GitHubToken", token),
                ("Environment", new Hashtable
                {
                    ["COPILOT_DEBUG_GITHUB_API_URL"] = apiUrl
                }))).BaseObject);
            var session = Assert.IsType<CopilotSession>(Assert.Single(Invoke(
                ps,
                "New-CopilotSession",
                ("Client", client),
                ("SessionId", sessionId),
                ("MaxAiCredits", 50),
                ("OnAutoModeSwitchRequest", ScriptBlock.Create(
                    "param($request, $invocation) [GitHub.Copilot.AutoModeSwitchResponse]::Yes"))))
                .BaseObject);

            var result = Assert.IsType<CopilotMessageResult>(Assert.Single(Invoke(
                ps,
                "Send-CopilotMessage",
                ("Session", session),
                ("Prompt", "Explain the recovered auto-mode switch in one sentence."),
                ("Timeout", TimeSpan.FromSeconds(120)))).BaseObject);

            Assert.Contains(result.Events, item => item is AutoModeSwitchRequestedEvent);
            Assert.Contains(result.Events, item => item is AutoModeSwitchCompletedEvent);
        }
        finally
        {
            TryInvoke(ps, "Close-CopilotSession");
            TryInvoke(
                ps,
                "Remove-CopilotSession",
                ("SessionId", sessionId),
                ("Confirm", false));
            TryInvoke(ps, "Stop-CopilotClient", ("Force", true));
        }
    }

    [ManualGapFact("COPILOT_ALLOW_MEMORY_TESTS=true")]
    public void Memory_EnabledSessionAcceptsTurn()
    {
        RequireAuthorization("COPILOT_ALLOW_MEMORY_TESTS");
        var firstSessionId = $"memory-manual-{Guid.NewGuid():N}";
        var secondSessionId = $"memory-manual-{Guid.NewGuid():N}";
        var nonce = $"memory-{Guid.NewGuid():N}";
        using var ps = CreateShell();

        try
        {
            var client = Assert.IsType<CopilotClient>(Assert.Single(Invoke(
                ps,
                "New-CopilotClient")).BaseObject);
            var session = Assert.IsType<CopilotSession>(Assert.Single(Invoke(
                ps,
                "New-CopilotSession",
                ("Client", client),
                ("SessionId", firstSessionId),
                ("Memory", new MemoryConfiguration { Enabled = true }),
                ("AvailableTools", Array.Empty<string>()),
                ("MaxAiCredits", 50))).BaseObject);

            Invoke(
                ps,
                "Send-CopilotMessage",
                ("Session", session),
                ("Prompt", $"Remember this exact token for future sessions: {nonce}. Reply exactly: saved."),
                ("Timeout", TimeSpan.FromSeconds(120)));
            Invoke(ps, "Close-CopilotSession", ("Session", session));

            var secondSession = Assert.IsType<CopilotSession>(Assert.Single(Invoke(
                ps,
                "New-CopilotSession",
                ("Client", client),
                ("SessionId", secondSessionId),
                ("Memory", new MemoryConfiguration { Enabled = true }),
                ("AvailableTools", Array.Empty<string>()),
                ("MaxAiCredits", 50))).BaseObject);
            var result = Assert.IsType<CopilotMessageResult>(Assert.Single(Invoke(
                ps,
                "Send-CopilotMessage",
                ("Session", secondSession),
                ("Prompt", "What exact token did I ask you to remember? Reply with only the token."),
                ("Timeout", TimeSpan.FromSeconds(120)))).BaseObject);

            Assert.Contains(nonce, result.Content, StringComparison.Ordinal);
        }
        finally
        {
            TryInvoke(ps, "Close-CopilotSession");
            TryInvoke(
                ps,
                "Remove-CopilotSession",
                ("SessionId", firstSessionId),
                ("Confirm", false));
            TryInvoke(
                ps,
                "Remove-CopilotSession",
                ("SessionId", secondSessionId),
                ("Confirm", false));
            TryInvoke(ps, "Stop-CopilotClient", ("Force", true));
        }
    }

    private static PowerShell CreateShell()
    {
        var ps = PowerShell.Create();
        ps.AddCommand("Import-Module")
            .AddParameter("Name", E2eModule.ResolveManifest())
            .AddParameter("Force", true);
        var output = ps.Invoke();
        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        Assert.Empty(output);
        Reset(ps);
        return ps;
    }

    private static Collection<PSObject> Invoke(
        PowerShell ps,
        string command,
        params (string Name, object Value)[] parameters)
    {
        Reset(ps);
        ps.AddCommand(command);
        foreach (var (name, value) in parameters)
            ps.AddParameter(name, value);

        var output = ps.Invoke();
        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        return output;
    }

    private static void TryInvoke(
        PowerShell ps,
        string command,
        params (string Name, object Value)[] parameters)
    {
        try
        {
            Invoke(ps, command, parameters);
        }
        catch
        {
        }
    }

    private static void Reset(PowerShell ps)
    {
        ps.Commands.Clear();
        ps.Streams.Error.Clear();
        ps.Streams.Warning.Clear();
    }

    private static string RequireEnvironment(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"{name} must be set before running this manual E2E test.");
        }

        return value;
    }

    private static void RequireAuthorization(string name)
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(name),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{name}=true is required because this manual E2E test mutates remote or account state.");
        }
    }
}
