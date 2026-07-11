#pragma warning disable GHCP001 // experimental SDK members: BearerTokenProvider, NamedProviderConfig
using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using GitHub.Copilot;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.Unit;

[Trait("Category", "Unit")]
public class ProviderBuilderCmdletTests
{
    [Theory]
    [InlineData(typeof(NewCopilotProviderCmdlet), "CopilotProvider", typeof(ProviderConfig))]
    [InlineData(typeof(NewCopilotNamedProviderCmdlet), "CopilotNamedProvider", typeof(NamedProviderConfig))]
    public void Builder_HasCorrectCmdletAndOutputType(
        Type cmdletType,
        string noun,
        Type outputType)
    {
        var cmdlet = Assert.IsType<CmdletAttribute>(
            Attribute.GetCustomAttribute(cmdletType, typeof(CmdletAttribute)));
        Assert.Equal(VerbsCommon.New, cmdlet.VerbName);
        Assert.Equal(noun, cmdlet.NounName);
        Assert.Equal("ScriptBlock", cmdlet.DefaultParameterSetName);

        var output = Assert.Single(Attribute.GetCustomAttributes(
            cmdletType, typeof(OutputTypeAttribute)).Cast<OutputTypeAttribute>());
        Assert.Contains(output.Type, type => type.Type == outputType);
    }

    [Fact]
    public void Provider_ExposesEverySdkPropertyWithExactTypes()
    {
        AssertParameterTypes<NewCopilotProviderCmdlet>(
            ("Type", typeof(string)),
            ("WireApi", typeof(string)),
            ("Transport", typeof(string)),
            ("BaseUrl", typeof(string)),
            ("ApiKey", typeof(string)),
            ("BearerToken", typeof(string)),
            ("BearerTokenProvider", typeof(ScriptBlock)),
            ("BearerTokenProviderDelegate", typeof(Func<ProviderTokenArgs, Task<string>>)),
            ("Azure", typeof(AzureOptions)),
            ("Headers", typeof(IDictionary)),
            ("ModelId", typeof(string)),
            ("WireModel", typeof(string)),
            ("MaxPromptTokens", typeof(int?)),
            ("MaxOutputTokens", typeof(int?)));
    }

    [Fact]
    public void Provider_BaseUrlIsMandatoryPositionZero()
    {
        var parameter = GetParameter<NewCopilotProviderCmdlet>("BaseUrl");

        Assert.True(parameter.Mandatory);
        Assert.Equal(0, parameter.Position);
    }

    [Fact]
    public void NamedProvider_ExposesEverySdkPropertyWithExactTypes()
    {
        AssertParameterTypes<NewCopilotNamedProviderCmdlet>(
            ("Name", typeof(string)),
            ("Type", typeof(string)),
            ("WireApi", typeof(string)),
            ("BaseUrl", typeof(string)),
            ("ApiKey", typeof(string)),
            ("BearerToken", typeof(string)),
            ("BearerTokenProvider", typeof(ScriptBlock)),
            ("BearerTokenProviderDelegate", typeof(Func<ProviderTokenArgs, Task<string>>)),
            ("Azure", typeof(AzureOptions)),
            ("Headers", typeof(IDictionary)));
    }

    [Fact]
    public void NamedProvider_NameAndBaseUrlAreMandatoryPositionalParameters()
    {
        var name = GetParameter<NewCopilotNamedProviderCmdlet>("Name");
        var baseUrl = GetParameter<NewCopilotNamedProviderCmdlet>("BaseUrl");

        Assert.True(name.Mandatory);
        Assert.Equal(0, name.Position);
        Assert.True(baseUrl.Mandatory);
        Assert.Equal(1, baseUrl.Position);
    }

