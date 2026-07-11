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
            $null = New-CopilotSession -AutoApprove -MaxAiCredits 50 -Tool $tool
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

    [Fact]
    public void SendCopilotMessageAsync_ReceiveReturnsTaggedResult()
    {
        ps.AddScript(@"
            $null = New-CopilotSession -AutoApprove -AvailableTools @() -MaxAiCredits 50
            $handle = Send-CopilotMessageAsync 'Say exactly: async-ok' -Tag 'tag-42' -Mode enqueue
            $handle | Receive-CopilotAsyncResult -Timeout (New-TimeSpan -Seconds 120)
        ");
        var results = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        var result = Assert.IsType<CopilotMessageResult>(results[^1].BaseObject);
        Assert.Contains("async-ok", result.Content);
        Assert.Equal("tag-42", result.Tag);
        Assert.NotNull(result.MessageId);

        ps.Commands.Clear();
        ps.AddCommand("Close-CopilotSession");
        ps.Invoke();
    }

    [Fact]
    public void SendCopilotMessage_FileAttachmentIsReadable()
    {
        var attachmentPath = Path.Combine(Path.GetTempPath(), $"copilot-e2e-{Guid.NewGuid():N}.txt");
        File.WriteAllText(attachmentPath, "The secret phrase is quartz-77.");
        try
        {
            // Unlike blobs (inlined into the prompt), file attachments are
            // surfaced as references the model reads via the read tool — so
            // this session keeps the default tool set.
            ps.AddScript($@"
                $null = New-CopilotSession -AutoApprove -MaxAiCredits 50
                Send-CopilotMessage 'What is the secret phrase in the attached file? Reply with only the phrase.' -Attachment '{attachmentPath}' -Timeout (New-TimeSpan -Seconds 120)
            ");
            var results = ps.Invoke();

            Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
            var result = Assert.IsType<CopilotMessageResult>(results[^1].BaseObject);
            Assert.Contains("quartz-77", result.Content);
        }
        finally
        {
            File.Delete(attachmentPath);
            ps.Commands.Clear();
            ps.AddCommand("Close-CopilotSession");
            ps.Invoke();
        }
    }

    [Fact]
    public void SendCopilotMessage_BlobAttachmentIsReadable()
    {
        ps.AddScript(@"
            $null = New-CopilotSession -AutoApprove -AvailableTools @() -MaxAiCredits 50
            $blob = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('The magic word is zebra-99.'))
            Send-CopilotMessage 'What is the magic word in the attached document? Reply with only the word.' -BlobData $blob -BlobMimeType 'text/plain' -Timeout (New-TimeSpan -Seconds 120)
        ");
        var results = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        var result = Assert.IsType<CopilotMessageResult>(results[^1].BaseObject);
        Assert.Contains("zebra-99", result.Content);

        ps.Commands.Clear();
        ps.AddCommand("Close-CopilotSession");
        ps.Invoke();
    }

    [Fact]
    public void StopCopilotMessage_AbortsInFlightMessage()
    {
        // Start a long task without waiting, abort it, then confirm the
        // session returns to idle well before the task could finish.
        ps.AddScript(@"
            $null = New-CopilotSession -AutoApprove -AvailableTools @() -MaxAiCredits 50
            $handle = Send-CopilotMessageAsync 'Count from 1 to 500, one number per line, no shortcuts.'
            Start-Sleep -Seconds 3
            Stop-CopilotMessage
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            $null = $handle | Receive-CopilotAsyncResult -Timeout (New-TimeSpan -Seconds 60)
            $sw.Elapsed.TotalSeconds
        ");
        var results = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        var secondsToIdle = Convert.ToDouble(results[^1].BaseObject);
        Assert.True(secondsToIdle < 45, $"expected prompt idle after abort, took {secondsToIdle:F1}s");
        Assert.DoesNotContain(ps.Streams.Warning, w => w.Message.Contains("Timed out"));

        ps.Commands.Clear();
        ps.AddCommand("Close-CopilotSession");
        ps.Invoke();
    }

    [Fact]
    public void ReceiveAsyncResult_DisposeSessionClearsModuleDefault()
    {
        // After -DisposeSession disposes the module-default session, later
        // cmdlets must fail with the clean "no session" error rather than
        // hitting a disposed session.
        ps.AddScript(@"
            $null = New-CopilotSession -AutoApprove -AvailableTools @() -MaxAiCredits 50
            $handle = Send-CopilotMessageAsync 'Say exactly: bye'
            $null = $handle | Receive-CopilotAsyncResult -Timeout (New-TimeSpan -Seconds 120) -DisposeSession
            Send-CopilotMessage 'this should fail cleanly'
        ");
        ps.Invoke();

        Assert.True(ps.HadErrors, "expected a terminating error after the default session was disposed");
        var error = ps.Streams.Error.ReadAll().Last();
        Assert.Contains("No Copilot session available", error.Exception.Message);
    }

    [Fact]
    public void McpServer_ToolRoundTrip()
    {
        // Requires npx (Node) on PATH. The server version is pinned: npx -y
        // executes the package, so an unpinned spec would run whatever is
        // latest on npm at test time (supply-chain + reproducibility risk).
        ps.AddScript(@"
            $null = New-CopilotSession -AutoApprove -MaxAiCredits 50 -McpServers @{
                everything = @{ Command = 'npx'; Args = @('-y', '@modelcontextprotocol/server-everything@2026.7.4'); Tools = @('echo') }
            }
            Send-CopilotMessage ""Call the echo tool with message 'mcp-roundtrip-ok' and reply with only the text it returned."" -Timeout (New-TimeSpan -Seconds 180)
        ");
        var results = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        var result = Assert.IsType<CopilotMessageResult>(results[^1].BaseObject);
        Assert.Contains("mcp-roundtrip-ok", result.Content);
        Assert.Contains(result.Events, e =>
            e is ToolExecutionStartEvent start && start.Data.ToolName.Contains("echo"));

        ps.Commands.Clear();
        ps.AddCommand("Close-CopilotSession");
        ps.Invoke();
    }
}
