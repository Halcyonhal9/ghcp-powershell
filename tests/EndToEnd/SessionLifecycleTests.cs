using System.Management.Automation;
using GitHub.Copilot;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.EndToEnd;

[Trait("Category", "EndToEnd")]
[Collection("EndToEnd")]
public class SessionLifecycleTests : IAsyncLifetime
{
    private PowerShell ps = null!;
    private readonly string testSessionId = $"test-{Guid.NewGuid():N}";

    public Task InitializeAsync()
    {
        ps = PowerShell.Create();
        var modulePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "out", "CopilotCmdlets.psd1");
        ps.AddCommand("Import-Module").AddParameter("Name", Path.GetFullPath(modulePath));
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
            ps.AddCommand("Remove-CopilotSession").AddParameter("SessionId", testSessionId).AddParameter("Confirm", false);
            ps.Invoke();
        }
        catch { }

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

    /// <summary>
    /// The Copilot CLI only persists sessions once they contain at least one
    /// message; empty sessions are not listed and cannot be resumed. Send a
    /// trivial message so lifecycle operations behave deterministically.
    /// </summary>
    private void SendSeedMessage()
    {
        ps.AddCommand("Send-CopilotMessage")
            .AddParameter("Prompt", "Say exactly: ok")
            .AddParameter("Timeout", TimeSpan.FromSeconds(120));
        ps.Invoke();
        ps.Commands.Clear();
    }

    [Fact]
    public void NewCopilotSession_CreatesSession()
    {
        ps.AddCommand("New-CopilotSession")
            .AddParameter("SessionId", testSessionId)
            .AddParameter("AutoApprove", true);

        var results = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        Assert.Single(results);
        var session = Assert.IsType<CopilotSession>(results[0].BaseObject);
        Assert.Equal(testSessionId, session.SessionId);
    }

    [Fact]
    public void GetCopilotSession_ListsSessions()
    {
        ps.AddCommand("New-CopilotSession")
            .AddParameter("SessionId", testSessionId)
            .AddParameter("AutoApprove", true);
        ps.Invoke();
        ps.Commands.Clear();

        SendSeedMessage();

        ps.AddCommand("Get-CopilotSession");
        var results = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        Assert.True(results.Count > 0);

        var sessionIds = results.Select(r => ((SessionMetadata)r.BaseObject).SessionId).ToList();
        Assert.Contains(testSessionId, sessionIds);
    }

    [Fact]
    public void CloseCopilotSession_ClosesWithoutDeleting()
    {
        ps.AddCommand("New-CopilotSession")
            .AddParameter("SessionId", testSessionId)
            .AddParameter("AutoApprove", true);
        ps.Invoke();
        ps.Commands.Clear();

        SendSeedMessage();

        ps.AddCommand("Close-CopilotSession");
        ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));

        // Session should still appear in the list
        ps.Commands.Clear();
        ps.AddCommand("Get-CopilotSession");
        var results = ps.Invoke();

        var sessionIds = results.Select(r => ((SessionMetadata)r.BaseObject).SessionId).ToList();
        Assert.Contains(testSessionId, sessionIds);
    }

    [Fact]
    public void ResumeCopilotSession_ResumesClosedSession()
    {
        ps.AddCommand("New-CopilotSession")
            .AddParameter("SessionId", testSessionId)
            .AddParameter("AutoApprove", true);
        ps.Invoke();
        ps.Commands.Clear();

        SendSeedMessage();

        ps.AddCommand("Close-CopilotSession");
        ps.Invoke();
        ps.Commands.Clear();

        ps.AddCommand("Resume-CopilotSession")
            .AddParameter("SessionId", testSessionId)
            .AddParameter("AutoApprove", true);
        var results = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        Assert.Single(results);
        Assert.IsType<CopilotSession>(results[0].BaseObject);
    }

    [Fact]
    public void RemoveCopilotSession_DeletesSession()
    {
        ps.AddCommand("New-CopilotSession")
            .AddParameter("SessionId", testSessionId)
            .AddParameter("AutoApprove", true);
        ps.Invoke();
        ps.Commands.Clear();

        SendSeedMessage();

        ps.AddCommand("Close-CopilotSession");
        ps.Invoke();
        ps.Commands.Clear();

        ps.AddCommand("Remove-CopilotSession")
            .AddParameter("SessionId", testSessionId)
            .AddParameter("Confirm", false);
        ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));

        // Session should no longer appear in the list
        ps.Commands.Clear();
        ps.AddCommand("Get-CopilotSession");
        var results = ps.Invoke();

        var sessionIds = results.Select(r => ((SessionMetadata)r.BaseObject).SessionId).ToList();
        Assert.DoesNotContain(testSessionId, sessionIds);
    }
}
