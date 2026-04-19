using System.Diagnostics;
using System.Management.Automation;
using GitHub.Copilot.SDK;

namespace CopilotCmdlets;

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
    [ArgumentCompleter(typeof(LogLevelCompleter))]
    public string LogLevel { get; set; } = "info";

    [Parameter]
    public string? OtlpEndpoint { get; set; }

    [Parameter]
    public string? TelemetrySourceName { get; set; }

    protected override void EndProcessing()
    {
        var options = new CopilotClientOptions
        {
            LogLevel = LogLevel
        };

        if (OtlpEndpoint is not null || TelemetrySourceName is not null)
        {
            options.Telemetry = new TelemetryConfig
            {
                OtlpEndpoint = OtlpEndpoint,
                SourceName = TelemetrySourceName
            };
        }

        if (GitHubToken is not null) options.GitHubToken = GitHubToken;
        if (CliPath is not null)
        {
            options.CliPath = CliPath;
        }
        else
        {
            var bundled = ModuleState.ResolveBundledCliPath();
            if (bundled is not null)
                options.CliPath = bundled;
        }
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

        if (ModuleState.Client is not null)
        {
            WriteWarning("Replacing existing Copilot client. The previous client is still running; use Stop-CopilotClient to clean it up.");
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
        if (!ModuleState.TryRequireClient(Client, out var target, out var noClient))
        {
            ThrowTerminatingError(noClient!);
            return;
        }

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
        if (!ModuleState.TryRequireClient(Client, out var target, out var noClient))
        {
            ThrowTerminatingError(noClient!);
            return;
        }

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

[Cmdlet(VerbsCommunications.Connect, "Copilot")]
public sealed class ConnectCopilotCmdlet : PSCmdlet
{
    [Parameter]
    public string? CliPath { get; set; }

    [Parameter]
    public string[]? ArgumentList { get; set; }

    protected override void EndProcessing()
    {
        var cli = CliPath ?? ModuleState.ResolveBundledCliPath();
        if (cli is null)
        {
            ThrowTerminatingError(new ErrorRecord(
                new FileNotFoundException(
                    "Could not locate the bundled Copilot CLI. Pass -CliPath to specify one explicitly."),
                "CliNotFound", ErrorCategory.ObjectNotFound, null));
            return;
        }

        if (ModuleState.Client is not null)
        {
            WriteWarning(
                "A Copilot client is already running. Stop it with Stop-CopilotClient before logging in " +
                "to avoid two processes contending for the same credential store.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = cli,
            UseShellExecute = false
        };

        if (ArgumentList is not null)
        {
            foreach (var arg in ArgumentList)
                psi.ArgumentList.Add(arg);
        }

        WriteVerbose($"Launching Copilot CLI: {cli}");
        Host.UI.WriteLine("Launching GitHub Copilot CLI. Type `/login` to authenticate, then `/exit` to return.");

        try
        {
            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start Copilot CLI process.");
            proc.WaitForExit();

            if (proc.ExitCode != 0)
            {
                WriteWarning($"Copilot CLI exited with code {proc.ExitCode}.");
            }
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "ConnectCopilotFailed", ErrorCategory.NotSpecified, cli));
        }
    }
}
