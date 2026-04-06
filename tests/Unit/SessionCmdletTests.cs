using System.Management.Automation;
using GitHub.Copilot.SDK;
using Xunit;

namespace CopilotPS.Tests.Unit;

[Trait("Category", "Unit")]
public class SessionCmdletTests
{
    [Fact]
    public void NewCopilotSession_HasCorrectCmdletAttribute()
    {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(
            typeof(NewCopilotSessionCmdlet), typeof(CmdletAttribute))!;
        Assert.Equal(VerbsCommon.New, attr.VerbName);
        Assert.Equal("CopilotSession", attr.NounName);
    }

    [Fact]
    public void NewCopilotSession_HasOutputType()
    {
        var attrs = Attribute.GetCustomAttributes(
            typeof(NewCopilotSessionCmdlet), typeof(OutputTypeAttribute));
        Assert.Single(attrs);
        var outputType = (OutputTypeAttribute)attrs[0];
        Assert.Contains(outputType.Type, t => t.Type == typeof(CopilotSession));
    }

    [Fact]
    public void ResumeCopilotSession_HasMandatorySessionIdParameter()
    {
        var prop = typeof(ResumeCopilotSessionCmdlet).GetProperty("SessionId")!;
        var paramAttr = (ParameterAttribute)Attribute.GetCustomAttribute(prop, typeof(ParameterAttribute))!;
        Assert.True(paramAttr.Mandatory);
        Assert.Equal(0, paramAttr.Position);
    }

    [Fact]
    public void ResumeCopilotSession_HasCorrectCmdletAttribute()
    {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(
            typeof(ResumeCopilotSessionCmdlet), typeof(CmdletAttribute))!;
        Assert.Equal("Resume", attr.VerbName);
        Assert.Equal("CopilotSession", attr.NounName);
    }

    [Fact]
    public void GetCopilotSession_HasCorrectCmdletAttribute()
    {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(
            typeof(GetCopilotSessionCmdlet), typeof(CmdletAttribute))!;
        Assert.Equal(VerbsCommon.Get, attr.VerbName);
        Assert.Equal("CopilotSession", attr.NounName);
    }

    [Fact]
    public void GetCopilotSession_OutputsSessionMetadata()
    {
        var attrs = Attribute.GetCustomAttributes(
            typeof(GetCopilotSessionCmdlet), typeof(OutputTypeAttribute));
        Assert.Single(attrs);
        var outputType = (OutputTypeAttribute)attrs[0];
        Assert.Contains(outputType.Type, t => t.Type == typeof(SessionMetadata));
    }

    [Fact]
    public void RemoveCopilotSession_SupportsShouldProcess()
    {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(
            typeof(RemoveCopilotSessionCmdlet), typeof(CmdletAttribute))!;
        Assert.True(attr.SupportsShouldProcess);
    }

    [Fact]
    public void RemoveCopilotSession_HasMandatorySessionIdParameter()
    {
        var prop = typeof(RemoveCopilotSessionCmdlet).GetProperty("SessionId")!;
        var paramAttr = (ParameterAttribute)Attribute.GetCustomAttribute(prop, typeof(ParameterAttribute))!;
        Assert.True(paramAttr.Mandatory);
        Assert.Equal(0, paramAttr.Position);
    }

    [Fact]
    public void CloseCopilotSession_HasCorrectCmdletAttribute()
    {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(
            typeof(CloseCopilotSessionCmdlet), typeof(CmdletAttribute))!;
        Assert.Equal(VerbsCommon.Close, attr.VerbName);
        Assert.Equal("CopilotSession", attr.NounName);
    }

    [Fact]
    public async Task PermissionHandlers_AutoApproveReturnsApproval()
    {
        var handler = PermissionHandlers.AutoApprove;
        var request = new PermissionRequest { Kind = "tool_use" };
        var invocation = new PermissionInvocation();

        var result = await handler.Invoke(request, invocation);

        Assert.Equal("approve", result.Kind.ToString());
    }

    [Fact]
    public void UserInputHandlers_InteractiveIsNotNull()
    {
        var handler = UserInputHandlers.Interactive;
        Assert.NotNull(handler);
    }
}
