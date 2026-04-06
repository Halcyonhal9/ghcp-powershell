using System.Management.Automation;
using GitHub.Copilot.SDK;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.Unit;

[Trait("Category", "Unit")]
public class CompleterTests
{
    [Fact]
    public void CopilotSessionCompleter_ImplementsIArgumentCompleter()
    {
        var completer = new CopilotSessionCompleter();
        Assert.IsAssignableFrom<IArgumentCompleter>(completer);
    }

    [Fact]
    public void CopilotSessionCompleter_ReturnsEmptyWhenNoClient()
    {
        var original = ModuleState.Client;
        try
        {
            ModuleState.Client = null;

            var completer = new CopilotSessionCompleter();
            var results = completer.CompleteArgument(
                "Resume-CopilotSession", "SessionId", "", null!, new System.Collections.Hashtable());

            Assert.Empty(results);
        }
        finally
        {
            ModuleState.Client = original;
        }
    }

    [Fact]
    public void ResumeCopilotSession_SessionIdHasArgumentCompleter()
    {
        var prop = typeof(ResumeCopilotSessionCmdlet).GetProperty("SessionId")!;
        var attr = Attribute.GetCustomAttribute(prop, typeof(ArgumentCompleterAttribute));
        Assert.NotNull(attr);
        Assert.Equal(typeof(CopilotSessionCompleter), ((ArgumentCompleterAttribute)attr).Type);
    }

    [Fact]
    public void RemoveCopilotSession_SessionIdHasArgumentCompleter()
    {
        var prop = typeof(RemoveCopilotSessionCmdlet).GetProperty("SessionId")!;
        var attr = Attribute.GetCustomAttribute(prop, typeof(ArgumentCompleterAttribute));
        Assert.NotNull(attr);
        Assert.Equal(typeof(CopilotSessionCompleter), ((ArgumentCompleterAttribute)attr).Type);
    }

    [Fact]
    public void ReasoningEffortCompleter_ImplementsIArgumentCompleter()
    {
        var completer = new ReasoningEffortCompleter();
        Assert.IsAssignableFrom<IArgumentCompleter>(completer);
    }

    [Fact]
    public void ReasoningEffortCompleter_ReturnsAllLevelsForEmptyInput()
    {
        var completer = new ReasoningEffortCompleter();
        var results = completer.CompleteArgument(
            "New-CopilotSession", "ReasoningEffort", "", null!, new System.Collections.Hashtable())
            .ToList();

        Assert.Equal(3, results.Count);
        Assert.Contains(results, r => r.CompletionText == "low");
        Assert.Contains(results, r => r.CompletionText == "medium");
        Assert.Contains(results, r => r.CompletionText == "high");
    }

    [Fact]
    public void ReasoningEffortCompleter_FiltersByPrefix()
    {
        var completer = new ReasoningEffortCompleter();
        var results = completer.CompleteArgument(
            "New-CopilotSession", "ReasoningEffort", "m", null!, new System.Collections.Hashtable())
            .ToList();

        Assert.Single(results);
        Assert.Equal("medium", results[0].CompletionText);
    }

    [Fact]
    public void NewCopilotSession_ReasoningEffortHasArgumentCompleter()
    {
        var prop = typeof(NewCopilotSessionCmdlet).GetProperty("ReasoningEffort")!;
        var attr = Attribute.GetCustomAttribute(prop, typeof(ArgumentCompleterAttribute));
        Assert.NotNull(attr);
        Assert.Equal(typeof(ReasoningEffortCompleter), ((ArgumentCompleterAttribute)attr).Type);
    }

    [Fact]
    public void ResumeCopilotSession_ReasoningEffortHasArgumentCompleter()
    {
        var prop = typeof(ResumeCopilotSessionCmdlet).GetProperty("ReasoningEffort")!;
        var attr = Attribute.GetCustomAttribute(prop, typeof(ArgumentCompleterAttribute));
        Assert.NotNull(attr);
        Assert.Equal(typeof(ReasoningEffortCompleter), ((ArgumentCompleterAttribute)attr).Type);
    }

    [Fact]
    public void LogLevelCompleter_ImplementsIArgumentCompleter()
    {
        var completer = new LogLevelCompleter();
        Assert.IsAssignableFrom<IArgumentCompleter>(completer);
    }

    [Fact]
    public void LogLevelCompleter_ReturnsAllLevelsForEmptyInput()
    {
        var completer = new LogLevelCompleter();
        var results = completer.CompleteArgument(
            "New-CopilotClient", "LogLevel", "", null!, new System.Collections.Hashtable())
            .ToList();

        Assert.Equal(5, results.Count);
        Assert.Contains(results, r => r.CompletionText == "trace");
        Assert.Contains(results, r => r.CompletionText == "debug");
        Assert.Contains(results, r => r.CompletionText == "info");
        Assert.Contains(results, r => r.CompletionText == "warn");
        Assert.Contains(results, r => r.CompletionText == "error");
    }

    [Fact]
    public void LogLevelCompleter_FiltersByPrefix()
    {
        var completer = new LogLevelCompleter();
        var results = completer.CompleteArgument(
            "New-CopilotClient", "LogLevel", "d", null!, new System.Collections.Hashtable())
            .ToList();

        Assert.Single(results);
        Assert.Equal("debug", results[0].CompletionText);
    }

    [Fact]
    public void NewCopilotClient_LogLevelHasArgumentCompleter()
    {
        var prop = typeof(NewCopilotClientCmdlet).GetProperty("LogLevel")!;
        var attr = Attribute.GetCustomAttribute(prop, typeof(ArgumentCompleterAttribute));
        Assert.NotNull(attr);
        Assert.Equal(typeof(LogLevelCompleter), ((ArgumentCompleterAttribute)attr).Type);
    }
}
