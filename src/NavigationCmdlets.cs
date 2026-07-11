using System.Management.Automation;
using GitHub.Copilot;

namespace CopilotCmdlets;

internal sealed class CopilotClientNavigationOperations
{
    private readonly Func<Action<SessionLifecycleEvent>, IDisposable> onLifecycle;
    private readonly Func<CancellationToken, Task<string?>> getLastSessionIdAsync;
    private readonly Func<CancellationToken, Task<string?>> getForegroundSessionIdAsync;
    private readonly Func<string, CancellationToken, Task> setForegroundSessionIdAsync;

    internal CopilotClientNavigationOperations(CopilotClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        onLifecycle = handler => client.OnLifecycle<SessionLifecycleEvent>(handler);
        getLastSessionIdAsync = client.GetLastSessionIdAsync;
        getForegroundSessionIdAsync = client.GetForegroundSessionIdAsync;
        setForegroundSessionIdAsync = client.SetForegroundSessionIdAsync;
    }

    internal CopilotClientNavigationOperations(
        Func<Action<SessionLifecycleEvent>, IDisposable> onLifecycle,
        Func<CancellationToken, Task<string?>> getLastSessionIdAsync,
        Func<CancellationToken, Task<string?>> getForegroundSessionIdAsync,
        Func<string, CancellationToken, Task> setForegroundSessionIdAsync)
    {
        ArgumentNullException.ThrowIfNull(onLifecycle);
        ArgumentNullException.ThrowIfNull(getLastSessionIdAsync);
        ArgumentNullException.ThrowIfNull(getForegroundSessionIdAsync);
        ArgumentNullException.ThrowIfNull(setForegroundSessionIdAsync);

        this.onLifecycle = onLifecycle;
        this.getLastSessionIdAsync = getLastSessionIdAsync;
        this.getForegroundSessionIdAsync = getForegroundSessionIdAsync;
        this.setForegroundSessionIdAsync = setForegroundSessionIdAsync;
    }

    internal IDisposable OnLifecycle(Action<SessionLifecycleEvent> handler)
        => onLifecycle(handler);

    internal Task<string?> GetLastSessionIdAsync(CancellationToken cancellationToken)
        => getLastSessionIdAsync(cancellationToken);

    internal Task<string?> GetForegroundSessionIdAsync(CancellationToken cancellationToken)
        => getForegroundSessionIdAsync(cancellationToken);

    internal Task SetForegroundSessionIdAsync(
        string sessionId,
        CancellationToken cancellationToken)
        => setForegroundSessionIdAsync(sessionId, cancellationToken);
}

[Cmdlet(VerbsLifecycle.Register, "CopilotSessionLifecycleEvent", DefaultParameterSetName = "ScriptBlock")]
[OutputType(typeof(IDisposable))]
public sealed class RegisterCopilotSessionLifecycleEventCmdlet : PSCmdlet
{
    private PSLanguageMode callbackLanguageMode = PSLanguageMode.FullLanguage;

    [Parameter]
    public CopilotClient? Client { get; set; }

    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ScriptBlock")]
    public ScriptBlock? Action { get; set; }

    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Delegate")]
    public Action<SessionLifecycleEvent>? ActionDelegate { get; set; }

    protected override void BeginProcessing()
    {
        callbackLanguageMode = SessionState.LanguageMode;
    }

    internal Action<SessionLifecycleEvent> BuildCallback(PSLanguageMode languageMode)
    {
        if (Action is not null && ActionDelegate is not null)
        {
            throw new ArgumentException(
                "Specify either -Action or -ActionDelegate, not both.");
        }

        if (ActionDelegate is not null)
            return ActionDelegate;

        if (Action is null)
        {
            throw new ArgumentException(
                "Specify either -Action or -ActionDelegate.");
        }

        var runner = new PowerShellCallbackRunner(Action, languageMode);
        return lifecycleEvent =>
            runner.InvokeVoidAsync(lifecycleEvent).GetAwaiter().GetResult();
    }

    internal static IDisposable InvokeOperation(
        CopilotClientNavigationOperations operations,
        Action<SessionLifecycleEvent> callback)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(callback);
        return operations.OnLifecycle(callback);
    }

    protected override void EndProcessing()
    {
        if (!ModuleState.TryRequireClient(Client, out var target, out var noClient))
        {
            ThrowTerminatingError(noClient!);
            return;
        }

        try
        {
            var callback = BuildCallback(callbackLanguageMode);
            var subscription = InvokeOperation(
                new CopilotClientNavigationOperations(target),
                callback);
            WriteObject(subscription);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex,
                "LifecycleSubscriptionFailed",
                ErrorCategory.InvalidOperation,
                target));
        }
    }
}

[Cmdlet(VerbsCommon.Get, "CopilotLastSessionId")]
[OutputType(typeof(string))]
public sealed class GetCopilotLastSessionIdCmdlet : PSCmdlet
{
    [Parameter]
    public CopilotClient? Client { get; set; }

    internal static string? InvokeOperation(
        CopilotClientNavigationOperations operations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operations);
        return operations.GetLastSessionIdAsync(cancellationToken)
            .GetAwaiter().GetResult();
    }

    protected override void EndProcessing()
    {
        if (!ModuleState.TryRequireClient(Client, out var target, out var noClient))
        {
            ThrowTerminatingError(noClient!);
            return;
        }

        try
        {
            var sessionId = InvokeOperation(
                new CopilotClientNavigationOperations(target),
                CancellationToken.None);
            if (sessionId is not null)
                WriteObject(sessionId);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex,
                "LastSessionIdReadFailed",
                ErrorCategory.ReadError,
                target));
        }
    }
}

[Cmdlet(VerbsCommon.Get, "CopilotForegroundSessionId")]
[OutputType(typeof(string))]
public sealed class GetCopilotForegroundSessionIdCmdlet : PSCmdlet
{
    [Parameter]
    public CopilotClient? Client { get; set; }

    internal static string? InvokeOperation(
        CopilotClientNavigationOperations operations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operations);
        return operations.GetForegroundSessionIdAsync(cancellationToken)
            .GetAwaiter().GetResult();
    }

    protected override void EndProcessing()
    {
        if (!ModuleState.TryRequireClient(Client, out var target, out var noClient))
        {
            ThrowTerminatingError(noClient!);
            return;
        }

        try
        {
            var sessionId = InvokeOperation(
                new CopilotClientNavigationOperations(target),
                CancellationToken.None);
            if (sessionId is not null)
                WriteObject(sessionId);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex,
                "ForegroundSessionIdReadFailed",
                ErrorCategory.ReadError,
                target));
        }
    }
}

[Cmdlet(VerbsCommon.Set, "CopilotForegroundSessionId")]
public sealed class SetCopilotForegroundSessionIdCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    [ArgumentCompleter(typeof(CopilotSessionCompleter))]
    public string SessionId { get; set; } = null!;

    [Parameter]
    public CopilotClient? Client { get; set; }

    internal static void InvokeOperation(
        CopilotClientNavigationOperations operations,
        string sessionId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operations);
        operations.SetForegroundSessionIdAsync(sessionId, cancellationToken)
            .GetAwaiter().GetResult();
    }

    protected override void EndProcessing()
    {
        if (!ModuleState.TryRequireClient(Client, out var target, out var noClient))
        {
            ThrowTerminatingError(noClient!);
            return;
        }

        try
        {
            InvokeOperation(
                new CopilotClientNavigationOperations(target),
                SessionId,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex,
                "ForegroundSessionIdSetFailed",
                ErrorCategory.WriteError,
                SessionId));
        }
    }
}
