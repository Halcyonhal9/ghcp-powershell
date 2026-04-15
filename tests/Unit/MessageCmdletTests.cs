using System.Management.Automation;
using GitHub.Copilot.SDK;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.Unit;

[Trait("Category", "Unit")]
public class MessageCmdletTests
{
    [Fact]
    public void SendCopilotMessage_HasCorrectCmdletAttribute()
    {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(
            typeof(SendCopilotMessageCmdlet), typeof(CmdletAttribute))!;
        Assert.Equal(VerbsCommunications.Send, attr.VerbName);
        Assert.Equal("CopilotMessage", attr.NounName);
    }

    [Fact]
    public void SendCopilotMessage_PromptIsMandatoryPosition0()
    {
        var prop = typeof(SendCopilotMessageCmdlet).GetProperty("Prompt")!;
        var paramAttr = (ParameterAttribute)Attribute.GetCustomAttribute(prop, typeof(ParameterAttribute))!;
        Assert.True(paramAttr.Mandatory);
        Assert.Equal(0, paramAttr.Position);
    }

    [Fact]
    public void SendCopilotMessage_TimeoutDefaultsTo5Minutes()
    {
        var cmdlet = new SendCopilotMessageCmdlet();
        Assert.Equal(TimeSpan.FromMinutes(5), cmdlet.Timeout);
    }

    [Fact]
    public void SendCopilotMessage_HasOutputType()
    {
        var attrs = Attribute.GetCustomAttributes(
            typeof(SendCopilotMessageCmdlet), typeof(OutputTypeAttribute));
        Assert.Single(attrs);
        var outputType = (OutputTypeAttribute)attrs[0];
        Assert.Contains(outputType.Type, t => t.Type == typeof(CopilotMessageResult));
    }

    [Fact]
    public void GetCopilotMessage_HasCorrectCmdletAttribute()
    {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(
            typeof(GetCopilotMessageCmdlet), typeof(CmdletAttribute))!;
        Assert.Equal(VerbsCommon.Get, attr.VerbName);
        Assert.Equal("CopilotMessage", attr.NounName);
    }

    [Fact]
    public void GetCopilotMessage_HasOutputType()
    {
        var attrs = Attribute.GetCustomAttributes(
            typeof(GetCopilotMessageCmdlet), typeof(OutputTypeAttribute));
        Assert.Single(attrs);
        var outputType = (OutputTypeAttribute)attrs[0];
        Assert.Contains(outputType.Type, t => t.Type == typeof(SessionEvent));
    }

    [Fact]
    public void CopilotMessageResult_DefaultsAreCorrect()
    {
        var result = new CopilotMessageResult();

        Assert.Null(result.MessageId);
        Assert.Equal(string.Empty, result.Content);
        Assert.Null(result.SessionId);
        Assert.NotNull(result.Events);
        Assert.Empty(result.Events);
        Assert.NotNull(result.UsageEvents);
        Assert.Empty(result.UsageEvents);
        Assert.Equal(0, result.TotalInputTokens);
        Assert.Equal(0, result.TotalOutputTokens);
        Assert.Null(result.ContextWindow);
    }

    [Fact]
    public void CopilotMessageResult_CanSetProperties()
    {
        var result = new CopilotMessageResult
        {
            MessageId = "msg-123",
            Content = "Hello, world!",
            SessionId = "session-456"
        };

        Assert.Equal("msg-123", result.MessageId);
        Assert.Equal("Hello, world!", result.Content);
        Assert.Equal("session-456", result.SessionId);
    }

    [Fact]
    public void CopilotMessageResult_CanAccumulateEvents()
    {
        var result = new CopilotMessageResult();
        var event1 = new SessionIdleEvent { Data = new SessionIdleData() };
        var event2 = new AssistantMessageEvent { Data = new AssistantMessageData { MessageId = "m1", Content = "test" } };

        result.Events.Add(event1);
        result.Events.Add(event2);

        Assert.Equal(2, result.Events.Count);
    }

