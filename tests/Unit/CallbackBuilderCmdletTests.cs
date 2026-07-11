using System.Linq.Expressions;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using GitHub.Copilot;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.Unit;

[Trait("Category", "Unit")]
public class CallbackBuilderCmdletTests
{
    [Theory]
    [InlineData(typeof(NewCopilotSessionHooksCmdlet), "CopilotSessionHooks", typeof(SessionHooks))]
    [InlineData(typeof(NewCopilotCommandCmdlet), "CopilotCommand", typeof(CommandDefinition))]
    public void Builder_HasCorrectCmdletAndOutputType(
        Type cmdletType,
        string noun,
        Type outputType)
    {
        var cmdlet = Assert.IsType<CmdletAttribute>(
            Attribute.GetCustomAttribute(cmdletType, typeof(CmdletAttribute)));
        Assert.Equal(VerbsCommon.New, cmdlet.VerbName);
        Assert.Equal(noun, cmdlet.NounName);

        var output = Assert.Single(Attribute.GetCustomAttributes(
            cmdletType, typeof(OutputTypeAttribute)).Cast<OutputTypeAttribute>());
        Assert.Contains(output.Type, type => type.Type == outputType);
    }

    [Fact]
    public void SessionHooks_ExposesEveryTypedCallbackPair()
    {
        foreach (var (name, delegateType) in HookDelegateTypes())
        {
            var scriptBlock = typeof(NewCopilotSessionHooksCmdlet).GetProperty(name);
            Assert.NotNull(scriptBlock);
            Assert.Equal(typeof(ScriptBlock), scriptBlock!.PropertyType);
            Assert.NotNull(Attribute.GetCustomAttribute(
                scriptBlock, typeof(ParameterAttribute)));

            var callback = typeof(NewCopilotSessionHooksCmdlet)
                .GetProperty($"{name}Delegate");
            Assert.NotNull(callback);
            Assert.Equal(delegateType, callback!.PropertyType);
            Assert.NotNull(Attribute.GetCustomAttribute(
                callback, typeof(ParameterAttribute)));
        }
    }

    [Fact]
    public void Command_UsesRequiredExclusiveHandlerParameterSets()
    {
        var cmdlet = Assert.IsType<CmdletAttribute>(Attribute.GetCustomAttribute(
            typeof(NewCopilotCommandCmdlet), typeof(CmdletAttribute)));
        Assert.Equal("ScriptBlock", cmdlet.DefaultParameterSetName);

        var name = GetParameter<NewCopilotCommandCmdlet>("Name");
        Assert.True(name.Mandatory);
        Assert.Equal(0, name.Position);

        var description = GetParameter<NewCopilotCommandCmdlet>("Description");
        Assert.False(description.Mandatory);

        var scriptBlock = GetParameter<NewCopilotCommandCmdlet>("ScriptBlock");
        Assert.True(scriptBlock.Mandatory);
        Assert.Equal(1, scriptBlock.Position);
        Assert.Equal("ScriptBlock", scriptBlock.ParameterSetName);
        Assert.Equal(
            typeof(ScriptBlock),
            typeof(NewCopilotCommandCmdlet).GetProperty("ScriptBlock")!.PropertyType);

        var handler = GetParameter<NewCopilotCommandCmdlet>("HandlerDelegate");
        Assert.True(handler.Mandatory);
        Assert.Equal(1, handler.Position);
        Assert.Equal("Delegate", handler.ParameterSetName);
        Assert.Equal(
            typeof(Func<CommandContext, Task>),
            typeof(NewCopilotCommandCmdlet).GetProperty("HandlerDelegate")!.PropertyType);
    }

