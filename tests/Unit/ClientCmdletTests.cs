using System.Management.Automation;
using GitHub.Copilot.SDK;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.Unit;

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

    [Fact]
    public void StopCopilotClient_HasForceParameter()
    {
        var prop = typeof(StopCopilotClientCmdlet).GetProperty("Force")!;
        Assert.NotNull(prop);
        Assert.Equal(typeof(SwitchParameter), prop.PropertyType);
    }

    [Fact]
    public void NewCopilotClient_HasAllExpectedParameters()
    {
        var type = typeof(NewCopilotClientCmdlet);
        Assert.NotNull(type.GetProperty("GitHubToken"));
        Assert.NotNull(type.GetProperty("CliPath"));
        Assert.NotNull(type.GetProperty("CliUrl"));
        Assert.NotNull(type.GetProperty("LogLevel"));
        Assert.NotNull(type.GetProperty("OtlpEndpoint"));
        Assert.NotNull(type.GetProperty("TelemetrySourceName"));
    }

    [Fact]
    public void NewCopilotClient_TelemetryParametersDefaultToNull()
    {
        var cmdlet = new NewCopilotClientCmdlet();
        Assert.Null(cmdlet.OtlpEndpoint);
        Assert.Null(cmdlet.TelemetrySourceName);
    }

    [Fact]
    public void ConnectCopilot_HasCorrectCmdletAttribute()
    {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(
            typeof(ConnectCopilotCmdlet), typeof(CmdletAttribute))!;
        Assert.Equal(VerbsCommunications.Connect, attr.VerbName);
        Assert.Equal("Copilot", attr.NounName);
        Assert.True(attr.SupportsShouldProcess);
    }

    [Fact]
    public void ConnectCopilot_HasExpectedParameters()
    {
        var type = typeof(ConnectCopilotCmdlet);
        Assert.NotNull(type.GetProperty("CliPath"));
        Assert.NotNull(type.GetProperty("ArgumentList"));
        var force = type.GetProperty("Force");
        Assert.NotNull(force);
        Assert.Equal(typeof(SwitchParameter), force!.PropertyType);
    }

    [Fact]
    public void ConnectCopilot_StopProcessingIsNullSafe()
    {
        var cmdlet = new ConnectCopilotCmdlet();
        var stopMethod = typeof(ConnectCopilotCmdlet).GetMethod(
            "StopProcessing",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        // _runningProc is null — must not throw
        stopMethod.Invoke(cmdlet, null);
    }

    [Fact]
    public void ConnectCopilot_StopProcessingKillsRunningProcess()
    {
        // Spawn a long-running child, attach it to _runningProc, then call StopProcessing
        // and verify the child exits.
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
            ArgumentList = { OperatingSystem.IsWindows() ? "/c" : "-c", "sleep 30" },
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        if (OperatingSystem.IsWindows())
        {
            psi.ArgumentList.Clear();
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add("ping -n 30 127.0.0.1");
        }
        using var proc = System.Diagnostics.Process.Start(psi)!;
        Assert.False(proc.HasExited);

        var cmdlet = new ConnectCopilotCmdlet();
        var procField = typeof(ConnectCopilotCmdlet).GetField(
            "_runningProc",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        procField.SetValue(cmdlet, proc);

        var stopMethod = typeof(ConnectCopilotCmdlet).GetMethod(
            "StopProcessing",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        stopMethod.Invoke(cmdlet, null);

        Assert.True(proc.WaitForExit(5000), "Process did not exit after StopProcessing");
    }

    [Fact]
    public void ResolveBundledCliPath_ReturnsExistingFileOrNull()
    {
        // Static helper: either resolves to a real file under runtimes/<rid>/native,
        // or returns null. It must never throw and must never return a non-existent path.
        var method = typeof(ModuleState).GetMethod(
            "ResolveBundledCliPath",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var result = (string?)method.Invoke(null, null);
        if (result is not null)
        {
            Assert.True(File.Exists(result), $"ResolveBundledCliPath returned a path that doesn't exist: {result}");
        }
    }
}
