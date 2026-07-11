using System.Management.Automation;
using System.Reflection;
using GitHub.Copilot;
using NSubstitute;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.Unit;

[Trait("Category", "Unit")]
[Collection("ModuleState")]
public class NavigationCmdletTests : IDisposable
{
    public NavigationCmdletTests()
    {
        ModuleState.Client = null;
        ModuleState.CurrentSession = null;
    }

    public void Dispose()
    {
        ModuleState.Client = null;
        ModuleState.CurrentSession = null;
    }

    [Theory]
    [InlineData(typeof(RegisterCopilotSessionLifecycleEventCmdlet), VerbsLifecycle.Register, "CopilotSessionLifecycleEvent")]
    [InlineData(typeof(GetCopilotLastSessionIdCmdlet), VerbsCommon.Get, "CopilotLastSessionId")]
    [InlineData(typeof(GetCopilotForegroundSessionIdCmdlet), VerbsCommon.Get, "CopilotForegroundSessionId")]
    [InlineData(typeof(SetCopilotForegroundSessionIdCmdlet), VerbsCommon.Set, "CopilotForegroundSessionId")]
    public void Cmdlets_HaveExpectedNames(Type cmdletType, string verb, string noun)
    {
        var attribute = cmdletType.GetCustomAttribute<CmdletAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(verb, attribute.VerbName);
        Assert.Equal(noun, attribute.NounName);
    }

    [Theory]
    [InlineData(typeof(RegisterCopilotSessionLifecycleEventCmdlet))]
    [InlineData(typeof(GetCopilotLastSessionIdCmdlet))]
    [InlineData(typeof(GetCopilotForegroundSessionIdCmdlet))]
    [InlineData(typeof(SetCopilotForegroundSessionIdCmdlet))]
    public void Cmdlets_HaveOptionalClientParameter(Type cmdletType)
    {
        var property = cmdletType.GetProperty("Client");
        var parameter = property?.GetCustomAttribute<ParameterAttribute>();

        Assert.NotNull(property);
        Assert.Equal(typeof(CopilotClient), property.PropertyType);
        Assert.NotNull(parameter);
        Assert.False(parameter.Mandatory);
    }

    [Fact]
    public void RegisterLifecycle_HasMutuallyExclusiveCallbackParameterSets()
    {
        var cmdlet = typeof(RegisterCopilotSessionLifecycleEventCmdlet)
            .GetCustomAttribute<CmdletAttribute>();
        var action = typeof(RegisterCopilotSessionLifecycleEventCmdlet)
            .GetProperty(nameof(RegisterCopilotSessionLifecycleEventCmdlet.Action))!;
        var actionDelegate = typeof(RegisterCopilotSessionLifecycleEventCmdlet)
            .GetProperty(nameof(RegisterCopilotSessionLifecycleEventCmdlet.ActionDelegate))!;
        var actionParameter = action.GetCustomAttribute<ParameterAttribute>();
        var delegateParameter = actionDelegate.GetCustomAttribute<ParameterAttribute>();

        Assert.NotNull(cmdlet);
        Assert.Equal("ScriptBlock", cmdlet.DefaultParameterSetName);
        Assert.Equal(typeof(ScriptBlock), action.PropertyType);
        Assert.NotNull(actionParameter);
        Assert.True(actionParameter.Mandatory);
        Assert.Equal(0, actionParameter.Position);
        Assert.Equal("ScriptBlock", actionParameter.ParameterSetName);
        Assert.Equal(typeof(Action<SessionLifecycleEvent>), actionDelegate.PropertyType);
        Assert.NotNull(delegateParameter);
        Assert.True(delegateParameter.Mandatory);
        Assert.Equal("Delegate", delegateParameter.ParameterSetName);
    }

    [Fact]
    public void Cmdlets_HaveExpectedOutputMetadata()
    {
        AssertOutputType<RegisterCopilotSessionLifecycleEventCmdlet>(typeof(IDisposable));
        AssertOutputType<GetCopilotLastSessionIdCmdlet>(typeof(string));
        AssertOutputType<GetCopilotForegroundSessionIdCmdlet>(typeof(string));
        Assert.Empty(typeof(SetCopilotForegroundSessionIdCmdlet)
            .GetCustomAttributes<OutputTypeAttribute>());
    }

