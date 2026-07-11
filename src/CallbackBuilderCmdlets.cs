using System.Management.Automation;
using GitHub.Copilot;

namespace CopilotCmdlets;

[Cmdlet(VerbsCommon.New, "CopilotSessionHooks")]
[OutputType(typeof(SessionHooks))]
public sealed class NewCopilotSessionHooksCmdlet : PSCmdlet
{
    [Parameter]
    public ScriptBlock? OnPreToolUse { get; set; }

    [Parameter]
    public Func<PreToolUseHookInput, HookInvocation, Task<PreToolUseHookOutput?>>?
        OnPreToolUseDelegate { get; set; }

    [Parameter]
    public ScriptBlock? OnPreMcpToolCall { get; set; }

    [Parameter]
    public Func<PreMcpToolCallHookInput, HookInvocation, Task<PreMcpToolCallHookOutput?>>?
        OnPreMcpToolCallDelegate { get; set; }

    [Parameter]
    public ScriptBlock? OnPostToolUse { get; set; }

    [Parameter]
    public Func<PostToolUseHookInput, HookInvocation, Task<PostToolUseHookOutput?>>?
        OnPostToolUseDelegate { get; set; }

    [Parameter]
    public ScriptBlock? OnPostToolUseFailure { get; set; }

    [Parameter]
    public Func<PostToolUseFailureHookInput, HookInvocation, Task<PostToolUseFailureHookOutput?>>?
        OnPostToolUseFailureDelegate { get; set; }

    [Parameter]
    public ScriptBlock? OnUserPromptSubmitted { get; set; }

    [Parameter]
    public Func<UserPromptSubmittedHookInput, HookInvocation, Task<UserPromptSubmittedHookOutput?>>?
        OnUserPromptSubmittedDelegate { get; set; }

    [Parameter]
    public ScriptBlock? OnSessionStart { get; set; }

    [Parameter]
    public Func<SessionStartHookInput, HookInvocation, Task<SessionStartHookOutput?>>?
        OnSessionStartDelegate { get; set; }

    [Parameter]
    public ScriptBlock? OnSessionEnd { get; set; }

    [Parameter]
    public Func<SessionEndHookInput, HookInvocation, Task<SessionEndHookOutput?>>?
        OnSessionEndDelegate { get; set; }

    [Parameter]
    public ScriptBlock? OnErrorOccurred { get; set; }

    [Parameter]
    public Func<ErrorOccurredHookInput, HookInvocation, Task<ErrorOccurredHookOutput?>>?
        OnErrorOccurredDelegate { get; set; }

    internal SessionHooks BuildHooks(PSLanguageMode languageMode)
    {
        return new SessionHooks
        {
            OnPreToolUse = BuildCallback(
                OnPreToolUse,
                OnPreToolUseDelegate,
                nameof(OnPreToolUse),
                languageMode,
                runner => (input, invocation) =>
                    runner.InvokeOptionalAsync<PreToolUseHookOutput>(input, invocation)),
            OnPreMcpToolCall = BuildCallback(
                OnPreMcpToolCall,
                OnPreMcpToolCallDelegate,
                nameof(OnPreMcpToolCall),
                languageMode,
                runner => (input, invocation) =>
                    runner.InvokeOptionalAsync<PreMcpToolCallHookOutput>(input, invocation)),
            OnPostToolUse = BuildCallback(
                OnPostToolUse,
                OnPostToolUseDelegate,
                nameof(OnPostToolUse),
                languageMode,
                runner => (input, invocation) =>
                    runner.InvokeOptionalAsync<PostToolUseHookOutput>(input, invocation)),
            OnPostToolUseFailure = BuildCallback(
                OnPostToolUseFailure,
                OnPostToolUseFailureDelegate,
                nameof(OnPostToolUseFailure),
                languageMode,
                runner => (input, invocation) =>
                    runner.InvokeOptionalAsync<PostToolUseFailureHookOutput>(input, invocation)),
            OnUserPromptSubmitted = BuildCallback(
                OnUserPromptSubmitted,
                OnUserPromptSubmittedDelegate,
                nameof(OnUserPromptSubmitted),
                languageMode,
                runner => (input, invocation) =>
                    runner.InvokeOptionalAsync<UserPromptSubmittedHookOutput>(input, invocation)),
            OnSessionStart = BuildCallback(
                OnSessionStart,
                OnSessionStartDelegate,
                nameof(OnSessionStart),
                languageMode,
                runner => (input, invocation) =>
                    runner.InvokeOptionalAsync<SessionStartHookOutput>(input, invocation)),
            OnSessionEnd = BuildCallback(
                OnSessionEnd,
                OnSessionEndDelegate,
                nameof(OnSessionEnd),
                languageMode,
                runner => (input, invocation) =>
                    runner.InvokeOptionalAsync<SessionEndHookOutput>(input, invocation)),
            OnErrorOccurred = BuildCallback(
                OnErrorOccurred,
                OnErrorOccurredDelegate,
                nameof(OnErrorOccurred),
                languageMode,
                runner => (input, invocation) =>
                    runner.InvokeOptionalAsync<ErrorOccurredHookOutput>(input, invocation))
        };
    }

    protected override void EndProcessing()
    {
        try
        {
            WriteObject(BuildHooks(SessionState.LanguageMode));
        }
        catch (ArgumentException ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "InvalidSessionHooks", ErrorCategory.InvalidArgument, null));
        }
    }

    private static TDelegate? BuildCallback<TDelegate>(
        ScriptBlock? scriptBlock,
        TDelegate? callback,
        string parameterName,
        PSLanguageMode languageMode,
        Func<PowerShellCallbackRunner, TDelegate> build)
        where TDelegate : Delegate
    {
        if (scriptBlock is not null && callback is not null)
        {
            throw new ArgumentException(
                $"Specify either -{parameterName} or -{parameterName}Delegate, not both.");
        }

        if (callback is not null)
            return callback;
        if (scriptBlock is null)
            return null;

        return build(new PowerShellCallbackRunner(scriptBlock, languageMode));
    }
}

[Cmdlet(
    VerbsCommon.New,
    "CopilotCommand",
    DefaultParameterSetName = "ScriptBlock")]
[OutputType(typeof(CommandDefinition))]
public sealed class NewCopilotCommandCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Name { get; set; } = null!;

    [Parameter]
    public string? Description { get; set; }

    [Parameter(
        Mandatory = true,
        Position = 1,
        ParameterSetName = "ScriptBlock")]
    public ScriptBlock? ScriptBlock { get; set; }

    [Parameter(
        Mandatory = true,
        Position = 1,
        ParameterSetName = "Delegate")]
    public Func<CommandContext, Task>? HandlerDelegate { get; set; }

    internal CommandDefinition BuildCommand(PSLanguageMode languageMode)
    {
        if (ScriptBlock is not null && HandlerDelegate is not null)
        {
            throw new ArgumentException(
                "Specify either -ScriptBlock or -HandlerDelegate, not both.");
        }

        var handler = HandlerDelegate;
        if (handler is null)
        {
            var runner = new PowerShellCallbackRunner(ScriptBlock!, languageMode);
            handler = context => runner.InvokeVoidAsync(context);
        }

        return new CommandDefinition
        {
            Name = Name,
            Description = Description,
            Handler = handler
        };
    }

    protected override void EndProcessing()
    {
        try
        {
            WriteObject(BuildCommand(SessionState.LanguageMode));
        }
        catch (ArgumentException ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "InvalidCommandDefinition", ErrorCategory.InvalidArgument, null));
        }
    }
}