    [Theory]
    [InlineData(typeof(NewCopilotProviderCmdlet))]
    [InlineData(typeof(NewCopilotNamedProviderCmdlet))]
    public void Provider_UsesExclusiveOptionalCallbackParameterSets(Type cmdletType)
    {
        var scriptBlock = Assert.IsType<ParameterAttribute>(
            Attribute.GetCustomAttribute(
                cmdletType.GetProperty("BearerTokenProvider")!,
                typeof(ParameterAttribute)));
        Assert.False(scriptBlock.Mandatory);
        Assert.Equal("ScriptBlock", scriptBlock.ParameterSetName);

        var callback = Assert.IsType<ParameterAttribute>(
            Attribute.GetCustomAttribute(
                cmdletType.GetProperty("BearerTokenProviderDelegate")!,
                typeof(ParameterAttribute)));
        Assert.False(callback.Mandatory);
        Assert.Equal("Delegate", callback.ParameterSetName);
    }

    [Fact]
    public void Provider_MapsEveryDtoPropertyExactly()
    {
        Func<ProviderTokenArgs, Task<string>> callback =
            args => Task.FromResult($"{args.ProviderName}:{args.SessionId}");
        var azure = new AzureOptions { ApiVersion = "2026-01-01" };
        IDictionary headers = new Hashtable
        {
            ["x-test"] = "value"
        };
        var provider = new NewCopilotProviderCmdlet
        {
            Type = "openai",
            WireApi = "responses",
            Transport = "websockets",
            BaseUrl = "https://provider.invalid/v1",
            ApiKey = "placeholder-api-key",
            BearerToken = "placeholder-bearer-token",
            BearerTokenProviderDelegate = callback,
            Azure = azure,
            Headers = headers,
            ModelId = "known-model",
            WireModel = "deployment-name",
            MaxPromptTokens = 1234,
            MaxOutputTokens = 567
        }.BuildProvider(PSLanguageMode.FullLanguage);

        Assert.Equal("openai", provider.Type);
        Assert.Equal("responses", provider.WireApi);
        Assert.Equal("websockets", provider.Transport);
        Assert.Equal("https://provider.invalid/v1", provider.BaseUrl);
        Assert.Equal("placeholder-api-key", provider.ApiKey);
        Assert.Equal("placeholder-bearer-token", provider.BearerToken);
        Assert.Same(callback, provider.BearerTokenProvider);
        Assert.Same(azure, provider.Azure);
        Assert.Equal("value", provider.Headers!["x-test"]);
        Assert.Equal("known-model", provider.ModelId);
        Assert.Equal("deployment-name", provider.WireModel);
        Assert.Equal(1234, provider.MaxPromptTokens);
        Assert.Equal(567, provider.MaxOutputTokens);
    }

    [Fact]
    public void NamedProvider_MapsEveryDtoPropertyExactly()
    {
        Func<ProviderTokenArgs, Task<string>> callback =
            args => Task.FromResult($"{args.ProviderName}:{args.SessionId}");
        var azure = new AzureOptions { ApiVersion = "2026-01-01" };
        IDictionary headers = new Hashtable
        {
            ["x-test"] = "value"
        };
        var provider = new NewCopilotNamedProviderCmdlet
        {
            Name = "named",
            Type = "azure",
            WireApi = "chat-completions",
            BaseUrl = "https://named.invalid",
            ApiKey = "placeholder-api-key",
            BearerToken = "placeholder-bearer-token",
            BearerTokenProviderDelegate = callback,
            Azure = azure,
            Headers = headers
        }.BuildProvider(PSLanguageMode.FullLanguage);

        Assert.Equal("named", provider.Name);
        Assert.Equal("azure", provider.Type);
        Assert.Equal("chat-completions", provider.WireApi);
        Assert.Equal("https://named.invalid", provider.BaseUrl);
        Assert.Equal("placeholder-api-key", provider.ApiKey);
        Assert.Equal("placeholder-bearer-token", provider.BearerToken);
        Assert.Same(callback, provider.BearerTokenProvider);
        Assert.Same(azure, provider.Azure);
        Assert.Equal("value", provider.Headers!["x-test"]);
    }

