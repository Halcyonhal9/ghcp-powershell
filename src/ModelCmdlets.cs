using System.Management.Automation;
using GitHub.Copilot.SDK;
using GitHub.Copilot.SDK.Rpc;

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

[Cmdlet(VerbsCommon.Set, "CopilotModel")]
public sealed class SetCopilotModelCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    [ArgumentCompleter(typeof(CopilotModelCompleter))]
    public string Model { get; set; } = null!;

    [Parameter]
    [CopilotSessionTransformation]
    [ArgumentCompleter(typeof(CopilotSessionCompleter))]
    public CopilotSession? Session { get; set; }

    [Parameter]
    [ArgumentCompleter(typeof(ReasoningEffortCompleter))]
    public string? ReasoningEffort { get; set; }

    [Parameter]
    public SwitchParameter Vision { get; set; }

    protected override void EndProcessing()
    {
        if (!ModuleState.TryRequireSession(Session, out var target, out var noSession))
        {
            ThrowTerminatingError(noSession!);
            return;
        }

        ModelCapabilitiesOverride? capabilities = null;
        if (MyInvocation.BoundParameters.ContainsKey("Vision"))
        {
            capabilities = new ModelCapabilitiesOverride
            {
                Supports = new ModelCapabilitiesOverrideSupports { Vision = Vision.IsPresent }
            };
        }

        try
        {
            target.SetModelAsync(Model, ReasoningEffort, capabilities, CancellationToken.None)
                .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "SetModelFailed", ErrorCategory.InvalidOperation, Model));
        }
    }
}
