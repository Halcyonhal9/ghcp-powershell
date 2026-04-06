using System.Management.Automation;
using GitHub.Copilot.SDK;

namespace CopilotPS;

[Cmdlet(VerbsCommon.New, "CopilotClient")]
[OutputType(typeof(CopilotClient))]
public sealed class NewCopilotClientCmdlet : PSCmdlet
{
    [Parameter]
    public string? GitHubToken { get; set; }

    [Parameter]
    public string? CliPath { get; set; }

    [Parameter]
    public string? CliUrl { get; set; }

    [Parameter]
    public string LogLevel { get; set; } = "info";

    protected override void EndProcessing()
    {
        var options = new CopilotClientOptions
        {
            LogLevel = LogLevel
        };

        if (GitHubToken is not null) options.GitHubToken = GitHubToken;
        if (CliPath is not null) options.CliPath = CliPath;
        if (CliUrl is not null)
        {
            options.CliUrl = CliUrl;
            options.UseStdio = false;
        }

        var client = new CopilotClient(options);

        try
        {
            client.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            client.Dispose();
            ThrowTerminatingError(new ErrorRecord(
                ex, "ClientStartFailed", ErrorCategory.ConnectionError, null));
            return;
        }

        ModuleState.Client = client;
        WriteObject(client);
    }
}

[Cmdlet(VerbsLifecycle.Stop, "CopilotClient", SupportsShouldProcess = true)]
public sealed class StopCopilotClientCmdlet : PSCmdlet
{
    [Parameter]
    public CopilotClient? Client { get; set; }

    [Parameter]
    public SwitchParameter Force { get; set; }

    protected override void EndProcessing()
    {
        var target = ModuleState.RequireClient(Client);

        if (!ShouldProcess("CopilotClient", Force ? "ForceStop" : "Stop"))
            return;

        try
        {
            if (Force)
                target.ForceStopAsync().GetAwaiter().GetResult();
            else
                target.StopAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(
                ex, "ClientStopFailed", ErrorCategory.CloseError, target));
            return;
        }

        target.Dispose();

        if (ReferenceEquals(target, ModuleState.Client))
        {
            ModuleState.Client = null;
            ModuleState.CurrentSession = null;
        }
    }
}

[Cmdlet(VerbsDiagnostic.Test, "CopilotConnection")]
[OutputType(typeof(PingResponse))]
public sealed class TestCopilotConnectionCmdlet : PSCmdlet
{
    [Parameter]
    public CopilotClient? Client { get; set; }

    [Parameter]
    public string? Message { get; set; }

    protected override void EndProcessing()
    {
        var target = ModuleState.RequireClient(Client);

        try
        {
            var response = target.PingAsync(Message ?? string.Empty, CancellationToken.None)
                .GetAwaiter().GetResult();
            WriteObject(response);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "PingFailed", ErrorCategory.ConnectionError, target));
        }
    }
}