    [Fact]
    public void Provider_UnsetPropertiesPreserveSdkDefaults()
    {
        var provider = new NewCopilotProviderCmdlet()
            .BuildProvider(PSLanguageMode.FullLanguage);

        Assert.Equal(string.Empty, provider.BaseUrl);
        Assert.Null(provider.Type);
        Assert.Null(provider.WireApi);
        Assert.Null(provider.Transport);
        Assert.Null(provider.ApiKey);
        Assert.Null(provider.BearerToken);
        Assert.Null(provider.BearerTokenProvider);
        Assert.Null(provider.Azure);
        Assert.Null(provider.Headers);
        Assert.Null(provider.ModelId);
        Assert.Null(provider.WireModel);
        Assert.Null(provider.MaxPromptTokens);
        Assert.Null(provider.MaxOutputTokens);
    }

    [Fact]
    public void NamedProvider_UnsetPropertiesPreserveSdkDefaults()
    {
        var provider = new NewCopilotNamedProviderCmdlet()
            .BuildProvider(PSLanguageMode.FullLanguage);

        Assert.Equal(string.Empty, provider.Name);
        Assert.Equal(string.Empty, provider.BaseUrl);
        Assert.Null(provider.Type);
        Assert.Null(provider.WireApi);
        Assert.Null(provider.ApiKey);
        Assert.Null(provider.BearerToken);
        Assert.Null(provider.BearerTokenProvider);
        Assert.Null(provider.Azure);
        Assert.Null(provider.Headers);
    }

    [Fact]
    public void Provider_HeadersAcceptPowerShellHashtable()
    {
        var state = InitialSessionState.CreateDefault2();
        state.Commands.Add(new SessionStateCmdletEntry(
            "New-CopilotProvider",
            typeof(NewCopilotProviderCmdlet),
            null));
        using var ps = PowerShell.Create(state);
        ps.AddCommand("New-CopilotProvider")
            .AddParameter("BaseUrl", "https://provider.invalid")
            .AddParameter("Headers", new Hashtable
            {
                ["X-Test"] = "value"
            });

        var output = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        var provider = Assert.IsType<ProviderConfig>(Assert.Single(output).BaseObject);
        Assert.Equal("value", provider.Headers!["X-Test"]);
    }

    [Fact]
    public async Task Provider_CallbackPreservesRestrictedCommandVisibility()
    {
        var state = InitialSessionState.Create();
        state.LanguageMode = PSLanguageMode.FullLanguage;
        state.Commands.Add(new SessionStateCmdletEntry(
            "New-CopilotProvider",
            typeof(NewCopilotProviderCmdlet),
            null));
        state.Commands.Add(new SessionStateFunctionEntry(
            "Get-AllowedToken",
            "'allowed-token'"));

        var allowedProvider = BuildProviderInRunspace(
            state,
            "Get-AllowedToken");
        var allowed = await allowedProvider.BearerTokenProvider!(
            new ProviderTokenArgs
            {
                ProviderName = "default",
                SessionId = "restricted"
            });

        Assert.Equal("allowed-token", allowed);

        var restrictedState = InitialSessionState.Create();
        restrictedState.LanguageMode = PSLanguageMode.FullLanguage;
        restrictedState.Commands.Add(new SessionStateCmdletEntry(
            "New-CopilotProvider",
            typeof(NewCopilotProviderCmdlet),
            null));
        var restrictedProvider = BuildProviderInRunspace(
            restrictedState,
            "Get-Process | Out-Null; 'token'");

        var error = await Assert.ThrowsAnyAsync<Exception>(() =>
            restrictedProvider.BearerTokenProvider!(
                new ProviderTokenArgs
                {
                    ProviderName = "default",
                    SessionId = "restricted"
                }));

        Assert.Contains("Get-Process", error.Message);
    }

