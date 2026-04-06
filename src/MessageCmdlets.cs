using System.Management.Automation;
using GitHub.Copilot.SDK;

namespace CopilotPS;

public class CopilotMessageResult
{
    public string? MessageId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public List<SessionEvent> Events { get; set; } = new();
}

[Cmdlet(VerbsCommunications.Send, "CopilotMessage")]
[OutputType(typeof(CopilotMessageResult))]
public sealed class SendCopilotMessageCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Prompt { get; set; } = null!;

    [Parameter]
    public CopilotSession? Session { get; set; }

    [Parameter]
    public string[]? Attachment { get; set; }

    [Parameter]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    protected override void EndProcessing()
    {
        var target = ModuleState.RequireSession(Session);

        var result = new CopilotMessageResult
        {
            SessionId = target.SessionId
        };

        var idleSignal = new ManualResetEventSlim(false);
        Exception? sessionError = null;

        var subscription = target.On(new SessionEventHandler(sessionEvent =>
        {
            result.Events.Add(sessionEvent);

            switch (sessionEvent)
            {
                case AssistantMessageDeltaEvent delta:
                    Console.Write(delta.Data.DeltaContent);
                    break;

                case AssistantMessageEvent msg:
                    result.MessageId = msg.Data.MessageId;
                    result.Content = msg.Data.Content ?? string.Empty;
                    break;

                case ToolExecutionStartEvent toolStart:
                    Console.Error.WriteLine($"[Tool] {toolStart.Data.ToolName} (id: {toolStart.Data.ToolCallId})");
                    break;

                case ToolExecutionCompleteEvent toolEnd:
                    var status = toolEnd.Data.Success ? "completed" : "failed";
                    Console.Error.WriteLine($"[Tool] {status} (id: {toolEnd.Data.ToolCallId})");
                    break;

                case SessionIdleEvent:
                    Console.WriteLine(); // newline after streamed content
                    idleSignal.Set();
                    break;

                case SessionErrorEvent error:
                    sessionError = new Exception(
                        $"Session error ({error.Data.ErrorType}): {error.Data.Message}");
                    idleSignal.Set();
                    break;
            }
        }));

        try
        {
            var options = new MessageOptions { Prompt = Prompt };

            if (Attachment is { Length: > 0 })
            {
                options.Attachments = Attachment
                    .Select(path => (UserMessageDataAttachmentsItem)new UserMessageDataAttachmentsItemFile
                    {
                        Path = path,
                        DisplayName = System.IO.Path.GetFileName(path),
                        Type = "file"
                    })
                    .ToList();
            }

            target.SendAsync(options, CancellationToken.None).GetAwaiter().GetResult();

            if (!idleSignal.Wait(Timeout))
            {
                ThrowTerminatingError(new ErrorRecord(
                    new TimeoutException($"Send-CopilotMessage timed out after {Timeout}."),
                    "MessageTimeout", ErrorCategory.OperationTimeout, null));
                return;
            }

            if (sessionError is not null)
            {
                ThrowTerminatingError(new ErrorRecord(
                    sessionError, "SessionError", ErrorCategory.InvalidResult, null));
                return;
            }

            WriteObject(result);
        }
        finally
        {
            subscription.Dispose();
        }
    }
}

[Cmdlet(VerbsCommon.Get, "CopilotMessage")]
[OutputType(typeof(SessionEvent))]
public sealed class GetCopilotMessageCmdlet : PSCmdlet
{
    [Parameter]
    public CopilotSession? Session { get; set; }

    protected override void EndProcessing()
    {
        var target = ModuleState.RequireSession(Session);

        try
        {
            var messages = target.GetMessagesAsync(CancellationToken.None)
                .GetAwaiter().GetResult();
            WriteObject(messages, enumerateCollection: true);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "GetMessagesFailed", ErrorCategory.ReadError, null));
        }
    }
}
