using System.Management.Automation;
using GitHub.Copilot.SDK;
using Xunit;

namespace CopilotPS.Tests.EndToEnd;

[Trait("Category", "EndToEnd")]
public class SessionLifecycleTests : IAsyncLifetime
{
    private PowerShell ps = null!;
    private readonly string testSessionId = $"test-{Guid.NewGuid():N}";

    public Task InitializeAsync()
    {
        ps = PowerShell.Create();
        var modulePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "out", "CopilotPS.psd1");
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

    [Fact]
    public void NewCopilotSession_CreatesSession()
    {
        ps.AddCommand("New-CopilotSession")
            .AddParameter("SessionId", testSessionId)
            .AddParameter("AutoApprove", true);

        var results = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        Assert.Single(results);
        Assert.IsType<CopilotSession>(results[0].BaseObject);
    }

    [Fact]
    public void GetCopilotSession_ListsSessions()
    {
        ps.AddCommand("New-CopilotSession")
            .AddParameter("SessionId", testSessionId)
            .AddParameter("AutoApprove", true);
        ps.Invoke();
        ps.Commands.Clear();

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
