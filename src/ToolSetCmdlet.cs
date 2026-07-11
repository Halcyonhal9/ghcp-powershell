#pragma warning disable GHCP001 // experimental SDK members: ToolSet and BuiltInTools
using System.Management.Automation;
using GitHub.Copilot;

namespace CopilotCmdlets;

[Cmdlet(VerbsCommon.New, "CopilotToolSet")]
[OutputType(typeof(ToolSet))]
public sealed class NewCopilotToolSetCmdlet : PSCmdlet
{
    [Parameter(Position = 0)]
    public string[]? BuiltIn { get; set; }

    [Parameter]
    public string[]? Custom { get; set; }

    [Parameter]
    public string[]? Mcp { get; set; }

    [Parameter]
    public SwitchParameter Isolated { get; set; }

    internal ToolSet BuildToolSet()
    {
        var toolSet = new ToolSet();

        if (BuiltIn is not null)
            toolSet.AddBuiltIn(BuiltIn);
        if (Isolated)
            toolSet.AddBuiltIn(BuiltInTools.Isolated);
        if (Custom is not null)
        {
            foreach (var name in Custom)
            {
                toolSet.AddCustom(name);
            }
        }
        if (Mcp is not null)
        {
            foreach (var name in Mcp)
            {
                toolSet.AddMcp(name);
            }
        }

        return toolSet;
    }

    protected override void EndProcessing()
    {
        try
        {
            WriteObject(BuildToolSet(), enumerateCollection: false);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "ToolSetCreateFailed", ErrorCategory.InvalidArgument, null));
        }
    }
}
