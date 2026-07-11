#pragma warning disable GHCP001 // experimental SDK members: Session.Ui and ToolSet
using System.Management.Automation;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.EndToEnd;

[Trait("Category", "EndToEnd")]
[Collection("EndToEnd")]
public class UiToolSetTests : IAsyncLifetime
{
    private static readonly TimeSpan SetupTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(30);

    private readonly string sessionId = $"ui-tools-{Guid.NewGuid():N}";
    private PowerShell ps = null!;

    public async Task InitializeAsync()
    {
        ps = PowerShell.Create();
        var assembly = Path.ChangeExtension(E2eModule.ResolveManifest(), ".dll");
        ps.AddCommand("Import-Module").AddParameter("Name", assembly);
        await InvokeAsync(SetupTimeout);
        ResetCommand();

        ps.AddCommand("New-CopilotClient");
        await InvokeAsync(SetupTimeout);
        ResetCommand();
    }

    public async Task DisposeAsync()
    {
        await TryInvokeAsync(
            shell => shell.AddCommand("Close-CopilotSession"),
            OperationTimeout);
        await TryInvokeAsync(
            shell => shell.AddCommand("Remove-CopilotSession")
                .AddParameter("SessionId", sessionId)
                .AddParameter("Confirm", false),
            OperationTimeout);
        await TryInvokeAsync(
            shell => shell.AddCommand("Stop-CopilotClient")
                .AddParameter("Force", true),
            SetupTimeout);

        ps.Dispose();
    }

    [Fact]
    public async Task UiCmdlets_RoundTripThroughHeadlessElicitationProvider()
    {
        ps.AddScript($$"""
            $session = New-CopilotSession -SessionId '{{sessionId}}' -AutoApprove -AvailableTools @() -OnElicitationRequest {
                param($context)

                $result = [GitHub.Copilot.ElicitationResult]::new()
                $result.Action = [GitHub.Copilot.Rpc.UIElicitationResponseAction]::Accept
                $content = [System.Collections.Generic.Dictionary[string, object]]::new()

                if ($context.RequestedSchema.Properties.ContainsKey('confirmed')) {
                    $content.Add('confirmed', $true)
                }
                elseif ($context.RequestedSchema.Properties.ContainsKey('selection')) {
                    $content.Add('selection', 'beta')
                }
                elseif ($context.RequestedSchema.Properties.ContainsKey('value')) {
                    $content.Add('value', 'typed value')
                }
                else {
                    $content.Add('name', 'Mona')
                }

                $result.Content = $content
                $result
            }
            $session
            """);
        var createOutput = await InvokeAsync(SetupTimeout);
        var session = Assert.IsType<CopilotSession>(Assert.Single(createOutput).BaseObject);
        Assert.True(session.Capabilities.Ui?.Elicitation);
        ResetCommand();

        ps.AddCommand("Confirm-CopilotElicitation")
            .AddParameter("Message", "Confirm?")
            .AddParameter("Session", session);
        var confirmOutput = await InvokeAsync(OperationTimeout);
        Assert.True(Assert.IsType<bool>(Assert.Single(confirmOutput).BaseObject));
        ResetCommand();

        ps.AddCommand("Select-CopilotElicitation")
            .AddParameter("Message", "Choose")
            .AddParameter("Option", new[] { "alpha", "beta" });
        var selectOutput = await InvokeAsync(OperationTimeout);
        Assert.Equal("beta", Assert.Single(selectOutput).BaseObject);
        ResetCommand();

        ps.AddCommand("Read-CopilotElicitationInput")
            .AddParameter("Message", "Enter value")
            .AddParameter("Options", new UiInputOptions
            {
                Title = "Value",
                Description = "A value to test",
                MinLength = 1,
                MaxLength = 20,
                Default = "default"
            })
            .AddParameter("Session", session);
        var inputOutput = await InvokeAsync(OperationTimeout);
        Assert.Equal("typed value", Assert.Single(inputOutput).BaseObject);
        ResetCommand();

        ps.AddCommand("Request-CopilotElicitation")
            .AddParameter("Parameters", new ElicitationParams
            {
                Message = "Name?",
                RequestedSchema = new ElicitationSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, object>
                    {
                        ["name"] = new Dictionary<string, object> { ["type"] = "string" }
                    },
                    Required = ["name"]
                }
            });
        var requestOutput = await InvokeAsync(OperationTimeout);
        var result = Assert.IsType<ElicitationResult>(Assert.Single(requestOutput).BaseObject);
        Assert.Equal(UIElicitationResponseAction.Accept, result.Action);
        Assert.Equal("Mona", result.Content!["name"].ToString());
    }

    [Fact]
    public async Task ToolSet_CanCreateSessionWithoutModelCall()
    {
        ps.AddCommand("New-CopilotToolSet")
            .AddParameter("Isolated", true)
            .AddParameter("Custom", new[] { "*" })
            .AddParameter("Mcp", new[] { "*" });
        var toolOutput = await InvokeAsync(OperationTimeout);
        var toolSet = Assert.IsType<ToolSet>(Assert.Single(toolOutput).BaseObject);
        Assert.NotEmpty(toolSet);
        ResetCommand();

        ps.AddCommand("New-CopilotSession")
            .AddParameter("SessionId", sessionId)
            .AddParameter("AutoApprove", true)
            .AddParameter("AvailableTools", toolSet);
        var sessionOutput = await InvokeAsync(SetupTimeout);

        var session = Assert.IsType<CopilotSession>(Assert.Single(sessionOutput).BaseObject);
        Assert.Equal(sessionId, session.SessionId);
    }

    private async Task<PSDataCollection<PSObject>> InvokeAsync(TimeSpan timeout)
    {
        try
        {
            var output = await ps.InvokeAsync().WaitAsync(timeout);
            Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
            return output;
        }
        catch (TimeoutException)
        {
            try { ps.Stop(); } catch { }
            throw new TimeoutException(
                $"PowerShell invocation exceeded the {timeout.TotalSeconds:F0}-second timeout.");
        }
    }

    private async Task TryInvokeAsync(Action<PowerShell> configure, TimeSpan timeout)
    {
        try
        {
            ResetCommand();
            configure(ps);
            await ps.InvokeAsync().WaitAsync(timeout);
        }
        catch
        {
            try { ps.Stop(); } catch { }
        }
    }

    private void ResetCommand()
    {
        ps.Commands.Clear();
        ps.Streams.Error.Clear();
        ps.Streams.Warning.Clear();
    }
}
