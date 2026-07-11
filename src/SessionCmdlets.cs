using System.Collections;
using System.Management.Automation;
using GitHub.Copilot;
using Microsoft.Extensions.AI;

namespace CopilotCmdlets;

/// <summary>
/// Shared parameters for cmdlets that build a <see cref="SessionConfigBase"/>
/// (New-CopilotSession and Resume-CopilotSession).
/// </summary>
public abstract class SessionConfigCmdletBase : PSCmdlet
{
    [Parameter]
    public CopilotClient? Client { get; set; }

    [Parameter]
    [ArgumentCompleter(typeof(CopilotModelCompleter))]
    public string? Model { get; set; }

    [Parameter]
    public string? SystemMessage { get; set; }

    [Parameter]
    [ArgumentCompleter(typeof(SystemMessageModeCompleter))]
    public string? SystemMessageMode { get; set; }

    [Parameter]
    public Hashtable? SystemMessageSections { get; set; }

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

    [Parameter]
    public string[]? DisabledSkills { get; set; }

    [Parameter]
    public SwitchParameter EnableCitations { get; set; }

    [Parameter]
    public string[]? ExcludedBuiltInAgents { get; set; }

    /// <summary>Per-session AI-credit budget (SessionLimits.MaxAiCredits).</summary>
    [Parameter]
    public double? MaxAiCredits { get; set; }

    /// <summary>MCP servers keyed by name. Values are hashtables with either
    /// 'Command' (stdio) or 'Url' (http) plus optional settings.</summary>
    [Parameter]
    public Hashtable? McpServers { get; set; }

    /// <summary>Custom tools created with New-CopilotTool (or any AIFunction).</summary>
    [Parameter]
    public AIFunction[]? Tool { get; set; }

    internal void ApplyCommonOptions(SessionConfigBase config)
    {
        config.Streaming = true;
        config.OnPermissionRequest = AutoApprove ? PermissionHandlers.AutoApprove : PermissionHandlers.Interactive;
        config.OnUserInputRequest = UserInputHandlers.Interactive;

        if (Model is not null) config.Model = Model;
        var systemMessage = SystemMessageHelper.Build(SystemMessage, SystemMessageMode, SystemMessageSections);
        if (systemMessage is not null) config.SystemMessage = systemMessage;
        if (ReasoningEffort is not null) config.ReasoningEffort = ReasoningEffort;
        if (InfiniteSessions) config.InfiniteSessions = new InfiniteSessionConfig { Enabled = true };
        if (WorkingDirectory is not null)
            config.WorkingDirectory = GetUnresolvedProviderPathFromPSPath(WorkingDirectory);
        if (AvailableTools is not null) config.AvailableTools = new List<string>(AvailableTools);
        if (ExcludedTools is not null) config.ExcludedTools = new List<string>(ExcludedTools);
        if (EnableConfigDiscovery) config.EnableConfigDiscovery = true;
        if (Agent is not null) config.Agent = Agent;
        if (SkillDirectories is not null) config.SkillDirectories = new List<string>(SkillDirectories);
        if (DisabledSkills is not null) config.DisabledSkills = new List<string>(DisabledSkills);
        if (EnableCitations) config.EnableCitations = true;
        if (ExcludedBuiltInAgents is not null) config.ExcludedBuiltInAgents = new List<string>(ExcludedBuiltInAgents);
        if (MaxAiCredits is not null) config.SessionLimits = new SessionLimitsConfig { MaxAiCredits = MaxAiCredits };
        if (McpServers is not null) config.McpServers = McpServerHelper.Build(McpServers);
        if (Tool is { Length: > 0 }) config.Tools = new List<AIFunctionDeclaration>(Tool);
    }
}

[Cmdlet(VerbsCommon.New, "CopilotSession")]
[OutputType(typeof(CopilotSession))]
public sealed class NewCopilotSessionCmdlet : SessionConfigCmdletBase
{
    [Parameter]
    public string? SessionId { get; set; }