    [Fact]
    public void CopilotMessageResult_TotalInputTokens_SumsUsageEvents()
    {
        var result = new CopilotMessageResult();
        result.UsageEvents.Add(new AssistantUsageData { Model = "gpt-4", InputTokens = 100, OutputTokens = 50 });
        result.UsageEvents.Add(new AssistantUsageData { Model = "gpt-4", InputTokens = 200, OutputTokens = 75 });

        Assert.Equal(300, result.TotalInputTokens);
        Assert.Equal(125, result.TotalOutputTokens);
    }

    [Fact]
    public void CopilotMessageResult_TotalTokens_HandlesNullValues()
    {
        var result = new CopilotMessageResult();
        result.UsageEvents.Add(new AssistantUsageData { Model = "gpt-4", InputTokens = 100, OutputTokens = null });
        result.UsageEvents.Add(new AssistantUsageData { Model = "gpt-4", InputTokens = null, OutputTokens = 50 });

        Assert.Equal(100, result.TotalInputTokens);
        Assert.Equal(50, result.TotalOutputTokens);
    }

    [Fact]
    public void CopilotMessageResult_ContextWindow_CanBeSet()
    {
        var result = new CopilotMessageResult();
        var contextData = new SessionUsageInfoData
        {
            TokenLimit = 128000,
            CurrentTokens = 5000,
            MessagesLength = 10
        };

        result.ContextWindow = contextData;

        Assert.NotNull(result.ContextWindow);
        Assert.Equal(128000, result.ContextWindow.TokenLimit);
        Assert.Equal(5000, result.ContextWindow.CurrentTokens);
        Assert.Equal(10, result.ContextWindow.MessagesLength);
    }

    [Fact]
    public void CopilotMessageResult_UsageEvents_AccumulateCorrectly()
    {
        var result = new CopilotMessageResult();
        var usage1 = new AssistantUsageData { Model = "gpt-4", InputTokens = 500, OutputTokens = 200, Cost = 0.01 };
        var usage2 = new AssistantUsageData { Model = "gpt-4", InputTokens = 300, OutputTokens = 150, Cost = 0.005 };

        result.UsageEvents.Add(usage1);
        result.UsageEvents.Add(usage2);

        Assert.Equal(2, result.UsageEvents.Count);
        Assert.Equal(800, result.TotalInputTokens);
        Assert.Equal(350, result.TotalOutputTokens);
        Assert.Equal("gpt-4", result.UsageEvents[0].Model);
    }

    [Fact]
    public void SendCopilotMessage_HasBlobDataParameter()
    {
        var prop = typeof(SendCopilotMessageCmdlet).GetProperty("BlobData")!;
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop.PropertyType);
    }

    [Fact]
    public void SendCopilotMessage_HasBlobMimeTypeParameter()
    {
        var prop = typeof(SendCopilotMessageCmdlet).GetProperty("BlobMimeType")!;
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop.PropertyType);
    }

    [Fact]
    public void SendCopilotMessage_BlobParametersDefaultToNull()
    {
        var cmdlet = new SendCopilotMessageCmdlet();
        Assert.Null(cmdlet.BlobData);
        Assert.Null(cmdlet.BlobMimeType);
    }

    [Fact]
    public void SendCopilotMessage_StopProcessingCancelsCts()
    {
        var cmdlet = new SendCopilotMessageCmdlet();

        // Trigger EndProcessing to initialize _cts, but it will throw because no session exists.
        // Instead, invoke StopProcessing via reflection and verify the _cts field behavior.
        // First call StopProcessing before EndProcessing — should not throw (null-safe).
        var stopMethod = typeof(SendCopilotMessageCmdlet).GetMethod(
            "StopProcessing",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        stopMethod.Invoke(cmdlet, null); // _cts is null — should be safe

        // Now set _cts via reflection to simulate mid-EndProcessing state
        var ctsField = typeof(SendCopilotMessageCmdlet).GetField(
            "_cts",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var cts = new CancellationTokenSource();
        ctsField.SetValue(cmdlet, cts);

        Assert.False(cts.IsCancellationRequested);
        stopMethod.Invoke(cmdlet, null);
        Assert.True(cts.IsCancellationRequested);
    }
}
