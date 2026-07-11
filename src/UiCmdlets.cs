#pragma warning disable GHCP001 // experimental SDK members: Session.Ui
using System.Management.Automation;
using GitHub.Copilot;

namespace CopilotCmdlets;

internal static class SessionUiExecution
{
    internal static Task<bool> ConfirmAsync(ISessionUiApi ui, string message)
        => ui.ConfirmAsync(message, CancellationToken.None);

    internal static Task<string?> SelectAsync(
        ISessionUiApi ui,
        string message,
        string[] options)
        => ui.SelectAsync(message, options, CancellationToken.None);

    internal static Task<string?> InputAsync(
        ISessionUiApi ui,
        string message,
        UiInputOptions? options)
        => ui.InputAsync(message, options, CancellationToken.None);

    internal static Task<ElicitationResult> ElicitAsync(
        ISessionUiApi ui,
        ElicitationParams parameters)
        => ui.ElicitAsync(parameters, CancellationToken.None);
}

[Cmdlet(VerbsLifecycle.Confirm, "CopilotElicitation")]
[OutputType(typeof(bool))]
public sealed class ConfirmCopilotElicitationCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Message { get; set; } = null!;

    [Parameter]
    [CopilotSessionTransformation]
    [ArgumentCompleter(typeof(CopilotSessionCompleter))]
    public CopilotSession? Session { get; set; }

    protected override void EndProcessing()
    {
        if (!ModuleState.TryRequireSession(Session, out var target, out var noSession))
        {
            ThrowTerminatingError(noSession!);
            return;
        }

        try
        {
            var confirmed = SessionUiExecution.ConfirmAsync(target.Ui, Message)
                .GetAwaiter().GetResult();
            WriteObject(confirmed);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "ConfirmElicitationFailed", ErrorCategory.InvalidOperation, Message));
        }
    }
}

[Cmdlet(VerbsCommon.Select, "CopilotElicitation")]
[OutputType(typeof(string))]
public sealed class SelectCopilotElicitationCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Message { get; set; } = null!;

    [Parameter(Mandatory = true, Position = 1)]
    public string[] Option { get; set; } = null!;

    [Parameter]
    [CopilotSessionTransformation]
    [ArgumentCompleter(typeof(CopilotSessionCompleter))]
    public CopilotSession? Session { get; set; }

    protected override void EndProcessing()
    {
        if (!ModuleState.TryRequireSession(Session, out var target, out var noSession))
        {
            ThrowTerminatingError(noSession!);
            return;
        }

        try
        {
            var selection = SessionUiExecution.SelectAsync(target.Ui, Message, Option)
                .GetAwaiter().GetResult();
            WriteObject(selection);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "SelectElicitationFailed", ErrorCategory.InvalidOperation, Message));
        }
    }
}

[Cmdlet(VerbsCommunications.Read, "CopilotElicitationInput")]
[OutputType(typeof(string))]
public sealed class ReadCopilotElicitationInputCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Message { get; set; } = null!;

    [Parameter]
    public UiInputOptions? Options { get; set; }

    [Parameter]
    [CopilotSessionTransformation]
    [ArgumentCompleter(typeof(CopilotSessionCompleter))]
    public CopilotSession? Session { get; set; }

    protected override void EndProcessing()
    {
        if (!ModuleState.TryRequireSession(Session, out var target, out var noSession))
        {
            ThrowTerminatingError(noSession!);
            return;
        }

        try
        {
            var input = SessionUiExecution.InputAsync(target.Ui, Message, Options)
                .GetAwaiter().GetResult();
            WriteObject(input);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "ReadElicitationInputFailed", ErrorCategory.InvalidOperation, Message));
        }
    }
}

[Cmdlet(VerbsLifecycle.Request, "CopilotElicitation")]
[OutputType(typeof(ElicitationResult))]
public sealed class RequestCopilotElicitationCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public ElicitationParams Parameters { get; set; } = null!;

    [Parameter]
    [CopilotSessionTransformation]
    [ArgumentCompleter(typeof(CopilotSessionCompleter))]
    public CopilotSession? Session { get; set; }

    protected override void EndProcessing()
    {
        if (!ModuleState.TryRequireSession(Session, out var target, out var noSession))
        {
            ThrowTerminatingError(noSession!);
            return;
        }

        try
        {
            var result = SessionUiExecution.ElicitAsync(target.Ui, Parameters)
                .GetAwaiter().GetResult();
            WriteObject(result);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "RequestElicitationFailed", ErrorCategory.InvalidOperation, Parameters));
        }
    }
}
