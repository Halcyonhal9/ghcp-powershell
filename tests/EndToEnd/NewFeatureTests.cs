using System.Management.Automation;
using GitHub.Copilot;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.EndToEnd;

[Trait("Category", "EndToEnd")]
[Collection("EndToEnd")]
public class NewFeatureTests : IAsyncLifetime
{
    private PowerShell ps = null!;

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
            ps.AddCommand("Stop-CopilotClient").AddParameter("Force", true);
            ps.Invoke();
        }
        catch { }

        ps.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public void GetCopilotStatus_ReturnsVersionAndProtocol()
    {
        ps.AddCommand("Get-CopilotStatus");
        var results = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        var status = Assert.IsType<GetStatusResponse>(Assert.Single(results).BaseObject);
        Assert.NotEmpty(status.Version);
        Assert.True(status.ProtocolVersion > 0);
    }

    [Fact]
    public void GetCopilotAuthStatus_ReportsAuthenticated()
    {
        ps.AddCommand("Get-CopilotAuthStatus");
        var results = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        var status = Assert.IsType<GetAuthStatusResponse>(Assert.Single(results).BaseObject);
        Assert.True(status.IsAuthenticated, status.StatusMessage);
    }

    [Fact]
    public void SendCopilotMessage_CustomToolIsInvoked()
    {
        ps.AddScript(@"
            $tool = New-CopilotTool -Name 'get_magic_word' -Description 'Returns the magic word' -ScriptBlock {
                'The magic word is xyzzy-42'
            } -SkipPermission
            $null = New-CopilotSession -AutoApprove -Tool $tool
            Send-CopilotMessage 'Call the get_magic_word tool and reply with only the magic word it returns.' -Timeout (New-TimeSpan -Seconds 120)
        ");
        var results = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        var result = Assert.IsType<CopilotMessageResult>(results[^1].BaseObject);
        Assert.Contains("xyzzy-42", result.Content);
        Assert.Contains(result.Events, e =>
            e is ToolExecutionStartEvent start && start.Data.ToolName == "get_magic_word");

        ps.Commands.Clear();
        ps.AddCommand("Close-CopilotSession");
        ps.Invoke();
    }

    [Fact]
    public void StopCopilotMessage_CompletesOnIdleSession()
    {
        ps.AddScript(@"
            $null = New-CopilotSession -AutoApprove
            Stop-CopilotMessage
        ");
        ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));

        ps.Commands.Clear();
        ps.AddCommand("Close-CopilotSession");
        ps.Invoke();
    }
}
