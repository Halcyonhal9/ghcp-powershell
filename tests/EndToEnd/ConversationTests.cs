using System.Management.Automation;
using GitHub.Copilot.SDK;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.EndToEnd;

[Trait("Category", "EndToEnd")]
public class ConversationTests : IAsyncLifetime
{
    private PowerShell ps = null!;
    private readonly string testSessionId = $"test-conv-{Guid.NewGuid():N}";

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

        ps.AddCommand("New-CopilotSession")
            .AddParameter("SessionId", testSessionId)
            .AddParameter("AutoApprove", true);
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

        try
        {
            ps.Commands.Clear();
            ps.AddCommand("Remove-CopilotSession")
                .AddParameter("SessionId", testSessionId)
                .AddParameter("Confirm", false);
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
    public void SendCopilotMessage_ReturnsResultWithContent()
    {
        ps.AddCommand("Send-CopilotMessage")
            .AddParameter("Prompt", "Say exactly: hello world");

        var results = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        Assert.Single(results);

        var result = (CopilotMessageResult)results[0].BaseObject;
        Assert.NotNull(result.Content);
        Assert.NotEmpty(result.Content);
        Assert.Equal(testSessionId, result.SessionId);
    }

    [Fact]
    public void SendCopilotMessage_EventsContainExpectedTypes()
    {
        ps.AddCommand("Send-CopilotMessage")
            .AddParameter("Prompt", "Say exactly: test");

        var results = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));

        var result = (CopilotMessageResult)results[0].BaseObject;
        Assert.True(result.Events.Count > 0, "Expected at least one event");

        var eventTypes = result.Events.Select(e => e.GetType()).ToList();
        Assert.Contains(typeof(SessionIdleEvent), eventTypes);
    }

    [Fact]
    public void GetCopilotMessage_ReturnsHistory()
    {
        ps.AddCommand("Send-CopilotMessage")
            .AddParameter("Prompt", "Say exactly: history test");
        ps.Invoke();
        ps.Commands.Clear();

        ps.AddCommand("Get-CopilotMessage");
        var results = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        Assert.True(results.Count > 0, "Expected message history to contain events");
    }
}
