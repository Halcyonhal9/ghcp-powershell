#pragma warning disable GHCP001 // experimental SDK members: Session.Ui
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using GitHub.Copilot;
using NSubstitute;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.Unit;

[Trait("Category", "Unit")]
[Collection("ModuleState")]
public class UiCmdletTests : IDisposable
{
    public UiCmdletTests()
    {
        ModuleState.Client = null;
        ModuleState.CurrentSession = null;
    }

    public void Dispose()
    {
        ModuleState.Client = null;
        ModuleState.CurrentSession = null;
    }

    [Fact]
    public void UiCmdlets_HaveExpectedMetadata()
    {
        var cases = new[]
        {
            (Type: typeof(ConfirmCopilotElicitationCmdlet), Verb: VerbsLifecycle.Confirm,
                Noun: "CopilotElicitation", Output: typeof(bool)),
            (Type: typeof(SelectCopilotElicitationCmdlet), Verb: VerbsCommon.Select,
                Noun: "CopilotElicitation", Output: typeof(string)),
            (Type: typeof(ReadCopilotElicitationInputCmdlet), Verb: VerbsCommunications.Read,
                Noun: "CopilotElicitationInput", Output: typeof(string)),
            (Type: typeof(RequestCopilotElicitationCmdlet), Verb: VerbsLifecycle.Request,
                Noun: "CopilotElicitation", Output: typeof(ElicitationResult)),
        };

        foreach (var item in cases)
        {
            var cmdlet = Assert.IsType<CmdletAttribute>(
                Attribute.GetCustomAttribute(item.Type, typeof(CmdletAttribute)));
            Assert.Equal(item.Verb, cmdlet.VerbName);
            Assert.Equal(item.Noun, cmdlet.NounName);

            var output = Assert.Single(Attribute.GetCustomAttributes(
                item.Type, typeof(OutputTypeAttribute)).Cast<OutputTypeAttribute>());
            Assert.Contains(output.Type, type => type.Type == item.Output);
        }
    }

    [Fact]
    public void UiCmdlets_HaveExpectedParameters()
    {
        AssertParameter<ConfirmCopilotElicitationCmdlet>(
            "Message", typeof(string), mandatory: true, position: 0);
        AssertParameter<SelectCopilotElicitationCmdlet>(
            "Message", typeof(string), mandatory: true, position: 0);
        AssertParameter<SelectCopilotElicitationCmdlet>(
            "Option", typeof(string[]), mandatory: true, position: 1);
        AssertParameter<ReadCopilotElicitationInputCmdlet>(
            "Message", typeof(string), mandatory: true, position: 0);
        AssertParameter<ReadCopilotElicitationInputCmdlet>(
            "Options", typeof(UiInputOptions), mandatory: false);
        AssertParameter<RequestCopilotElicitationCmdlet>(
            "Parameters", typeof(ElicitationParams), mandatory: true, position: 0);

        foreach (var cmdletType in new[]
        {
            typeof(ConfirmCopilotElicitationCmdlet),
            typeof(SelectCopilotElicitationCmdlet),
            typeof(ReadCopilotElicitationInputCmdlet),
            typeof(RequestCopilotElicitationCmdlet),
        })
        {
            var session = cmdletType.GetProperty("Session")!;
            Assert.Equal(typeof(CopilotSession), session.PropertyType);
            Assert.NotNull(Attribute.GetCustomAttribute(
                session, typeof(CopilotSessionTransformationAttribute)));
            var completer = Assert.IsType<ArgumentCompleterAttribute>(
                Attribute.GetCustomAttribute(session, typeof(ArgumentCompleterAttribute)));
            Assert.Equal(typeof(CopilotSessionCompleter), completer.Type);
        }
    }

    [Fact]
    public void UiCmdlets_BindParametersBeforeUsingModuleStateFallback()
    {
        var cases = new (string Name, Type Type, Action<PowerShell> AddParameters)[]
        {
            ("Confirm-CopilotElicitation", typeof(ConfirmCopilotElicitationCmdlet),
                ps => ps.AddParameter("Message", "confirm")),
            ("Select-CopilotElicitation", typeof(SelectCopilotElicitationCmdlet),
                ps => ps.AddParameter("Message", "select").AddParameter("Option", new[] { "a", "b" })),
            ("Read-CopilotElicitationInput", typeof(ReadCopilotElicitationInputCmdlet),
                ps => ps.AddParameter("Message", "input")
                    .AddParameter("Options", new UiInputOptions { Title = "Value" })),
            ("Request-CopilotElicitation", typeof(RequestCopilotElicitationCmdlet),
                ps => ps.AddParameter("Parameters", new ElicitationParams
                {
                    Message = "request",
                    RequestedSchema = new ElicitationSchema()
                })),
        };

        foreach (var item in cases)
        {
            using var ps = CreateShell((item.Name, item.Type));
            ps.AddCommand(item.Name);
            item.AddParameters(ps);

            var error = InvokeExpectingTerminatingError(ps);

            Assert.StartsWith("NoSession", error.FullyQualifiedErrorId);
            Assert.Equal(ErrorCategory.InvalidOperation, error.CategoryInfo.Category);
        }
    }

