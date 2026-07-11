using System.Collections;
using System.Management.Automation;
using Microsoft.Extensions.AI;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.Unit;

/// <summary>Parameter-surface tests for options added with the SDK 1.x upgrade.</summary>
[Trait("Category", "Unit")]
public class NewParameterTests
{
    [Theory]
    [InlineData(typeof(NewCopilotSessionCmdlet))]
    [InlineData(typeof(ResumeCopilotSessionCmdlet))]
    public void SessionCmdlets_ShareCommonConfigParameters(Type cmdletType)
    {
        Assert.True(typeof(SessionConfigCmdletBase).IsAssignableFrom(cmdletType));

        foreach (var (name, type) in new (string, Type)[]
        {
            ("EnableCitations", typeof(SwitchParameter)),
            ("ExcludedBuiltInAgents", typeof(string[])),
            ("MaxAiCredits", typeof(double?)),
            ("DisabledSkills", typeof(string[])),
            ("McpServers", typeof(Hashtable)),
            ("Tool", typeof(AIFunction[])),
            ("AvailableTools", typeof(string[])),
            ("ExcludedTools", typeof(string[])),
            ("InfiniteSessions", typeof(SwitchParameter)),
        })
        {
            var prop = cmdletType.GetProperty(name);
            Assert.NotNull(prop);
            Assert.Equal(type, prop!.PropertyType);
            Assert.NotNull(Attribute.GetCustomAttribute(prop, typeof(ParameterAttribute)));
        }
    }

    [Fact]
    public void ResumeCopilotSession_HasContinuePendingWork()
    {
        var prop = typeof(ResumeCopilotSessionCmdlet).GetProperty("ContinuePendingWork");
        Assert.NotNull(prop);
        Assert.Equal(typeof(SwitchParameter), prop!.PropertyType);
    }

    [Theory]
    [InlineData("WorkingDirectory", typeof(string))]
    [InlineData("Environment", typeof(Hashtable))]
    [InlineData("UseLoggedInUser", typeof(SwitchParameter))]
    public void NewCopilotClient_HasNewOptions(string name, Type type)
    {
        var prop = typeof(NewCopilotClientCmdlet).GetProperty(name);
        Assert.NotNull(prop);
        Assert.Equal(type, prop!.PropertyType);
        Assert.NotNull(Attribute.GetCustomAttribute(prop, typeof(ParameterAttribute)));
    }

    [Theory]
    [InlineData(typeof(SendCopilotMessageCmdlet))]
    [InlineData(typeof(SendCopilotMessageAsyncCmdlet))]
    public void SendCmdlets_HaveModeAndDisplayPrompt(Type cmdletType)
    {
        foreach (var name in new[] { "Mode", "DisplayPrompt" })
        {
            var prop = cmdletType.GetProperty(name);
            Assert.NotNull(prop);
            Assert.Equal(typeof(string), prop!.PropertyType);
            Assert.NotNull(Attribute.GetCustomAttribute(prop, typeof(ParameterAttribute)));
        }

        var modeProp = cmdletType.GetProperty("Mode")!;
        var completer = Assert.IsType<ArgumentCompleterAttribute>(
            Attribute.GetCustomAttribute(modeProp, typeof(ArgumentCompleterAttribute)));
        Assert.Equal(typeof(MessageModeCompleter), completer.Type);
    }

    [Fact]
    public void MessageModeCompleter_ReturnsBothModes()
    {
        var completer = new MessageModeCompleter();
        var results = completer.CompleteArgument(
            "Send-CopilotMessage", "Mode", "", null!, new Hashtable()).ToList();

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.CompletionText == "enqueue");
        Assert.Contains(results, r => r.CompletionText == "immediate");
    }

    [Fact]
    public void CopilotMessageResult_TokenTotalsAreLong()
    {
        Assert.Equal(typeof(long), typeof(CopilotMessageResult).GetProperty("TotalInputTokens")!.PropertyType);
        Assert.Equal(typeof(long), typeof(CopilotMessageResult).GetProperty("TotalOutputTokens")!.PropertyType);
    }
}