    [Fact]
    public async Task Provider_ScriptBlockCallbackReturnsRequiredToken()
    {
        var provider = new NewCopilotProviderCmdlet
        {
            BearerTokenProvider = ScriptBlock.Create(
                "param($tokenArgs) \"$($tokenArgs.ProviderName):$($tokenArgs.SessionId)\"")
        }.BuildProvider(PSLanguageMode.FullLanguage);

        var token = await provider.BearerTokenProvider!(new ProviderTokenArgs
        {
            ProviderName = "default",
            SessionId = "session-1"
        });

        Assert.Equal("default:session-1", token);
    }

    [Fact]
    public async Task NamedProvider_ScriptBlockCallbackReturnsRequiredToken()
    {
        var provider = new NewCopilotNamedProviderCmdlet
        {
            BearerTokenProvider = ScriptBlock.Create(
                "param($tokenArgs) \"$($tokenArgs.ProviderName):$($tokenArgs.SessionId)\"")
        }.BuildProvider(PSLanguageMode.FullLanguage);

        var token = await provider.BearerTokenProvider!(new ProviderTokenArgs
        {
            ProviderName = "named",
            SessionId = "session-2"
        });

        Assert.Equal("named:session-2", token);
    }

    [Theory]
    [InlineData("$null", "non-null")]
    [InlineData("42", "System.Int32")]
    [InlineData("'one'; 'two'", "2 values")]
    public async Task Provider_PropagatesSharedRunnerRequiredResultErrors(
        string script,
        string expectedMessage)
    {
        var provider = new NewCopilotProviderCmdlet
        {
            BearerTokenProvider = ScriptBlock.Create(script)
        }.BuildProvider(PSLanguageMode.FullLanguage);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.BearerTokenProvider!(null!));

        Assert.Contains(expectedMessage, error.Message);
    }

    [Fact]
    public void Provider_RejectsScriptBlockDelegateConflict()
    {
        var cmdlet = new NewCopilotProviderCmdlet
        {
            BearerTokenProvider = ScriptBlock.Create("'token'"),
            BearerTokenProviderDelegate = _ => Task.FromResult("token")
        };

        var error = Assert.Throws<ArgumentException>(
            () => cmdlet.BuildProvider(PSLanguageMode.FullLanguage));

        Assert.Contains("-BearerTokenProvider", error.Message);
        Assert.Contains("-BearerTokenProviderDelegate", error.Message);
    }

    [Fact]
    public void NamedProvider_RejectsScriptBlockDelegateConflict()
    {
        var cmdlet = new NewCopilotNamedProviderCmdlet
        {
            BearerTokenProvider = ScriptBlock.Create("'token'"),
            BearerTokenProviderDelegate = _ => Task.FromResult("token")
        };

        var error = Assert.Throws<ArgumentException>(
            () => cmdlet.BuildProvider(PSLanguageMode.FullLanguage));

        Assert.Contains("-BearerTokenProvider", error.Message);
        Assert.Contains("-BearerTokenProviderDelegate", error.Message);
    }

    private static void AssertParameterTypes<TCmdlet>(
        params (string Name, Type Type)[] expected)
    {
        foreach (var (name, type) in expected)
        {
            var property = typeof(TCmdlet).GetProperty(name);
            Assert.NotNull(property);
            Assert.Equal(type, property!.PropertyType);
            Assert.NotNull(Attribute.GetCustomAttribute(
                property, typeof(ParameterAttribute)));
        }
    }

    private static ParameterAttribute GetParameter<TCmdlet>(string name)
    {
        var property = typeof(TCmdlet).GetProperty(name)!;
        return Assert.IsType<ParameterAttribute>(
            Attribute.GetCustomAttribute(property, typeof(ParameterAttribute)));
    }

    private static ProviderConfig BuildProviderInRunspace(
        InitialSessionState state,
        string callback)
    {
        using var ps = PowerShell.Create(state);
        ps.AddCommand("New-CopilotProvider")
            .AddParameter("BaseUrl", "https://provider.invalid")
            .AddParameter("BearerTokenProvider", ScriptBlock.Create(callback));
        var output = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        return Assert.IsType<ProviderConfig>(Assert.Single(output).BaseObject);
    }
}