    [Fact]
    public async Task SessionUiExecution_ForwardsArgumentsAndReturnsSdkResults()
    {
        var ui = Substitute.For<ISessionUiApi>();
        using var cancellation = new CancellationTokenSource();
        var cancellationToken = cancellation.Token;
        var options = new UiInputOptions { Title = "Input" };
        var choices = new[] { "alpha", "beta" };
        var parameters = new ElicitationParams
        {
            Message = "request",
            RequestedSchema = new ElicitationSchema()
        };
        var elicitation = new ElicitationResult();

        ui.ConfirmAsync("confirm", cancellationToken)
            .Returns(Task.FromResult(true));
        ui.SelectAsync("select", choices, cancellationToken)
            .Returns(Task.FromResult<string?>("beta"));
        ui.InputAsync("input", options, cancellationToken)
            .Returns(Task.FromResult<string?>("typed"));
        ui.ElicitAsync(parameters, cancellationToken)
            .Returns(Task.FromResult(elicitation));

        Assert.True(await SessionUiExecution.ConfirmAsync(ui, "confirm", cancellationToken));
        Assert.Equal("beta", await SessionUiExecution.SelectAsync(
            ui, "select", choices, cancellationToken));
        Assert.Equal("typed", await SessionUiExecution.InputAsync(
            ui, "input", options, cancellationToken));
        Assert.Same(elicitation, await SessionUiExecution.ElicitAsync(
            ui, parameters, cancellationToken));

        await ui.Received(1).ConfirmAsync("confirm", cancellationToken);
        await ui.Received(1).SelectAsync("select", choices, cancellationToken);
        await ui.Received(1).InputAsync("input", options, cancellationToken);
        await ui.Received(1).ElicitAsync(parameters, cancellationToken);
    }

    [Fact]
    public async Task SessionUiExecution_PreservesNullSdkResults()
    {
        var ui = Substitute.For<ISessionUiApi>();
        var choices = new[] { "alpha", "beta" };

        ui.SelectAsync("select", choices, CancellationToken.None)
            .Returns(Task.FromResult<string?>(null));
        ui.InputAsync("input", null, CancellationToken.None)
            .Returns(Task.FromResult<string?>(null));

        Assert.Null(await SessionUiExecution.SelectAsync(
            ui, "select", choices, CancellationToken.None));
        Assert.Null(await SessionUiExecution.InputAsync(
            ui, "input", null, CancellationToken.None));
    }

    [Fact]
    public async Task SessionUiExecution_PropagatesSdkExceptions()
    {
        var ui = Substitute.For<ISessionUiApi>();
        var choices = new[] { "alpha" };
        var parameters = new ElicitationParams
        {
            Message = "request",
            RequestedSchema = new ElicitationSchema()
        };
        var confirmError = new InvalidOperationException("confirm failed");
        var selectError = new InvalidOperationException("select failed");
        var inputError = new InvalidOperationException("input failed");
        var requestError = new InvalidOperationException("request failed");

        ui.ConfirmAsync("confirm", CancellationToken.None)
            .Returns(Task.FromException<bool>(confirmError));
        ui.SelectAsync("select", choices, CancellationToken.None)
            .Returns(Task.FromException<string?>(selectError));
        ui.InputAsync("input", null, CancellationToken.None)
            .Returns(Task.FromException<string?>(inputError));
        ui.ElicitAsync(parameters, CancellationToken.None)
            .Returns(Task.FromException<ElicitationResult>(requestError));

        Assert.Same(confirmError, await Assert.ThrowsAsync<InvalidOperationException>(
            () => SessionUiExecution.ConfirmAsync(ui, "confirm", CancellationToken.None)));
        Assert.Same(selectError, await Assert.ThrowsAsync<InvalidOperationException>(
            () => SessionUiExecution.SelectAsync(
                ui, "select", choices, CancellationToken.None)));
        Assert.Same(inputError, await Assert.ThrowsAsync<InvalidOperationException>(
            () => SessionUiExecution.InputAsync(
                ui, "input", null, CancellationToken.None)));
        Assert.Same(requestError, await Assert.ThrowsAsync<InvalidOperationException>(
            () => SessionUiExecution.ElicitAsync(
                ui, parameters, CancellationToken.None)));
    }

    [Theory]
    [InlineData(typeof(ConfirmCopilotElicitationCmdlet))]
    [InlineData(typeof(SelectCopilotElicitationCmdlet))]
    [InlineData(typeof(ReadCopilotElicitationInputCmdlet))]
    [InlineData(typeof(RequestCopilotElicitationCmdlet))]
    public void UiCmdlets_StopProcessingCancelsPendingOperation(Type cmdletType)
    {
        var cmdlet = Assert.IsAssignableFrom<PSCmdlet>(Activator.CreateInstance(cmdletType));
        using var cancellation = new CancellationTokenSource();
        var field = cmdletType.GetField(
            "cancellationSource",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        var stopProcessing = cmdletType.GetMethod(
            "StopProcessing",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.NotNull(stopProcessing);
        field.SetValue(cmdlet, cancellation);

        stopProcessing.Invoke(cmdlet, null);

        Assert.True(cancellation.IsCancellationRequested);
    }

    private static void AssertParameter<TCmdlet>(
        string name,
        Type expectedType,
        bool mandatory,
        int? position = null)
    {
        var property = typeof(TCmdlet).GetProperty(name)!;
        Assert.Equal(expectedType, property.PropertyType);
        var parameter = Assert.IsType<ParameterAttribute>(
            Attribute.GetCustomAttribute(property, typeof(ParameterAttribute)));
        Assert.Equal(mandatory, parameter.Mandatory);
        if (position is not null)
            Assert.Equal(position, parameter.Position);
    }

    private static PowerShell CreateShell(params (string Name, Type Type)[] cmdlets)
    {
        var state = InitialSessionState.CreateDefault2();
        foreach (var (name, type) in cmdlets)
        {
            state.Commands.Add(new SessionStateCmdletEntry(name, type, null));
        }
        return PowerShell.Create(state);
    }

    private static ErrorRecord InvokeExpectingTerminatingError(PowerShell ps)
    {
        var exception = Assert.ThrowsAny<RuntimeException>(() => ps.Invoke());
        return exception.ErrorRecord;
    }
}
