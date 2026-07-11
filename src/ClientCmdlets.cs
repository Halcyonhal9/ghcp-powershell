using System.Collections;
using System.Diagnostics;
using System.Management.Automation;
using GitHub.Copilot;

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
    public string? LogLevel { get; set; }

    [Parameter]
    public string? OtlpEndpoint { get; set; }

    [Parameter]
    public string? TelemetrySourceName { get; set; }

    [Parameter]
    public string? WorkingDirectory { get; set; }

    [Parameter]
    public Hashtable? Environment { get; set; }

    [Parameter]
    public SwitchParameter UseLoggedInUser { get; set; }

    protected override void EndProcessing()
    {
        var options = new CopilotClientOptions();

        if (LogLevel is not null) options.LogLevel = new CopilotLogLevel(LogLevel);

        if (OtlpEndpoint is not null || TelemetrySourceName is not null)
        {
            options.Telemetry = new TelemetryConfig
            {
                OtlpEndpoint = OtlpEndpoint,
                SourceName = TelemetrySourceName
            };
        }

        if (GitHubToken is not null) options.GitHubToken = GitHubToken;
        if (WorkingDirectory is not null)
            options.WorkingDirectory = GetUnresolvedProviderPathFromPSPath(WorkingDirectory);
        if (UseLoggedInUser.IsPresent) options.UseLoggedInUser = true;

        if (Environment is not null)
        {
            var env = new Dictionary<string, string>();
            foreach (DictionaryEntry entry in Environment)
            {
                env[entry.Key.ToString()!] = McpServerHelper.Unwrap(entry.Value)?.ToString() ?? string.Empty;
            }
            options.Environment = env;
        }

        if (CliUrl is not null)
        {
            options.Connection = RuntimeConnection.ForUri(CliUrl);
        }
        else
        {
            // The SDK resolves its bundled CLI relative to the host application
            // (pwsh), so always pass the module-relative path explicitly.
            var cli = CliPath ?? ModuleState.ResolveBundledCliPath();
            if (cli is not null)
                options.Connection = RuntimeConnection.ForStdio(cli);
        }

        CopilotClient client;
        try
        {
            client = new CopilotClient(options);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "ClientOptionsInvalid", ErrorCategory.InvalidArgument, null));
            return;
        }

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
            var response = target.PingAsync(Message, CancellationToken.None)
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

[Cmdlet(VerbsCommon.Get, "CopilotStatus")]
[OutputType(typeof(GetStatusResponse))]
public sealed class GetCopilotStatusCmdlet : PSCmdlet
{
    [Parameter]
    public CopilotClient? Client { get; set; }

    protected override void EndProcessing()
    {
        if (!ModuleState.TryRequireClient(Client, out var target, out var noClient))
        {
            ThrowTerminatingError(noClient!);
            return;
        }

        try
        {
            var status = target.GetStatusAsync(CancellationToken.None)
                .GetAwaiter().GetResult();
            WriteObject(status);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "GetStatusFailed", ErrorCategory.ConnectionError, target));
        }
    }
}

[Cmdlet(VerbsCommon.Get, "CopilotAuthStatus")]
[OutputType(typeof(GetAuthStatusResponse))]
public sealed class GetCopilotAuthStatusCmdlet : PSCmdlet
{
    [Parameter]
    public CopilotClient? Client { get; set; }

    protected override void EndProcessing()
    {
        if (!ModuleState.TryRequireClient(Client, out var target, out var noClient))
        {
            ThrowTerminatingError(noClient!);
            return;
        }

        try
        {
            var status = target.GetAuthStatusAsync(CancellationToken.None)
                .GetAwaiter().GetResult();
            WriteObject(status);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "GetAuthStatusFailed", ErrorCategory.ConnectionError, target));
        }
    }
}

[Cmdlet(VerbsCommunications.Connect, "Copilot", SupportsShouldProcess = true)]
public sealed class ConnectCopilotCmdlet : PSCmdlet
{
    [Parameter]
    public string? CliPath { get; set; }

    [Parameter]
    public string[]? ArgumentList { get; set; }

    [Parameter]
    public SwitchParameter Force { get; set; }

    private Process? _runningProc;

    protected override void StopProcessing()
    {
        try { _runningProc?.Kill(entireProcessTree: true); } catch { /* ignore */ }
    }

    protected override void EndProcessing()
    {
        var cli = CliPath ?? ModuleState.ResolveBundledCliPath();
        if (cli is null)
        {
            ThrowTerminatingError(new ErrorRecord(
                new FileNotFoundException(ModuleState.BuildMissingCliMessage()),
                "CliNotFound", ErrorCategory.ObjectNotFound, null));
            return;
        }

        if (ModuleState.Client is not null)
        {
            const string warning =
                "A Copilot client is already running. Two CLI processes will contend for the same " +
                "credential store, which can corrupt cached login state. Stop the existing client " +
                "with Stop-CopilotClient before continuing.";
            if (!Force.IsPresent && !ShouldContinue(warning, "Connect-Copilot"))
                return;
            WriteWarning(warning);
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
        WriteInformation(new InformationRecord(
            "Launching GitHub Copilot CLI. Type `/login` to authenticate, then `/exit` to return.",
            "Connect-Copilot"), new[] { "PSHOST" });

        try
        {
            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start Copilot CLI process.");
            _runningProc = proc;
            try
            {
                proc.WaitForExit();
            }
            finally
            {
                _runningProc = null;
            }

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
