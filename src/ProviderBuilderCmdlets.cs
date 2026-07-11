#pragma warning disable GHCP001 // experimental SDK members: BearerTokenProvider, NamedProviderConfig
using System.Collections;
using System.Management.Automation;
using GitHub.Copilot;

namespace CopilotCmdlets;

[Cmdlet(
    VerbsCommon.New,
    "CopilotProvider",
    DefaultParameterSetName = "ScriptBlock")]
[OutputType(typeof(ProviderConfig))]
public sealed class NewCopilotProviderCmdlet : PSCmdlet
{
    [Parameter]
    public string? Type { get; set; }

    [Parameter]
    public string? WireApi { get; set; }

    [Parameter]
    public string? Transport { get; set; }

    [Parameter(Mandatory = true, Position = 0)]
    public string BaseUrl { get; set; } = string.Empty;

    [Parameter]
    public string? ApiKey { get; set; }

    [Parameter]
    public string? BearerToken { get; set; }

    [Parameter(ParameterSetName = "ScriptBlock")]
    public ScriptBlock? BearerTokenProvider { get; set; }

    [Parameter(ParameterSetName = "Delegate")]
    public Func<ProviderTokenArgs, Task<string>>? BearerTokenProviderDelegate { get; set; }

    [Parameter]
    public AzureOptions? Azure { get; set; }

    [Parameter]
    public IDictionary? Headers { get; set; }

    [Parameter]
    public string? ModelId { get; set; }

    [Parameter]
    public string? WireModel { get; set; }

    [Parameter]
    public int? MaxPromptTokens { get; set; }

    [Parameter]
    public int? MaxOutputTokens { get; set; }

    internal ProviderConfig BuildProvider(PSLanguageMode languageMode)
    {
        if (BearerTokenProvider is not null && BearerTokenProviderDelegate is not null)
        {
            throw new ArgumentException(
                "Specify either -BearerTokenProvider or -BearerTokenProviderDelegate, not both.");
        }

        var bearerTokenProvider = BearerTokenProviderDelegate;
        if (BearerTokenProvider is not null)
        {
            var runner = new PowerShellCallbackRunner(BearerTokenProvider, languageMode);
            bearerTokenProvider = args => runner.InvokeRequiredAsync<string>(args);
        }

        return new ProviderConfig
        {
            Type = Type,
            WireApi = WireApi,
            Transport = Transport,
            BaseUrl = BaseUrl,
            ApiKey = ApiKey,
            BearerToken = BearerToken,
            BearerTokenProvider = bearerTokenProvider,
            Azure = Azure,
            Headers = BuildHeaders(Headers),
            ModelId = ModelId,
            WireModel = WireModel,
            MaxPromptTokens = MaxPromptTokens,
            MaxOutputTokens = MaxOutputTokens
        };
    }

    protected override void EndProcessing()
    {
        try
        {
            WriteObject(BuildProvider(SessionState.LanguageMode));
        }
        catch (ArgumentException ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "InvalidProviderConfig", ErrorCategory.InvalidArgument, null));
        }
    }

    internal static Dictionary<string, string>? BuildHeaders(IDictionary? headers)
    {
        if (headers is null)
            return null;

        var result = new Dictionary<string, string>();
        foreach (DictionaryEntry entry in headers)
        {
            var key = McpServerHelper.Unwrap(entry.Key)?.ToString();
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(
                    "Provider header names must be non-empty strings.");
            }

            result[key] = McpServerHelper.Unwrap(entry.Value)?.ToString() ?? string.Empty;
        }

        return result;
    }
}

[Cmdlet(
    VerbsCommon.New,
    "CopilotNamedProvider",
    DefaultParameterSetName = "ScriptBlock")]
[OutputType(typeof(NamedProviderConfig))]
public sealed class NewCopilotNamedProviderCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Name { get; set; } = string.Empty;

    [Parameter]
    public string? Type { get; set; }

    [Parameter]
    public string? WireApi { get; set; }

    [Parameter(Mandatory = true, Position = 1)]
    public string BaseUrl { get; set; } = string.Empty;

    [Parameter]
    public string? ApiKey { get; set; }

    [Parameter]
    public string? BearerToken { get; set; }

    [Parameter(ParameterSetName = "ScriptBlock")]
    public ScriptBlock? BearerTokenProvider { get; set; }

    [Parameter(ParameterSetName = "Delegate")]
    public Func<ProviderTokenArgs, Task<string>>? BearerTokenProviderDelegate { get; set; }

    [Parameter]
    public AzureOptions? Azure { get; set; }

    [Parameter]
    public IDictionary? Headers { get; set; }

    internal NamedProviderConfig BuildProvider(PSLanguageMode languageMode)
    {
        if (BearerTokenProvider is not null && BearerTokenProviderDelegate is not null)
        {
            throw new ArgumentException(
                "Specify either -BearerTokenProvider or -BearerTokenProviderDelegate, not both.");
        }

        var bearerTokenProvider = BearerTokenProviderDelegate;
        if (BearerTokenProvider is not null)
        {
            var runner = new PowerShellCallbackRunner(BearerTokenProvider, languageMode);
            bearerTokenProvider = args => runner.InvokeRequiredAsync<string>(args);
        }

        return new NamedProviderConfig
        {
            Name = Name,
            Type = Type,
            WireApi = WireApi,
            BaseUrl = BaseUrl,
            ApiKey = ApiKey,
            BearerToken = BearerToken,
            BearerTokenProvider = bearerTokenProvider,
            Azure = Azure,
            Headers = NewCopilotProviderCmdlet.BuildHeaders(Headers)
        };
    }

    protected override void EndProcessing()
    {
        try
        {
            WriteObject(BuildProvider(SessionState.LanguageMode));
        }
        catch (ArgumentException ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "InvalidNamedProviderConfig", ErrorCategory.InvalidArgument, null));
        }
    }
}
