using System.Management.Automation;
using GitHub.Copilot.SDK;

namespace CopilotCmdlets;

[Cmdlet(VerbsCommon.New, "CopilotSession")]
[OutputType(typeof(CopilotSession))]
public sealed class NewCopilotSessionCmdlet : PSCmdlet
{
    [Parameter]
    public CopilotClient? Client { get; set; }

    [Parameter]
    public string? SessionId { get; set; }

    [Parameter]
    [ArgumentCompleter(typeof(CopilotModelCompleter))]
    public string? Model { get; set; }

    [Parameter]
    public string? SystemMessage { get; set; }

    [Parameter]
    [ArgumentCompleter(typeof(ReasoningEffortCompleter))]
    public string? ReasoningEffort { get; set; }

    [Parameter]
    public SwitchParameter AutoApprove { get; set; }

    [Parameter]
    public SwitchParameter InfiniteSessions { get; set; }

    [Parameter]
    public string? WorkingDirectory { get; set; }

    [Parameter]
    public string[]? AvailableTools { get; set; }

    [Parameter]
    public string[]? ExcludedTools { get; set; }

    [Parameter]
    public SwitchParameter EnableConfigDiscovery { get; set; }

    [Parameter]
    public string? Agent { get; set; }

    [Parameter]
    public string[]? SkillDirectories { get; set; }

    protected override void EndProcessing()
    {
        if (!ModuleState.TryRequireClient(Client, out var target, out var noClient))
        {
            ThrowTerminatingError(noClient!);
            return;
        }

        var config = new SessionConfig
        {
            Streaming = true,
            OnPermissionRequest = AutoApprove ? PermissionHandlers.AutoApprove : PermissionHandlers.Interactive,
            OnUserInputRequest = UserInputHandlers.Interactive
        };

        if (SessionId is not null) config.SessionId = SessionId;
        if (Model is not null) config.Model = Model;
        if (SystemMessage is not null) config.SystemMessage = new SystemMessageConfig { Content = SystemMessage };
        if (ReasoningEffort is not null) config.ReasoningEffort = ReasoningEffort;
        if (InfiniteSessions) config.InfiniteSessions = new InfiniteSessionConfig { Enabled = true };
        if (WorkingDirectory is not null) config.WorkingDirectory = WorkingDirectory;
        if (AvailableTools is not null) config.AvailableTools = new List<string>(AvailableTools);
        if (ExcludedTools is not null) config.ExcludedTools = new List<string>(ExcludedTools);
        if (EnableConfigDiscovery) config.EnableConfigDiscovery = true;
        if (Agent is not null) config.Agent = Agent;
        if (SkillDirectories is not null) config.SkillDirectories = new List<string>(SkillDirectories);

        try
        {
            var session = target.CreateSessionAsync(config, CancellationToken.None)
                .GetAwaiter().GetResult();
            ModuleState.CurrentSession = session;
            WriteObject(session);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "SessionCreateFailed", ErrorCategory.OpenError, null));
        }
    }
}

