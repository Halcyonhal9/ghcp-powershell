using System.Management.Automation;
using GitHub.Copilot;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.Unit;

[Trait("Category", "Unit")]
public class StatusCmdletTests
{
    [Fact]
    public void GetCopilotStatus_HasCorrectCmdletAttribute()
    {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(
            typeof(GetCopilotStatusCmdlet), typeof(CmdletAttribute))!;
        Assert.Equal(VerbsCommon.Get, attr.VerbName);
        Assert.Equal("CopilotStatus", attr.NounName);
    }

    [Fact]
    public void GetCopilotStatus_OutputsGetStatusResponse()
    {
        var attrs = Attribute.GetCustomAttributes(
            typeof(GetCopilotStatusCmdlet), typeof(OutputTypeAttribute));
        Assert.Single(attrs);
        var outputType = (OutputTypeAttribute)attrs[0];
        Assert.Contains(outputType.Type, t => t.Type == typeof(GetStatusResponse));
    }

    [Fact]
    public void GetCopilotAuthStatus_HasCorrectCmdletAttribute()
    {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(
            typeof(GetCopilotAuthStatusCmdlet), typeof(CmdletAttribute))!;
        Assert.Equal(VerbsCommon.Get, attr.VerbName);
        Assert.Equal("CopilotAuthStatus", attr.NounName);
    }

    [Fact]
    public void GetCopilotAuthStatus_OutputsGetAuthStatusResponse()
    {
        var attrs = Attribute.GetCustomAttributes(
            typeof(GetCopilotAuthStatusCmdlet), typeof(OutputTypeAttribute));
        Assert.Single(attrs);
        var outputType = (OutputTypeAttribute)attrs[0];
        Assert.Contains(outputType.Type, t => t.Type == typeof(GetAuthStatusResponse));
    }

    [Fact]
    public void StopCopilotMessage_HasCorrectCmdletAttribute()
    {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(
            typeof(StopCopilotMessageCmdlet), typeof(CmdletAttribute))!;
        Assert.Equal(VerbsLifecycle.Stop, attr.VerbName);
        Assert.Equal("CopilotMessage", attr.NounName);
    }

    [Fact]
    public void StopCopilotMessage_SessionAcceptsSessionIdStrings()
    {
        var prop = typeof(StopCopilotMessageCmdlet).GetProperty("Session")!;
        Assert.Equal(typeof(CopilotSession), prop.PropertyType);
        Assert.NotNull(Attribute.GetCustomAttribute(prop, typeof(CopilotSessionTransformationAttribute)));
    }
}
