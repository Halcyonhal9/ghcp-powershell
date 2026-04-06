using System.Management.Automation;
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
