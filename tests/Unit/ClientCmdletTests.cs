using System.Management.Automation;
using GitHub.Copilot.SDK;
using Xunit;

namespace CopilotPS.Tests.Unit;

[Trait("Category", "Unit")]
public class ClientCmdletTests
{
    [Fact]
    public void NewCopilotClient_HasCorrectCmdletAttribute()
    {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(
            typeof(NewCopilotClientCmdlet), typeof(CmdletAttribute))!;
        Assert.Equal(VerbsCommon.New, attr.VerbName);
        Assert.Equal("CopilotClient", attr.NounName);
    }

    [Fact]
    public void NewCopilotClient_LogLevelDefaultsToInfo()
    {
        var cmdlet = new NewCopilotClientCmdlet();
        Assert.Equal("info", cmdlet.LogLevel);
    }

    [Fact]
    public void NewCopilotClient_HasOutputTypeAttribute()
    {
        var attrs = Attribute.GetCustomAttributes(
            typeof(NewCopilotClientCmdlet), typeof(OutputTypeAttribute));
        Assert.Single(attrs);
        var outputType = (OutputTypeAttribute)attrs[0];
        Assert.Contains(outputType.Type, t => t.Type == typeof(CopilotClient));
    }

    [Fact]
    public void StopCopilotClient_HasCorrectCmdletAttribute()
    {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(
            typeof(StopCopilotClientCmdlet), typeof(CmdletAttribute))!;
        Assert.Equal(VerbsLifecycle.Stop, attr.VerbName);
        Assert.Equal("CopilotClient", attr.NounName);
    }

    [Fact]
    public void StopCopilotClient_SupportsShouldProcess()
    {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(
            typeof(StopCopilotClientCmdlet), typeof(CmdletAttribute))!;
        Assert.True(attr.SupportsShouldProcess);
    }

    [Fact]
    public void TestCopilotConnection_HasCorrectCmdletAttribute()
    {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(
            typeof(TestCopilotConnectionCmdlet), typeof(CmdletAttribute))!;
        Assert.Equal(VerbsDiagnostic.Test, attr.VerbName);
        Assert.Equal("CopilotConnection", attr.NounName);
    }

    [Fact]
    public void TestCopilotConnection_HasPingResponseOutputType()
    {
        var attrs = Attribute.GetCustomAttributes(
            typeof(TestCopilotConnectionCmdlet), typeof(OutputTypeAttribute));
        Assert.Single(attrs);
        var outputType = (OutputTypeAttribute)attrs[0];
        Assert.Contains(outputType.Type, t => t.Type == typeof(PingResponse));
    }
}