    [Fact]
    public void SessionHooks_UnsetCallbacksRemainNull()
    {
        var hooks = new NewCopilotSessionHooksCmdlet()
            .BuildHooks(PSLanguageMode.FullLanguage);

        Assert.Null(hooks.OnPreToolUse);
        Assert.Null(hooks.OnPreMcpToolCall);
        Assert.Null(hooks.OnPostToolUse);
        Assert.Null(hooks.OnPostToolUseFailure);
        Assert.Null(hooks.OnUserPromptSubmitted);
        Assert.Null(hooks.OnSessionStart);
        Assert.Null(hooks.OnSessionEnd);
        Assert.Null(hooks.OnErrorOccurred);
    }

    [Fact]
    public async Task SessionHooks_WiresEveryScriptBlockCallback()
    {
        var cmdlet = new NewCopilotSessionHooksCmdlet
        {
            OnPreToolUse = ScriptBlock.Create("""
                param($hookInput, $invocation)
                if ($hookInput.ToolName -ne 'pre-tool' -or $invocation.SessionId -ne 'inv') { throw 'bad pre args' }
                [GitHub.Copilot.PreToolUseHookOutput]@{ AdditionalContext = 'pre-ok' }
                """),
            OnPreMcpToolCall = ScriptBlock.Create("""
                param($hookInput, $invocation)
                if ($hookInput.ServerName -ne 'server' -or $invocation.SessionId -ne 'inv') { throw 'bad mcp args' }
                [GitHub.Copilot.PreMcpToolCallHookOutput]::new()
                """),
            OnPostToolUse = ScriptBlock.Create("""
                param($hookInput, $invocation)
                if ($hookInput.ToolName -ne 'post-tool' -or $invocation.SessionId -ne 'inv') { throw 'bad post args' }
                [GitHub.Copilot.PostToolUseHookOutput]@{ AdditionalContext = 'post-ok' }
                """),
            OnPostToolUseFailure = ScriptBlock.Create("""
                param($hookInput, $invocation)
                if ($hookInput.Error -ne 'failed' -or $invocation.SessionId -ne 'inv') { throw 'bad failure args' }
                [GitHub.Copilot.PostToolUseFailureHookOutput]@{ AdditionalContext = 'failure-ok' }
                """),
            OnUserPromptSubmitted = ScriptBlock.Create("""
                param($hookInput, $invocation)
                if ($hookInput.Prompt -ne 'prompt' -or $invocation.SessionId -ne 'inv') { throw 'bad prompt args' }
                [GitHub.Copilot.UserPromptSubmittedHookOutput]@{ ModifiedPrompt = 'prompt-ok' }
                """),
            OnSessionStart = ScriptBlock.Create("""
                param($hookInput, $invocation)
                if ($hookInput.Source -ne 'new' -or $invocation.SessionId -ne 'inv') { throw 'bad start args' }
                [GitHub.Copilot.SessionStartHookOutput]@{ AdditionalContext = 'start-ok' }
                """),
            OnSessionEnd = ScriptBlock.Create("""
                param($hookInput, $invocation)
                if ($hookInput.Reason -ne 'complete' -or $invocation.SessionId -ne 'inv') { throw 'bad end args' }
                [GitHub.Copilot.SessionEndHookOutput]@{ SessionSummary = 'end-ok' }
                """),
            OnErrorOccurred = ScriptBlock.Create("""
                param($hookInput, $invocation)
                if ($hookInput.Error -ne 'boom' -or $invocation.SessionId -ne 'inv') { throw 'bad error args' }
                [GitHub.Copilot.ErrorOccurredHookOutput]@{ UserNotification = 'error-ok' }
                """)
        };
        var hooks = cmdlet.BuildHooks(PSLanguageMode.FullLanguage);
        var invocation = new HookInvocation { SessionId = "inv" };

        var pre = await hooks.OnPreToolUse!(
            new PreToolUseHookInput { ToolName = "pre-tool" }, invocation);
        var mcp = await hooks.OnPreMcpToolCall!(
            new PreMcpToolCallHookInput { ServerName = "server" }, invocation);
        var post = await hooks.OnPostToolUse!(
            new PostToolUseHookInput { ToolName = "post-tool" }, invocation);
        var failure = await hooks.OnPostToolUseFailure!(
            new PostToolUseFailureHookInput { Error = "failed" }, invocation);
        var prompt = await hooks.OnUserPromptSubmitted!(
            new UserPromptSubmittedHookInput { Prompt = "prompt" }, invocation);
        var start = await hooks.OnSessionStart!(
            new SessionStartHookInput { Source = "new" }, invocation);
        var end = await hooks.OnSessionEnd!(
            new SessionEndHookInput { Reason = "complete" }, invocation);
        var error = await hooks.OnErrorOccurred!(
            new ErrorOccurredHookInput { Error = "boom" }, invocation);

        Assert.Equal("pre-ok", pre!.AdditionalContext);
        Assert.NotNull(mcp);
        Assert.Equal("post-ok", post!.AdditionalContext);
        Assert.Equal("failure-ok", failure!.AdditionalContext);
        Assert.Equal("prompt-ok", prompt!.ModifiedPrompt);
        Assert.Equal("start-ok", start!.AdditionalContext);
        Assert.Equal("end-ok", end!.SessionSummary);
        Assert.Equal("error-ok", error!.UserNotification);
    }

