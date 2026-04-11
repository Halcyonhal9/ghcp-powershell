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

    [Fact]
    public void SetCopilotModel_HasCorrectCmdletAttribute()
    {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(
            typeof(SetCopilotModelCmdlet), typeof(CmdletAttribute))!;
        Assert.Equal(VerbsCommon.Set, attr.VerbName);
        Assert.Equal("CopilotModel", attr.NounName);
    }

    [Fact]
    public void SetCopilotModel_ModelIsMandatoryPosition0()
    {
        var prop = typeof(SetCopilotModelCmdlet).GetProperty("Model")!;
        var paramAttr = (ParameterAttribute)Attribute.GetCustomAttribute(prop, typeof(ParameterAttribute))!;
        Assert.True(paramAttr.Mandatory);
        Assert.Equal(0, paramAttr.Position);
    }

    [Fact]
    public void SetCopilotModel_ModelHasArgumentCompleter()
    {
        var prop = typeof(SetCopilotModelCmdlet).GetProperty("Model")!;
        var attr = Attribute.GetCustomAttribute(prop, typeof(ArgumentCompleterAttribute));
        Assert.NotNull(attr);
        Assert.Equal(typeof(CopilotModelCompleter), ((ArgumentCompleterAttribute)attr).Type);
    }

    [Fact]
    public void SetCopilotModel_HasReasoningEffortParameter()
    {
        var prop = typeof(SetCopilotModelCmdlet).GetProperty("ReasoningEffort")!;
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop.PropertyType);
    }

    [Fact]
    public void SetCopilotModel_ReasoningEffortHasArgumentCompleter()
    {
        var prop = typeof(SetCopilotModelCmdlet).GetProperty("ReasoningEffort")!;
        var attr = Attribute.GetCustomAttribute(prop, typeof(ArgumentCompleterAttribute));
        Assert.NotNull(attr);
        Assert.Equal(typeof(ReasoningEffortCompleter), ((ArgumentCompleterAttribute)attr).Type);
    }

    [Fact]
    public void SetCopilotModel_HasVisionParameter()
    {
        var prop = typeof(SetCopilotModelCmdlet).GetProperty("Vision")!;
        Assert.NotNull(prop);
        Assert.Equal(typeof(SwitchParameter), prop.PropertyType);
    }

    [Fact]
    public void SetCopilotModel_HasOptionalSessionParameter()
    {
        var prop = typeof(SetCopilotModelCmdlet).GetProperty("Session")!;
        Assert.NotNull(prop);
        Assert.Equal(typeof(CopilotSession), prop.PropertyType);
    }
}