    [Fact]
    public void SetForegroundSessionId_HasMandatoryCompletedSessionId()
    {
        var property = typeof(SetCopilotForegroundSessionIdCmdlet)
            .GetProperty(nameof(SetCopilotForegroundSessionIdCmdlet.SessionId))!;
        var parameter = property.GetCustomAttribute<ParameterAttribute>();
        var completer = property.GetCustomAttribute<ArgumentCompleterAttribute>();

        Assert.Equal(typeof(string), property.PropertyType);
        Assert.NotNull(parameter);
        Assert.True(parameter.Mandatory);
        Assert.Equal(0, parameter.Position);
        Assert.NotNull(completer);
        Assert.Equal(typeof(CopilotSessionCompleter), completer.Type);
    }

    [Fact]
    public async Task Operations_DelegateAllFourSdkCallsWithoutChangingArguments()
    {
        var substitutes = new OperationSubstitutes();
        var operations = substitutes.CreateOperations();
        var callback = new Action<SessionLifecycleEvent>(_ => { });
        var subscription = Substitute.For<IDisposable>();
        using var source = new CancellationTokenSource();
        var cancellationToken = source.Token;

        substitutes.OnLifecycle.Invoke(callback).Returns(subscription);
        substitutes.GetLastSessionIdAsync.Invoke(cancellationToken)
            .Returns(Task.FromResult<string?>("last-session"));
        substitutes.GetForegroundSessionIdAsync.Invoke(cancellationToken)
            .Returns(Task.FromResult<string?>("foreground-session"));
        substitutes.SetForegroundSessionIdAsync.Invoke("target-session", cancellationToken)
            .Returns(Task.CompletedTask);

        Assert.Same(subscription, operations.OnLifecycle(callback));
        Assert.Equal("last-session", await operations.GetLastSessionIdAsync(cancellationToken));
        Assert.Equal(
            "foreground-session",
            await operations.GetForegroundSessionIdAsync(cancellationToken));
        await operations.SetForegroundSessionIdAsync("target-session", cancellationToken);

        substitutes.OnLifecycle.Received(1).Invoke(callback);
        await substitutes.GetLastSessionIdAsync.Received(1).Invoke(cancellationToken);
        await substitutes.GetForegroundSessionIdAsync.Received(1).Invoke(cancellationToken);
        await substitutes.SetForegroundSessionIdAsync.Received(1)
            .Invoke("target-session", cancellationToken);
    }

    [Fact]
    public void RegisterLifecycle_RawDelegateAndSubscriptionPreserveIdentity()
    {
        var substitutes = new OperationSubstitutes();
        var operations = substitutes.CreateOperations();
        var callback = new Action<SessionLifecycleEvent>(_ => { });
        var subscription = Substitute.For<IDisposable>();
        var cmdlet = new RegisterCopilotSessionLifecycleEventCmdlet
        {
            ActionDelegate = callback
        };
        substitutes.OnLifecycle.Invoke(callback).Returns(subscription);

        var builtCallback = cmdlet.BuildCallback(PSLanguageMode.FullLanguage);
        var result = RegisterCopilotSessionLifecycleEventCmdlet.InvokeOperation(
            operations,
            builtCallback);

        Assert.Same(callback, builtCallback);
        Assert.Same(subscription, result);
        substitutes.OnLifecycle.Received(1).Invoke(callback);
    }

    [Fact]
    public void RegisterLifecycle_RejectsBothCallbackForms()
    {
        var cmdlet = new RegisterCopilotSessionLifecycleEventCmdlet
        {
            Action = ScriptBlock.Create(""),
            ActionDelegate = _ => { }
        };

        var error = Assert.Throws<ArgumentException>(
            () => cmdlet.BuildCallback(PSLanguageMode.FullLanguage));

        Assert.Contains("-Action", error.Message);
        Assert.Contains("-ActionDelegate", error.Message);
    }

    [Fact]
    public void RegisterLifecycle_RequiresOneCallbackForm()
    {
        var cmdlet = new RegisterCopilotSessionLifecycleEventCmdlet();

        var error = Assert.Throws<ArgumentException>(
            () => cmdlet.BuildCallback(PSLanguageMode.FullLanguage));

        Assert.Contains("-Action", error.Message);
        Assert.Contains("-ActionDelegate", error.Message);
    }