    [Fact]
    public async Task SessionHooks_OptionalCallbackAllowsNoResult()
    {
        var hooks = new NewCopilotSessionHooksCmdlet
        {
            OnPreToolUse = ScriptBlock.Create("$null")
        }.BuildHooks(PSLanguageMode.FullLanguage);

        var result = await hooks.OnPreToolUse!(null!, null!);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("'wrong-type'", "System.String")]
    [InlineData(
        "[GitHub.Copilot.PreToolUseHookOutput]::new(); [GitHub.Copilot.PreToolUseHookOutput]::new()",
        "2 values")]
    public async Task SessionHooks_PropagatesSharedRunnerResultErrors(
        string script,
        string expectedMessage)
    {
        var hooks = new NewCopilotSessionHooksCmdlet
        {
            OnPreToolUse = ScriptBlock.Create(script)
        }.BuildHooks(PSLanguageMode.FullLanguage);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => hooks.OnPreToolUse!(null!, null!));

        Assert.Contains(expectedMessage, error.Message);
    }

    [Fact]
    public void SessionHooks_PreservesEveryRawDelegateIdentity()
    {
        Func<PreToolUseHookInput, HookInvocation, Task<PreToolUseHookOutput?>> pre =
            (_, _) => Task.FromResult<PreToolUseHookOutput?>(null);
        Func<PreMcpToolCallHookInput, HookInvocation, Task<PreMcpToolCallHookOutput?>> mcp =
            (_, _) => Task.FromResult<PreMcpToolCallHookOutput?>(null);
        Func<PostToolUseHookInput, HookInvocation, Task<PostToolUseHookOutput?>> post =
            (_, _) => Task.FromResult<PostToolUseHookOutput?>(null);
        Func<PostToolUseFailureHookInput, HookInvocation, Task<PostToolUseFailureHookOutput?>> failure =
            (_, _) => Task.FromResult<PostToolUseFailureHookOutput?>(null);
        Func<UserPromptSubmittedHookInput, HookInvocation, Task<UserPromptSubmittedHookOutput?>> prompt =
            (_, _) => Task.FromResult<UserPromptSubmittedHookOutput?>(null);
        Func<SessionStartHookInput, HookInvocation, Task<SessionStartHookOutput?>> start =
            (_, _) => Task.FromResult<SessionStartHookOutput?>(null);
        Func<SessionEndHookInput, HookInvocation, Task<SessionEndHookOutput?>> end =
            (_, _) => Task.FromResult<SessionEndHookOutput?>(null);
        Func<ErrorOccurredHookInput, HookInvocation, Task<ErrorOccurredHookOutput?>> error =
            (_, _) => Task.FromResult<ErrorOccurredHookOutput?>(null);
        var hooks = new NewCopilotSessionHooksCmdlet
        {
            OnPreToolUseDelegate = pre,
            OnPreMcpToolCallDelegate = mcp,
            OnPostToolUseDelegate = post,
            OnPostToolUseFailureDelegate = failure,
            OnUserPromptSubmittedDelegate = prompt,
            OnSessionStartDelegate = start,
            OnSessionEndDelegate = end,
            OnErrorOccurredDelegate = error
        }.BuildHooks(PSLanguageMode.FullLanguage);

        Assert.Same(pre, hooks.OnPreToolUse);
        Assert.Same(mcp, hooks.OnPreMcpToolCall);
        Assert.Same(post, hooks.OnPostToolUse);
        Assert.Same(failure, hooks.OnPostToolUseFailure);
        Assert.Same(prompt, hooks.OnUserPromptSubmitted);
        Assert.Same(start, hooks.OnSessionStart);
        Assert.Same(end, hooks.OnSessionEnd);
        Assert.Same(error, hooks.OnErrorOccurred);
    }

    [Theory]
    [InlineData("OnPreToolUse")]
    [InlineData("OnPreMcpToolCall")]
    [InlineData("OnPostToolUse")]
    [InlineData("OnPostToolUseFailure")]
    [InlineData("OnUserPromptSubmitted")]
    [InlineData("OnSessionStart")]
    [InlineData("OnSessionEnd")]
    [InlineData("OnErrorOccurred")]
    public void SessionHooks_RejectsEveryScriptBlockDelegateConflict(string parameterName)
    {
        var cmdlet = new NewCopilotSessionHooksCmdlet();
        typeof(NewCopilotSessionHooksCmdlet).GetProperty(parameterName)!
            .SetValue(cmdlet, ScriptBlock.Create("$null"));
        var delegateProperty = typeof(NewCopilotSessionHooksCmdlet)
            .GetProperty($"{parameterName}Delegate")!;
        delegateProperty.SetValue(cmdlet, CreateDelegate(delegateProperty.PropertyType));

        var error = Assert.Throws<ArgumentException>(
            () => cmdlet.BuildHooks(PSLanguageMode.FullLanguage));

        Assert.Contains($"-{parameterName}", error.Message);
        Assert.Contains($"-{parameterName}Delegate", error.Message);
    }

    [Fact]
    public async Task Command_MapsScriptBlockAndContext()
    {
        var command = new NewCopilotCommandCmdlet
        {
            Name = "deploy",
            Description = "Deploy the app",
            ScriptBlock = ScriptBlock.Create("""
                param($context)
                if ($context.CommandName -ne 'deploy' -or $context.Args -ne 'production') {
                    throw 'bad command context'
                }
                1
                2
                """)
        }.BuildCommand(PSLanguageMode.FullLanguage);

        await command.Handler(new CommandContext
        {
            CommandName = "deploy",
            Args = "production"
        });

        Assert.Equal("deploy", command.Name);
        Assert.Equal("Deploy the app", command.Description);
    }

    [Fact]
    public void Command_MapsOptionalDescriptionAsNull()
    {
        var command = new NewCopilotCommandCmdlet
        {
            Name = "plain",
            ScriptBlock = ScriptBlock.Create("$null")
        }.BuildCommand(PSLanguageMode.FullLanguage);

        Assert.Null(command.Description);
    }

    [Fact]
    public void Command_PreservesRawHandlerIdentity()
    {
        Func<CommandContext, Task> handler = _ => Task.CompletedTask;

        var command = new NewCopilotCommandCmdlet
        {
            Name = "raw",
            HandlerDelegate = handler
        }.BuildCommand(PSLanguageMode.FullLanguage);

        Assert.Same(handler, command.Handler);
    }

    [Fact]
    public async Task Command_HandlerExceptionsFlowUnchanged()
    {
        var command = new NewCopilotCommandCmdlet
        {
            Name = "fail",
            ScriptBlock = ScriptBlock.Create("throw 'command-boom'")
        }.BuildCommand(PSLanguageMode.FullLanguage);

        var error = await Assert.ThrowsAnyAsync<Exception>(
            () => command.Handler(new CommandContext()));

        Assert.Contains("command-boom", error.Message);
    }

    [Fact]
    public void Command_RejectsScriptBlockDelegateConflict()
    {
        var cmdlet = new NewCopilotCommandCmdlet
        {
            Name = "conflict",
            ScriptBlock = ScriptBlock.Create("$null"),
            HandlerDelegate = _ => Task.CompletedTask
        };

        var error = Assert.Throws<ArgumentException>(
            () => cmdlet.BuildCommand(PSLanguageMode.FullLanguage));

        Assert.Contains("-ScriptBlock", error.Message);
        Assert.Contains("-HandlerDelegate", error.Message);
    }

    [Fact]
    public async Task Command_UsesInvokingSessionLanguageMode()
    {
        var state = InitialSessionState.CreateDefault2();
        state.LanguageMode = PSLanguageMode.ConstrainedLanguage;
        state.Commands.Add(new SessionStateCmdletEntry(
            "New-CopilotCommand", typeof(NewCopilotCommandCmdlet), null));
        using var ps = PowerShell.Create(state);
        ps.AddCommand("New-CopilotCommand")
            .AddParameter("Name", "restricted")
            .AddParameter(
                "ScriptBlock",
                ScriptBlock.Create("[System.IO.File]::Exists('.')"));

        var output = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        var command = Assert.IsType<CommandDefinition>(Assert.Single(output).BaseObject);
        var error = await Assert.ThrowsAnyAsync<Exception>(
            () => command.Handler(new CommandContext()));
        Assert.Contains("language mode", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<(string Name, Type DelegateType)> HookDelegateTypes()
    {
        yield return (
            "OnPreToolUse",
            typeof(Func<PreToolUseHookInput, HookInvocation, Task<PreToolUseHookOutput>>));
        yield return (
            "OnPreMcpToolCall",
            typeof(Func<PreMcpToolCallHookInput, HookInvocation, Task<PreMcpToolCallHookOutput>>));
        yield return (
            "OnPostToolUse",
            typeof(Func<PostToolUseHookInput, HookInvocation, Task<PostToolUseHookOutput>>));
        yield return (
            "OnPostToolUseFailure",
            typeof(Func<PostToolUseFailureHookInput, HookInvocation, Task<PostToolUseFailureHookOutput>>));
        yield return (
            "OnUserPromptSubmitted",
            typeof(Func<UserPromptSubmittedHookInput, HookInvocation, Task<UserPromptSubmittedHookOutput>>));
        yield return (
            "OnSessionStart",
            typeof(Func<SessionStartHookInput, HookInvocation, Task<SessionStartHookOutput>>));
        yield return (
            "OnSessionEnd",
            typeof(Func<SessionEndHookInput, HookInvocation, Task<SessionEndHookOutput>>));
        yield return (
            "OnErrorOccurred",
            typeof(Func<ErrorOccurredHookInput, HookInvocation, Task<ErrorOccurredHookOutput>>));
    }

    private static ParameterAttribute GetParameter<TCmdlet>(string propertyName)
        => Assert.IsType<ParameterAttribute>(Attribute.GetCustomAttribute(
            typeof(TCmdlet).GetProperty(propertyName)!,
            typeof(ParameterAttribute)));

    private static Delegate CreateDelegate(Type delegateType)
    {
        var invoke = delegateType.GetMethod("Invoke")!;
        var parameters = invoke.GetParameters()
            .Select(parameter => Expression.Parameter(parameter.ParameterType))
            .ToArray();
        return Expression.Lambda(
                delegateType,
                Expression.Default(invoke.ReturnType),
                parameters)
            .Compile();
    }
}
