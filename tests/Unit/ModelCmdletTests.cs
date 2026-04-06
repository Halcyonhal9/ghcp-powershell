using System.Management.Automation;
using GitHub.Copilot.SDK;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.Unit;

[Trait("Category", "Unit")]
public class ModelCmdletTests
{
    [Fact]
    public void GetCopilotModel_HasCorrectCmdletAttribute()
    {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(
            typeof(GetCopilotModelCmdlet), typeof(CmdletAttribute))!;
        Assert.Equal(VerbsCommon.Get, attr.VerbName);
        Assert.Equal("CopilotModel", attr.NounName);
    }

    [Fact]
    public void GetCopilotModel_HasOutputTypeAttribute()
    {
        var attrs = Attribute.GetCustomAttributes(
            typeof(GetCopilotModelCmdlet), typeof(OutputTypeAttribute));
        Assert.Single(attrs);
        var outputType = (OutputTypeAttribute)attrs[0];
        Assert.Contains(outputType.Type, t => t.Type == typeof(ModelInfo));
    }

    [Fact]
    public void GetCopilotModel_HasOptionalClientParameter()
    {
        var prop = typeof(GetCopilotModelCmdlet).GetProperty("Client")!;
        Assert.NotNull(prop);
        Assert.Equal(typeof(CopilotClient), prop.PropertyType);
    }

    [Fact]
    public void CopilotModelCompleter_ImplementsIArgumentCompleter()
    {
        var completer = new CopilotModelCompleter();
        Assert.IsAssignableFrom<IArgumentCompleter>(completer);
    }

    [Fact]
    public void CopilotModelCompleter_ReturnsEmptyWhenNoClient()
    {
        var original = ModuleState.Client;
        try
        {
            ModuleState.Client = null;

            var completer = new CopilotModelCompleter();
            var results = completer.CompleteArgument(
                "New-CopilotSession", "Model", "", null!, new System.Collections.Hashtable());

            Assert.Empty(results);
        }
        finally
        {
            ModuleState.Client = original;
        }
    }

    [Fact]
    public void NewCopilotSession_ModelHasArgumentCompleter()
    {
        var prop = typeof(NewCopilotSessionCmdlet).GetProperty("Model")!;
        var attr = Attribute.GetCustomAttribute(prop, typeof(ArgumentCompleterAttribute));
        Assert.NotNull(attr);
        var completerAttr = (ArgumentCompleterAttribute)attr;
        Assert.Equal(typeof(CopilotModelCompleter), completerAttr.Type);
    }

    [Fact]
    public void ResumeCopilotSession_ModelHasArgumentCompleter()
    {
        var prop = typeof(ResumeCopilotSessionCmdlet).GetProperty("Model")!;
        var attr = Attribute.GetCustomAttribute(prop, typeof(ArgumentCompleterAttribute));
        Assert.NotNull(attr);
        var completerAttr = (ArgumentCompleterAttribute)attr;
        Assert.Equal(typeof(CopilotModelCompleter), completerAttr.Type);
    }
}
