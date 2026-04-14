using System.Management.Automation;
using GitHub.Copilot.SDK;

namespace CopilotCmdlets;

/// <summary>
/// Wraps an in-flight Send-CopilotMessage call. Returned by Send-CopilotMessageAsync;
/// collected by Receive-CopilotAsyncResult.
/// </summary>
public class CopilotAsyncResult : IDisposable
{
    private readonly ManualResetEventSlim _idleSignal = new(false);
    private readonly CancellationTokenSource _cts = new();
    private readonly object _eventLock = new();
    private IDisposable? _subscription;
    private volatile Exception? _sessionError;
    private volatile bool _disposed;

    /// <summary>User-supplied tag for correlating results (e.g. conversation ID).</summary>
    public string? Tag { get; set; }

    /// <summary>The session this message was sent to.</summary>
    public CopilotSession Session { get; }

    /// <summary>True once the session has gone idle or errored.</summary>
    public bool IsCompleted => _idleSignal.IsSet;

    /// <summary>The accumulated result. Populated progressively as events arrive.</summary>
    public CopilotMessageResult Result { get; }

    internal CopilotAsyncResult(CopilotSession session, string? tag)
    {
        Session = session;
        Tag = tag;
        Result = new CopilotMessageResult { SessionId = session.SessionId };
    }

    internal void Start(MessageOptions options)
    {
        _subscription = Session.On(new SessionEventHandler(OnEvent));
        Session.SendAsync(options, _cts.Token).GetAwaiter().GetResult();
    }

    private void OnEvent(SessionEvent sessionEvent)
    {
        lock (_eventLock)
        {
            Result.Events.Add(sessionEvent);

            switch (sessionEvent)
            {
                case AssistantMessageEvent msg:
                    Result.MessageId = msg.Data.MessageId;
                    Result.Content = msg.Data.Content ?? string.Empty;
                    break;

                case AssistantUsageEvent usage:
                    Result.UsageEvents.Add(usage.Data);
                    break;

                case SessionUsageInfoEvent usageInfo:
                    Result.ContextWindow = usageInfo.Data;
                    break;

                case SessionIdleEvent:
                    _idleSignal.Set();
                    break;

                case SessionErrorEvent error:
                    _sessionError = new Exception(
                        $"Session error ({error.Data.ErrorType}): {error.Data.Message}");
                    _idleSignal.Set();
                    break;
            }
        }
    }

    /// <summary>Block until idle/error or timeout. Returns true if completed, false on timeout.</summary>
    internal bool Wait(TimeSpan timeout)
    {
        try
        {
            return _idleSignal.Wait(timeout, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            return _idleSignal.IsSet;
        }
    }

    /// <summary>Returns the session error if one occurred, otherwise null.</summary>
    internal Exception? GetError() => _sessionError;

    /// <summary>Cancel the in-flight request.</summary>
    public void Cancel() => _cts.Cancel();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _subscription?.Dispose();
        _cts.Dispose();
        _idleSignal.Dispose();
    }
}

/// <summary>
/// Send-CopilotMessageAsync — fires a message to a session and returns immediately
/// with a CopilotAsyncResult handle. Use Receive-CopilotAsyncResult to collect.
/// </summary>
[Cmdlet(VerbsCommunications.Send, "CopilotMessageAsync")]
[OutputType(typeof(CopilotAsyncResult))]
public sealed class SendCopilotMessageAsyncCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Prompt { get; set; } = null!;

    [Parameter]
    public CopilotSession? Session { get; set; }

    [Parameter]
    public string? Tag { get; set; }

    [Parameter]
    public string[]? Attachment { get; set; }

    /// <summary>Base64-encoded binary data to attach inline (e.g. an image). Use with -BlobMimeType.</summary>
    [Parameter]
    public string? BlobData { get; set; }

    [Parameter]
    public string? BlobMimeType { get; set; }

    protected override void EndProcessing()
    {
        if (!ModuleState.TryRequireSession(Session, out var target, out var noSession))
        {
            ThrowTerminatingError(noSession!);
            return;
        }

        var asyncResult = new CopilotAsyncResult(target, Tag);

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

        try
        {
            asyncResult.Start(options);
            WriteObject(asyncResult);
        }
        catch (Exception ex)
        {
            asyncResult.Dispose();
            ThrowTerminatingError(new ErrorRecord(
                ex, "AsyncSendFailed", ErrorCategory.InvalidOperation, null));
        }
    }
}

/// <summary>
/// Receive-CopilotAsyncResult — waits for one or more CopilotAsyncResult handles to complete.
/// Returns CopilotMessageResult objects tagged with the original Tag.
/// </summary>
[Cmdlet(VerbsCommunications.Receive, "CopilotAsyncResult")]
[OutputType(typeof(CopilotMessageResult))]
public sealed class ReceiveCopilotAsyncResultCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    public CopilotAsyncResult Result { get; set; } = null!;

    [Parameter]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);

    [Parameter]
    public SwitchParameter DisposeSession { get; set; }

    protected override void ProcessRecord()
    {
        var asyncResult = Result;
        try
        {
            if (!asyncResult.Wait(Timeout))
            {
                WriteWarning($"[{asyncResult.Tag}] Timed out after {Timeout}");
            }

            var error = asyncResult.GetError();
            if (error is not null)
            {
                WriteError(new ErrorRecord(
                    error, "AsyncSessionError", ErrorCategory.InvalidResult, asyncResult.Tag));
            }

            asyncResult.Result.Tag = asyncResult.Tag;
            WriteObject(asyncResult.Result);
        }
        finally
        {
            asyncResult.Dispose();

            if (DisposeSession)
            {
                try { asyncResult.Session.DisposeAsync().GetAwaiter().GetResult(); }
                catch { /* best-effort cleanup */ }
            }
        }
    }
}
