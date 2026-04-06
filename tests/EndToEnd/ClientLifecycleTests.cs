using System.Management.Automation;
using GitHub.Copilot.SDK;
using Xunit;

namespace CopilotPS.Tests.EndToEnd;

[Trait("Category", "EndToEnd")]
public class ClientLifecycleTests : IAsyncLifetime
{
    private PowerShell ps = null!;

    public Task InitializeAsync()
    {
        ps = PowerShell.Create();
        var modulePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "out", "CopilotPS.psd1");
        ps.AddCommand("Import-Module").AddParameter("Name", Path.GetFullPath(modulePath));
        ps.Invoke();
        ps.Commands.Clear();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        // Best-effort cleanup
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
    public void NewCopilotClient_CreatesAndReturnsClient()
    {
        ps.AddCommand("New-CopilotClient");

        var results = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        Assert.Single(results);
        Assert.IsType<CopilotClient>(results[0].BaseObject);
    }

    [Fact]
    public void TestCopilotConnection_ReturnsPingResponse()
    {
        ps.AddCommand("New-CopilotClient");
        ps.Invoke();
        ps.Commands.Clear();

        ps.AddCommand("Test-CopilotConnection").AddParameter("Message", "hello");
        var results = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        Assert.Single(results);
        Assert.IsType<PingResponse>(results[0].BaseObject);

        var ping = (PingResponse)results[0].BaseObject;
        Assert.NotNull(ping.Message);
    }

    [Fact]
    public void StopCopilotClient_StopsCleanly()
    {
        ps.AddCommand("New-CopilotClient");
        ps.Invoke();
        ps.Commands.Clear();

        ps.AddCommand("Stop-CopilotClient");
        ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
    }

    [Fact]
    public void StopCopilotClient_Force_StopsCleanly()
    {
        ps.AddCommand("New-CopilotClient");
        ps.Invoke();
        ps.Commands.Clear();

        ps.AddCommand("Stop-CopilotClient").AddParameter("Force", true);
        ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
    }
}