    protected override void EndProcessing()
    {
        if (!ModuleState.TryRequireClient(Client, out var target, out var noClient))
        {
            ThrowTerminatingError(noClient!);
            return;
        }

        var config = new SessionConfig();
        ApplyCommonOptions(config);
        if (SessionId is not null) config.SessionId = SessionId;

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
public sealed class ResumeCopilotSessionCmdlet : SessionConfigCmdletBase
{
    [Parameter(Mandatory = true, Position = 0)]
    [ArgumentCompleter(typeof(CopilotSessionCompleter))]
    public string SessionId { get; set; } = null!;

    /// <summary>Resume any queued work the session had pending when it was last closed.</summary>
    [Parameter]
    public SwitchParameter ContinuePendingWork { get; set; }

    protected override void EndProcessing()
    {
        if (!ModuleState.TryRequireClient(Client, out var target, out var noClient))
        {
            ThrowTerminatingError(noClient!);
            return;
        }

        var config = new ResumeSessionConfig();
        ApplyCommonOptions(config);
        if (ContinuePendingWork) config.ContinuePendingWork = true;

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
    [CopilotSessionTransformation]
    [ArgumentCompleter(typeof(CopilotSessionCompleter))]
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

[Cmdlet(VerbsCommon.New, "CopilotSectionOverride")]
[OutputType(typeof(SectionOverride))]
public sealed class NewCopilotSectionOverrideCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    [ArgumentCompleter(typeof(SectionOverrideActionCompleter))]
    public string Action { get; set; } = null!;

    [Parameter(Position = 1)]
    public string? Content { get; set; }

    protected override void EndProcessing()
    {
        if (!Enum.TryParse<SectionOverrideAction>(Action, ignoreCase: true, out var action))
        {
            ThrowTerminatingError(new ErrorRecord(
                new ArgumentException($"Invalid action '{Action}'. Valid values: Replace, Remove, Append, Prepend."),
                "InvalidSectionAction", ErrorCategory.InvalidArgument, Action));
            return;
        }

        var sectionOverride = new SectionOverride { Action = action };
        if (Content is not null) sectionOverride.Content = Content;
        WriteObject(sectionOverride);
    }
}

internal static class McpServerHelper
{
    /// <summary>
    /// Converts a PowerShell hashtable of server-name → settings-hashtable into
    /// SDK MCP server configs. Settings with 'Command' become stdio servers;
    /// settings with 'Url' become HTTP servers.
    /// </summary>
    internal static Dictionary<string, McpServerConfig> Build(Hashtable servers)
    {
        var result = new Dictionary<string, McpServerConfig>();
        foreach (DictionaryEntry entry in servers)
        {
            var name = entry.Key.ToString()!;
            var value = Unwrap(entry.Value);
            if (value is not Hashtable settings)
            {
                throw new ArgumentException(
                    $"MCP server '{name}' must be a hashtable of settings, got {value?.GetType().Name ?? "null"}.");
            }

            result[name] = BuildServer(name, settings);
        }
        return result;
    }

    private static McpServerConfig BuildServer(string name, Hashtable settings)
    {
        var command = GetString(settings, "Command");
        var url = GetString(settings, "Url");

        if (command is null == url is null)
        {
            throw new ArgumentException(
                $"MCP server '{name}' must specify exactly one of 'Command' (stdio) or 'Url' (http).");
        }

        McpServerConfig config;
        if (command is not null)
        {
            config = new McpStdioServerConfig
            {
                Command = command,
                Args = GetStringList(settings, "Args"),
                Env = GetStringMap(settings, "Env"),
                WorkingDirectory = GetString(settings, "WorkingDirectory") ?? GetString(settings, "Cwd")
            };
        }
        else
        {
            config = new McpHttpServerConfig
            {
                Url = url!,
                Headers = GetStringMap(settings, "Headers")
            };
        }

        config.Tools = GetStringList(settings, "Tools");
        if (GetValue(settings, "Timeout") is { } timeout)
            config.Timeout = Convert.ToInt32(timeout);
        return config;
    }

    private static object? Unwrap(object? value)
        => value is PSObject psObject ? psObject.BaseObject : value;

    private static object? GetValue(Hashtable settings, string key)
        => Unwrap(settings[key]);

    private static string? GetString(Hashtable settings, string key)
        => GetValue(settings, key)?.ToString();

    private static List<string>? GetStringList(Hashtable settings, string key)
    {
        return GetValue(settings, key) switch
        {
            null => null,
            string single => [single],
            IEnumerable items => items.Cast<object?>()
                .Select(i => Unwrap(i)?.ToString() ?? string.Empty)
                .ToList(),
            var other => [other.ToString() ?? string.Empty]
        };
    }

    private static Dictionary<string, string>? GetStringMap(Hashtable settings, string key)
    {
        if (GetValue(settings, key) is not Hashtable map) return null;
        var result = new Dictionary<string, string>();
        foreach (DictionaryEntry entry in map)
        {
            result[entry.Key.ToString()!] = Unwrap(entry.Value)?.ToString() ?? string.Empty;
        }
        return result;
    }
}

internal static class SystemMessageHelper
{
    internal static SystemMessageConfig? Build(string? content, string? mode, Hashtable? sections)
    {
        if (content is null && mode is null && sections is null)
            return null;

        var config = new SystemMessageConfig();
        if (content is not null) config.Content = content;
        if (mode is not null)
        {
            if (Enum.TryParse<SystemMessageMode>(mode, ignoreCase: true, out var parsed))
                config.Mode = parsed;
            else
                throw new ArgumentException(
                    $"Invalid SystemMessageMode '{mode}'. Valid values: Append, Replace, Customize.");
        }
        if (sections is not null)
        {
            var dict = new Dictionary<SystemMessageSection, SectionOverride>();
            foreach (DictionaryEntry entry in sections)
            {
                var key = entry.Key.ToString()!;
                if (entry.Value is SectionOverride so)
                {
                    dict[new SystemMessageSection(key)] = so;
                }
                else if (entry.Value is Hashtable ht)
                {
                    var sectionOverride = new SectionOverride();
                    if (ht["Action"] is string actionStr)
                    {
                        if (Enum.TryParse<SectionOverrideAction>(actionStr, ignoreCase: true, out var action))
                            sectionOverride.Action = action;
                        else
                            throw new ArgumentException(
                                $"Invalid SectionOverrideAction '{actionStr}' for section '{key}'. Valid values: Replace, Remove, Append, Prepend.");
                    }
                    if (ht["Content"] is string c)
                        sectionOverride.Content = c;
                    dict[new SystemMessageSection(key)] = sectionOverride;
                }
                else
                {
                    throw new ArgumentException(
                        $"Section '{key}' must be a SectionOverride or Hashtable, got {entry.Value?.GetType().Name ?? "null"}.");
                }
            }
            config.Sections = dict;
        }
        return config;
    }
}