    [Fact]
    public void RegisterLifecycle_ScriptBlockReceivesLifecycleEventSynchronously()
    {
        var cmdlet = new RegisterCopilotSessionLifecycleEventCmdlet
        {
            Action = ScriptBlock.Create(
                "param($lifecycleEvent) $lifecycleEvent.SessionId = 'callback-ran'")
        };
        var callback = cmdlet.BuildCallback(PSLanguageMode.FullLanguage);
        var lifecycleEvent = new SessionLifecycleEvent
        {
            Type = "session.created",
            SessionId = "before"
        };

        callback(lifecycleEvent);

        Assert.Equal("callback-ran", lifecycleEvent.SessionId);
    }

    [Fact]
    public void RegisterLifecycle_ScriptBlockPreservesLanguageMode()
    {
        var cmdlet = new RegisterCopilotSessionLifecycleEventCmdlet
        {
            Action = ScriptBlock.Create("[System.IO.File]::Exists('.')")
        };
        var callback = cmdlet.BuildCallback(PSLanguageMode.ConstrainedLanguage);

        var error = Assert.ThrowsAny<Exception>(
            () => callback(new SessionLifecycleEvent()));

        Assert.Contains("language mode", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RegisterLifecycle_DoesNotSwallowScriptBlockErrorsLocally()
    {
        var cmdlet = new RegisterCopilotSessionLifecycleEventCmdlet
        {
            Action = ScriptBlock.Create("throw 'lifecycle callback failed'")
        };
        var callback = cmdlet.BuildCallback(PSLanguageMode.FullLanguage);

        var error = Assert.ThrowsAny<Exception>(
            () => callback(new SessionLifecycleEvent()));

        Assert.Contains("lifecycle callback failed", error.Message);
    }

    [Fact]
    public void RegisterLifecycle_DoesNotStoreSubscriptionInModuleStateOrCmdlet()
    {
        var substitutes = new OperationSubstitutes();
        var operations = substitutes.CreateOperations();
        var subscription = Substitute.For<IDisposable>();
        var callback = new Action<SessionLifecycleEvent>(_ => { });
        var cmdlet = new RegisterCopilotSessionLifecycleEventCmdlet
        {
            ActionDelegate = callback
        };
        using var client = new CopilotClient(new CopilotClientOptions());
        ModuleState.Client = client;
        substitutes.OnLifecycle.Invoke(callback).Returns(subscription);

        var result = RegisterCopilotSessionLifecycleEventCmdlet.InvokeOperation(
            operations,
            cmdlet.BuildCallback(PSLanguageMode.FullLanguage));
        var moduleStateFields = typeof(ModuleState).GetFields(
            BindingFlags.Static | BindingFlags.NonPublic);
        var cmdletFields = typeof(RegisterCopilotSessionLifecycleEventCmdlet).GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.Same(subscription, result);
        Assert.Same(client, ModuleState.Client);
        Assert.All(moduleStateFields, field => Assert.NotSame(subscription, field.GetValue(null)));
        Assert.All(cmdletFields, field => Assert.NotSame(subscription, field.GetValue(cmdlet)));
    }

    [Fact]
    public void Operations_AreInstanceBasedAndImmutable()
    {
        var fields = typeof(CopilotClientNavigationOperations).GetFields(
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotEmpty(fields);
        Assert.All(fields, field =>
        {
            Assert.False(field.IsStatic);
            Assert.True(field.IsInitOnly);
        });
    }

    [Fact]
    public void GetLastSessionId_DelegatesAndReturnsNullUnchanged()
    {
        var substitutes = new OperationSubstitutes();
        var operations = substitutes.CreateOperations();
        using var source = new CancellationTokenSource();
        var cancellationToken = source.Token;
        substitutes.GetLastSessionIdAsync.Invoke(cancellationToken)
            .Returns(Task.FromResult<string?>(null));

        var result = GetCopilotLastSessionIdCmdlet.InvokeOperation(
            operations,
            cancellationToken);

        Assert.Null(result);
        substitutes.GetLastSessionIdAsync.Received(1).Invoke(cancellationToken);
    }

    [Fact]
    public void GetForegroundSessionId_DelegatesAndReturnsNullUnchanged()
    {
        var substitutes = new OperationSubstitutes();
        var operations = substitutes.CreateOperations();
        using var source = new CancellationTokenSource();
        var cancellationToken = source.Token;
        substitutes.GetForegroundSessionIdAsync.Invoke(cancellationToken)
            .Returns(Task.FromResult<string?>(null));

        var result = GetCopilotForegroundSessionIdCmdlet.InvokeOperation(
            operations,
            cancellationToken);

        Assert.Null(result);
        substitutes.GetForegroundSessionIdAsync.Received(1).Invoke(cancellationToken);
    }

    [Fact]
    public void GetLastSessionId_PropagatesCancellationWithoutAggregateException()
    {
        var substitutes = new OperationSubstitutes();
        var operations = substitutes.CreateOperations();
        using var source = new CancellationTokenSource();
        source.Cancel();
        substitutes.GetLastSessionIdAsync.Invoke(source.Token)
            .Returns(Task.FromCanceled<string?>(source.Token));

        var error = Assert.ThrowsAny<OperationCanceledException>(
            () => GetCopilotLastSessionIdCmdlet.InvokeOperation(
                operations,
                source.Token));

        Assert.Equal(source.Token, error.CancellationToken);
        substitutes.GetLastSessionIdAsync.Received(1).Invoke(source.Token);
    }

    [Fact]
    public void GetForegroundSessionId_PropagatesSdkErrorUnchanged()
    {
        var substitutes = new OperationSubstitutes();
        var operations = substitutes.CreateOperations();
        var expected = new InvalidOperationException("foreground read failed");
        substitutes.GetForegroundSessionIdAsync.Invoke(CancellationToken.None)
            .Returns(Task.FromException<string?>(expected));

        var error = Assert.Throws<InvalidOperationException>(
            () => GetCopilotForegroundSessionIdCmdlet.InvokeOperation(
                operations,
                CancellationToken.None));

        Assert.Same(expected, error);
    }

    [Fact]
    public void SetForegroundSessionId_DelegatesAndPropagatesSdkErrorUnchanged()
    {
        var substitutes = new OperationSubstitutes();
        var operations = substitutes.CreateOperations();
        var expected = new InvalidOperationException("foreground write failed");
        substitutes.SetForegroundSessionIdAsync
            .Invoke("target-session", CancellationToken.None)
            .Returns(Task.FromException(expected));

        var error = Assert.Throws<InvalidOperationException>(
            () => SetCopilotForegroundSessionIdCmdlet.InvokeOperation(
                operations,
                "target-session",
                CancellationToken.None));

        Assert.Same(expected, error);
        substitutes.SetForegroundSessionIdAsync.Received(1)
            .Invoke("target-session", CancellationToken.None);
    }

    private static void AssertOutputType<TCmdlet>(Type expectedType)
    {
        var attributes = typeof(TCmdlet).GetCustomAttributes<OutputTypeAttribute>();
        var attribute = Assert.Single(attributes);
        Assert.Contains(attribute.Type, type => type.Type == expectedType);
    }

    private sealed class OperationSubstitutes
    {
        internal Func<Action<SessionLifecycleEvent>, IDisposable> OnLifecycle { get; }
            = Substitute.For<Func<Action<SessionLifecycleEvent>, IDisposable>>();

        internal Func<CancellationToken, Task<string?>> GetLastSessionIdAsync { get; }
            = Substitute.For<Func<CancellationToken, Task<string?>>>();

        internal Func<CancellationToken, Task<string?>> GetForegroundSessionIdAsync { get; }
            = Substitute.For<Func<CancellationToken, Task<string?>>>();

        internal Func<string, CancellationToken, Task> SetForegroundSessionIdAsync { get; }
            = Substitute.For<Func<string, CancellationToken, Task>>();

        internal CopilotClientNavigationOperations CreateOperations()
            => new(
                OnLifecycle,
                GetLastSessionIdAsync,
                GetForegroundSessionIdAsync,
                SetForegroundSessionIdAsync);
    }
}