[Cmdlet(VerbsLifecycle.Resume, "CopilotSession")]
[OutputType(typeof(CopilotSession))]
public sealed class ResumeCopilotSessionCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    [ArgumentCompleter(typeof(CopilotSessionCompleter))]
    public string SessionId { get; set; } = null!;

    [Parameter]
    public CopilotClient? Client { get; set; }

    [Parameter]
    [ArgumentCompleter(typeof(CopilotModelCompleter))]
    public string? Model { get; set; }

    [Parameter]
    public SwitchParameter AutoApprove { get; set; }

    [Parameter]
    public string? SystemMessage { get; set; }

    [Parameter]
    [ArgumentCompleter(typeof(ReasoningEffortCompleter))]
    public string? ReasoningEffort { get; set; }

    [Parameter]
    public string? WorkingDirectory { get; set; }

    [Parameter]
    public SwitchParameter EnableConfigDiscovery { get; set; }

    [Parameter]
    public string? Agent { get; set; }

    [Parameter]
    public string[]? SkillDirectories { get; set; }

    protected override void EndProcessing()
    {
        if (!ModuleState.TryRequireClient(Client, out var target, out var noClient))
        {
            ThrowTerminatingError(noClient!);
            return;
        }

        var config = new ResumeSessionConfig
        {
            Streaming = true,
            OnPermissionRequest = AutoApprove ? PermissionHandlers.AutoApprove : PermissionHandlers.Interactive,
            OnUserInputRequest = UserInputHandlers.Interactive
        };

        if (Model is not null) config.Model = Model;
        if (SystemMessage is not null) config.SystemMessage = new SystemMessageConfig { Content = SystemMessage };
        if (ReasoningEffort is not null) config.ReasoningEffort = ReasoningEffort;
        if (WorkingDirectory is not null) config.WorkingDirectory = WorkingDirectory;
        if (EnableConfigDiscovery) config.EnableConfigDiscovery = true;
        if (Agent is not null) config.Agent = Agent;
        if (SkillDirectories is not null) config.SkillDirectories = new List<string>(SkillDirectories);

        try
        {
            var session = target.ResumeSessionAsync(SessionId, config, CancellationToken.None)
                .GetAwaiter().GetResult();
            ModuleState.CurrentSession = session;
            WriteObject(session);
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "SessionResumeFailed", ErrorCategory.OpenError, null));
        }
    }
}

[Cmdlet(VerbsCommon.Get, "CopilotSession")]
[OutputType(typeof(SessionMetadata))]
public sealed class GetCopilotSessionCmdlet : PSCmdlet
{
    [Parameter(Position = 0)]
    [ArgumentCompleter(typeof(CopilotSessionCompleter))]
    public string? SessionId { get; set; }

    [Parameter]
    public CopilotClient? Client { get; set; }

    protected override void EndProcessing()
    {
        if (!ModuleState.TryRequireClient(Client, out var target, out var noClient))
        {
            ThrowTerminatingError(noClient!);
            return;
        }

        try
        {
            if (SessionId is not null)
            {
                var metadata = target.GetSessionMetadataAsync(SessionId, CancellationToken.None)
                    .GetAwaiter().GetResult();
                if (metadata is not null)
                {
                    WriteObject(metadata);
                }
                else
                {
                    WriteError(new ErrorRecord(
                        new ItemNotFoundException($"Session '{SessionId}' was not found."),
                        "SessionNotFound", ErrorCategory.ObjectNotFound, SessionId));
                }
            }
            else
            {
                var sessions = target.ListSessionsAsync(new SessionListFilter(), CancellationToken.None)
                    .GetAwaiter().GetResult();
                WriteObject(sessions, enumerateCollection: true);
            }
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "SessionListFailed", ErrorCategory.ReadError, null));
        }
    }
}

[Cmdlet(VerbsCommon.Remove, "CopilotSession", SupportsShouldProcess = true)]
public sealed class RemoveCopilotSessionCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    [ArgumentCompleter(typeof(CopilotSessionCompleter))]
    public string SessionId { get; set; } = null!;

    [Parameter]
    public CopilotClient? Client { get; set; }

    protected override void EndProcessing()
    {
        if (!ModuleState.TryRequireClient(Client, out var target, out var noClient))
        {
            ThrowTerminatingError(noClient!);
            return;
        }

        if (!ShouldProcess(SessionId, "Delete session"))
            return;

        try
        {
            target.DeleteSessionAsync(SessionId, CancellationToken.None)
                .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "SessionDeleteFailed", ErrorCategory.WriteError, SessionId));
        }
    }
}

[Cmdlet(VerbsCommon.Close, "CopilotSession")]
public sealed class CloseCopilotSessionCmdlet : PSCmdlet
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
            target.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(
                ex, "SessionCloseFailed", ErrorCategory.CloseError, target));
            return;
        }

        if (ReferenceEquals(target, ModuleState.CurrentSession))
        {
            ModuleState.CurrentSession = null;
        }
    }
}
