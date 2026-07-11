#pragma warning disable GHCP001 // experimental SDK members: Session.Ui
using System.Management.Automation;
using GitHub.Copilot;

namespace CopilotCmdlets;

internal static class SessionUiExecution
{
    internal static Task<bool> ConfirmAsync(
        ISessionUiApi ui,
        string message,
        CancellationToken cancellationToken)
        => ui.ConfirmAsync(message, cancellationToken);

    internal static Task<string?> SelectAsync(
        ISessionUiApi ui,
        string message,
        string[] options,
        CancellationToken cancellationToken)
        => ui.SelectAsync(message, options, cancellationToken);

    internal static Task<string?> InputAsync(
        ISessionUiApi ui,
        string message,
        UiInputOptions? options,
        CancellationToken cancellationToken)
        => ui.InputAsync(message, options, cancellationToken);

    internal static Task<ElicitationResult> ElicitAsync(
        ISessionUiApi ui,
        ElicitationParams parameters,
        CancellationToken cancellationToken)
        => ui.ElicitAsync(parameters, cancellationToken);
}

[Cmdlet(VerbsLifecycle.Confirm, "CopilotElicitation")]
[OutputType(typeof(bool))]
public sealed class ConfirmCopilotElicitationCmdlet : PSCmdlet
{
    private CancellationTokenSource? cancellationSource;

    [Parameter(Mandatory = true, Position = 0)]
    public string Message { get; set; } = null!;

    [Parameter]
    [CopilotSessionTransformation]
    [ArgumentCompleter(typeof(CopilotSessionCompleter))]
    public CopilotSession? Session { get; set; }

    protected override void StopProcessing()
    {
        cancellationSource?.Cancel();
    }

    protected override void EndProcessing()
    {
        if (!ModuleState.TryRequireSession(Session, out var target, out var noSession))
        {
            ThrowTerminatingError(noSession!);
            return;
        }

        using var cancellation = new CancellationTokenSource();
        cancellationSource = cancellation;
        try
        {
            var confirmed = SessionUiExecution.ConfirmAsync(
                    target.Ui, Message, cancellation.Token)
                .GetAwaiter().GetResult();
            WriteObject(confirmed);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw new PipelineStoppedException();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "ConfirmElicitationFailed", ErrorCategory.InvalidOperation, Message));
        }
        finally
        {
            cancellationSource = null;
        }
    }
}

[Cmdlet(VerbsCommon.Select, "CopilotElicitation")]
[OutputType(typeof(string))]
public sealed class SelectCopilotElicitationCmdlet : PSCmdlet
{
    private CancellationTokenSource? cancellationSource;

    [Parameter(Mandatory = true, Position = 0)]
    public string Message { get; set; } = null!;

    [Parameter(Mandatory = true, Position = 1)]
    public string[] Option { get; set; } = null!;

    [Parameter]
    [CopilotSessionTransformation]
    [ArgumentCompleter(typeof(CopilotSessionCompleter))]
    public CopilotSession? Session { get; set; }

    protected override void StopProcessing()
    {
        cancellationSource?.Cancel();
    }

    protected override void EndProcessing()
    {
        if (!ModuleState.TryRequireSession(Session, out var target, out var noSession))
        {
            ThrowTerminatingError(noSession!);
            return;
        }

        using var cancellation = new CancellationTokenSource();
        cancellationSource = cancellation;
        try
        {
            var selection = SessionUiExecution.SelectAsync(
                    target.Ui, Message, Option, cancellation.Token)
                .GetAwaiter().GetResult();
            WriteObject(selection);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw new PipelineStoppedException();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "SelectElicitationFailed", ErrorCategory.InvalidOperation, Message));
        }
        finally
        {
            cancellationSource = null;
        }
    }
}

[Cmdlet(VerbsCommunications.Read, "CopilotElicitationInput")]
[OutputType(typeof(string))]
public sealed class ReadCopilotElicitationInputCmdlet : PSCmdlet
{
    private CancellationTokenSource? cancellationSource;

    [Parameter(Mandatory = true, Position = 0)]
    public string Message { get; set; } = null!;

    [Parameter]
    public UiInputOptions? Options { get; set; }

    [Parameter]
    [CopilotSessionTransformation]
    [ArgumentCompleter(typeof(CopilotSessionCompleter))]
    public CopilotSession? Session { get; set; }

    protected override void StopProcessing()
    {
        cancellationSource?.Cancel();
    }

    protected override void EndProcessing()
    {
        if (!ModuleState.TryRequireSession(Session, out var target, out var noSession))
        {
            ThrowTerminatingError(noSession!);
            return;
        }

        using var cancellation = new CancellationTokenSource();
        cancellationSource = cancellation;
        try
        {
            var input = SessionUiExecution.InputAsync(
                    target.Ui, Message, Options, cancellation.Token)
                .GetAwaiter().GetResult();
            WriteObject(input);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw new PipelineStoppedException();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "ReadElicitationInputFailed", ErrorCategory.InvalidOperation, Message));
        }
        finally
        {
            cancellationSource = null;
        }
    }
}

[Cmdlet(VerbsLifecycle.Request, "CopilotElicitation")]
[OutputType(typeof(ElicitationResult))]
public sealed class RequestCopilotElicitationCmdlet : PSCmdlet
{
    private CancellationTokenSource? cancellationSource;

    [Parameter(Mandatory = true, Position = 0)]
    public ElicitationParams Parameters { get; set; } = null!;

    [Parameter]
    [CopilotSessionTransformation]
    [ArgumentCompleter(typeof(CopilotSessionCompleter))]
    public CopilotSession? Session { get; set; }

    protected override void StopProcessing()
    {
        cancellationSource?.Cancel();
    }

    protected override void EndProcessing()
    {
        if (!ModuleState.TryRequireSession(Session, out var target, out var noSession))
        {
            ThrowTerminatingError(noSession!);
            return;
        }

        using var cancellation = new CancellationTokenSource();
        cancellationSource = cancellation;
        try
        {
            var result = SessionUiExecution.ElicitAsync(
                    target.Ui, Parameters, cancellation.Token)
                .GetAwaiter().GetResult();
            WriteObject(result);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw new PipelineStoppedException();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "RequestElicitationFailed", ErrorCategory.InvalidOperation, Parameters));
        }
        finally
        {
            cancellationSource = null;
        }
    }
}
