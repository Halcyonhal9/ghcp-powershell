using System.Management.Automation;
using GitHub.Copilot.SDK;

namespace CopilotCmdlets;

/// <summary>Structured result returned by Send-CopilotMessage after the session goes idle.</summary>
public class CopilotMessageResult
{
    /// <summary>The SDK-assigned message identifier (correlation key for future async patterns).</summary>
    public string? MessageId { get; set; }

    /// <summary>Full accumulated assistant response text.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>The session that produced this result.</summary>
    public string? SessionId { get; set; }

    /// <summary>All SDK events received during this send (deltas, tool calls, idle, errors, etc.).</summary>
    public List<SessionEvent> Events { get; set; } = new();

    /// <summary>User-supplied tag for correlating async results (e.g. conversation ID).</summary>
    public string? Tag { get; set; }

    /// <summary>Per-LLM-call usage events received during this send.</summary>
    public List<AssistantUsageData> UsageEvents { get; set; } = new();

    /// <summary>Aggregated input tokens across all LLM calls in this message.</summary>
    public double TotalInputTokens => UsageEvents.Sum(u => u.InputTokens ?? 0);

    /// <summary>Aggregated output tokens across all LLM calls in this message.</summary>
    public double TotalOutputTokens => UsageEvents.Sum(u => u.OutputTokens ?? 0);

    /// <summary>Context window state at last usage_info event.</summary>
    public SessionUsageInfoData? ContextWindow { get; set; }
}

[Cmdlet(VerbsCommunications.Send, "CopilotMessage")]
[OutputType(typeof(CopilotMessageResult))]
public sealed class SendCopilotMessageCmdlet : PSCmdlet
{
    private CancellationTokenSource? _cts;

    [Parameter(Mandatory = true, Position = 0)]
    public string Prompt { get; set; } = null!;

    [Parameter]
    public CopilotSession? Session { get; set; }

    [Parameter]
    public string[]? Attachment { get; set; }

    /// <summary>Base64-encoded binary data to attach inline (e.g. an image). Use with -BlobMimeType.</summary>
    [Parameter]
    public string? BlobData { get; set; }

    [Parameter]
    public string? BlobMimeType { get; set; }

    [Parameter]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    protected override void StopProcessing()
    {
        _cts?.Cancel();
    }

    protected override void EndProcessing()
    {
        _cts = new CancellationTokenSource();
        if (!ModuleState.TryRequireSession(Session, out var target, out var noSession))
        {
            ThrowTerminatingError(noSession!);
            return;
        }

        var result = new CopilotMessageResult
        {
            SessionId = target.SessionId
        };

        using var idleSignal = new ManualResetEventSlim(false);
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

                case AssistantUsageEvent usage:
                    result.UsageEvents.Add(usage.Data);
                    Console.Error.WriteLine(
                        $"[Usage] {usage.Data.Model}: in={usage.Data.InputTokens ?? 0}, out={usage.Data.OutputTokens ?? 0}, cost={usage.Data.Cost ?? 0}");
                    break;

                case SessionUsageInfoEvent usageInfo:
                    result.ContextWindow = usageInfo.Data;
                    break;

                case SessionIdleEvent:
                    Console.WriteLine(); // newline after streamed content
                    idleSignal.Set();
                    break;

                case SessionErrorEvent error:
                    Volatile.Write(ref sessionError, new Exception(
                        $"Session error ({error.Data.ErrorType}): {error.Data.Message}"));
                    idleSignal.Set();
                    break;
            }
        }));

        try
        {
            var options = new MessageOptions { Prompt = Prompt };

            var attachments = new List<UserMessageDataAttachmentsItem>();

            if (Attachment is { Length: > 0 })
            {
                attachments.AddRange(Attachment.Select(path =>
                    (UserMessageDataAttachmentsItem)new UserMessageDataAttachmentsItemFile
                    {
                        Path = path,
                        DisplayName = System.IO.Path.GetFileName(path),
                        Type = "file"
                    }));
            }

            if (BlobData is not null)
            {
                attachments.Add(new UserMessageDataAttachmentsItemBlob
                {
                    Data = BlobData,
                    MimeType = BlobMimeType ?? "application/octet-stream"
                });
            }

            if (attachments.Count > 0)
            {
                options.Attachments = attachments;
            }

            target.SendAsync(options, _cts.Token).GetAwaiter().GetResult();

            if (!idleSignal.Wait(Timeout, _cts.Token))
            {
                ThrowTerminatingError(new ErrorRecord(
                    new TimeoutException($"Send-CopilotMessage timed out after {Timeout}."),
                    "MessageTimeout", ErrorCategory.OperationTimeout, null));
                return;
            }

            var error = Volatile.Read(ref sessionError);
            if (error is not null)
            {
                ThrowTerminatingError(new ErrorRecord(
                    error, "SessionError", ErrorCategory.InvalidResult, null));
                return;
            }

            WriteObject(result);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            throw new PipelineStoppedException();
        }
        finally
        {
            subscription.Dispose();
            _cts.Dispose();
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
        if (!ModuleState.TryRequireSession(Session, out var target, out var noSession))
        {
            ThrowTerminatingError(noSession!);
            return;
        }

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
