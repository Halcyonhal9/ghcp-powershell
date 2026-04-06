using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;
using GitHub.Copilot.SDK;

namespace CopilotCmdlets;

[Cmdlet(VerbsCommon.Get, "CopilotModel")]
[OutputType(typeof(ModelInfo))]
public sealed class GetCopilotModelCmdlet : PSCmdlet
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
            var models = target.ListModelsAsync(CancellationToken.None)
                .GetAwaiter().GetResult();
            WriteObject(models, enumerateCollection: true);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "ListModelsFailed", ErrorCategory.ReadError, null));
        }
    }
}

/// <summary>
/// Provides tab-completion for -Model parameters by querying the SDK for available models.
/// </summary>
public sealed class CopilotModelCompleter : IArgumentCompleter
{
    public IEnumerable<CompletionResult> CompleteArgument(
        string commandName,
        string parameterName,
        string wordToComplete,
        CommandAst commandAst,
        IDictionary fakeBoundParameters)
    {
        var client = ModuleState.Client;
        if (client is null)
            yield break;

        IEnumerable<ModelInfo> models;
        try
        {
            models = client.ListModelsAsync(CancellationToken.None)
                .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Model completion failed: {ex.Message}");
            yield break;
        }

        var prefix = wordToComplete ?? string.Empty;

        foreach (var model in models)
        {
            var id = model.Id;
            if (id is null) continue;
            if (id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                yield return new CompletionResult(id, id, CompletionResultType.ParameterValue, id);
            }
        }
    }
}
