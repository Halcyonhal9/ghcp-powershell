#pragma warning disable GHCP001 // experimental SDK members exposed by issue #28
using System.Collections;
using System.Management.Automation;
using GitHub.Copilot;
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
            ("InfiniteSessionConfig", typeof(InfiniteSessionConfig)),
            ("LargeOutput", typeof(LargeToolOutputConfig)),
            ("Memory", typeof(MemoryConfiguration)),
            ("CustomAgents", typeof(CustomAgentConfig[])),
            ("DefaultAgent", typeof(DefaultAgentConfig)),
            ("CustomAgentsLocalOnly", typeof(SwitchParameter)),
            ("McpOAuthTokenStorage", typeof(McpOAuthTokenStorageMode?)),
            ("OnMcpAuthRequest", typeof(ScriptBlock)),
            ("OnMcpAuthRequestDelegate", typeof(Func<McpAuthContext, Task<McpAuthResult>>)),
            ("Provider", typeof(ProviderConfig)),
            ("Providers", typeof(NamedProviderConfig[])),
            ("ProviderModels", typeof(ProviderModelConfig[])),
            ("Hooks", typeof(SessionHooks)),
            ("OnElicitationRequest", typeof(ScriptBlock)),
            ("OnElicitationRequestDelegate", typeof(Func<ElicitationContext, Task<ElicitationResult>>)),
            ("OnExitPlanModeRequest", typeof(ScriptBlock)),
            ("OnExitPlanModeRequestDelegate", typeof(Func<ExitPlanModeRequest, ExitPlanModeInvocation, Task<ExitPlanModeResult>>)),
            ("OnAutoModeSwitchRequest", typeof(ScriptBlock)),
            ("OnAutoModeSwitchRequestDelegate", typeof(Func<AutoModeSwitchRequest, AutoModeSwitchInvocation, Task<AutoModeSwitchResponse>>)),
            ("RemoteSession", typeof(string)),
            ("Commands", typeof(CommandDefinition[])),
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

    [Fact]
    public void NewCopilotSession_HasCloudOption()
    {
        var prop = typeof(NewCopilotSessionCmdlet).GetProperty("Cloud");

        Assert.NotNull(prop);
        Assert.Equal(typeof(CloudSessionOptions), prop!.PropertyType);
        Assert.Null(typeof(ResumeCopilotSessionCmdlet).GetProperty("Cloud"));
    }

    [Theory]
    [InlineData("WorkingDirectory", typeof(string))]
    [InlineData("Environment", typeof(Hashtable))]
    [InlineData("UseLoggedInUser", typeof(SwitchParameter))]
    [InlineData("EnableRemoteSessions", typeof(SwitchParameter))]
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
    public void SendCmdlets_HaveModeDisplayPromptAndAgentMode(Type cmdletType)
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

        var agentMode = cmdletType.GetProperty("AgentMode");
        Assert.NotNull(agentMode);
        Assert.Equal(typeof(AgentMode?), agentMode!.PropertyType);
        var agentModeCompleter = Assert.IsType<ArgumentCompleterAttribute>(
            Attribute.GetCustomAttribute(agentMode, typeof(ArgumentCompleterAttribute)));
        Assert.Equal(typeof(AgentModeCompleter), agentModeCompleter.Type);
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
    public void AgentModeCompleter_ReturnsSdkModes()
    {
        var completer = new AgentModeCompleter();
        var results = completer.CompleteArgument(
            "Send-CopilotMessage", "AgentMode", "", null!, new Hashtable()).ToList();

        Assert.Equal(["Interactive", "Plan", "Autopilot", "Shell"],
            results.Select(result => result.CompletionText));
    }

    [Fact]
    public void RemoteSessionModeCompleter_ReturnsSdkModes()
    {
        var completer = new RemoteSessionModeCompleter();
        var results = completer.CompleteArgument(
            "New-CopilotSession", "RemoteSession", "", null!, new Hashtable()).ToList();

        Assert.Equal(["off", "export", "on"],
            results.Select(result => result.CompletionText));
    }

    [Fact]
    public void CopilotMessageResult_TokenTotalsAreLong()
    {
        Assert.Equal(typeof(long), typeof(CopilotMessageResult).GetProperty("TotalInputTokens")!.PropertyType);
        Assert.Equal(typeof(long), typeof(CopilotMessageResult).GetProperty("TotalOutputTokens")!.PropertyType);
    }
}
