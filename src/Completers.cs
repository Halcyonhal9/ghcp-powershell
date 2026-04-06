using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;
using GitHub.Copilot.SDK;

namespace CopilotCmdlets;

/// <summary>
/// Provides tab-completion for -Model parameters by querying the SDK for available models.
/// </summary>
public sealed class CopilotModelCompleter : IArgumentCompleter
{
    public IEnumerable<CompletionResult> CompleteArgument(
        string commandName,
        string parameterName,
        string wordToComplete,
        CommandAst commandAst,
        IDictionary fakeBoundParameters)
    {
        var client = ModuleState.Client;
        if (client is null)
            yield break;

        IEnumerable<ModelInfo> models;
        try
        {
            models = client.ListModelsAsync(CancellationToken.None)
                .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Model completion failed: {ex.Message}");
            yield break;
        }

        var prefix = wordToComplete ?? string.Empty;

        foreach (var model in models)
        {
            var id = model.Id;
            if (id is null) continue;
            if (id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                yield return new CompletionResult(id, id, CompletionResultType.ParameterValue, id);
            }
        }
    }
}

/// <summary>
/// Provides tab-completion for -SessionId parameters by querying the SDK for existing sessions.
/// </summary>
public sealed class CopilotSessionCompleter : IArgumentCompleter
{
    public IEnumerable<CompletionResult> CompleteArgument(
        string commandName,
        string parameterName,
        string wordToComplete,
        CommandAst commandAst,
        IDictionary fakeBoundParameters)
    {
        var client = ModuleState.Client;
        if (client is null)
            yield break;

        IEnumerable<SessionMetadata> sessions;
        try
        {
            sessions = client.ListSessionsAsync(new SessionListFilter(), CancellationToken.None)
                .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Session completion failed: {ex.Message}");
            yield break;
        }

        var prefix = wordToComplete ?? string.Empty;

        foreach (var session in sessions)
        {
            var id = session.SessionId;
            if (id is null) continue;
            if (id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                yield return new CompletionResult(id, id, CompletionResultType.ParameterValue, id);
            }
        }
    }
}

/// <summary>
/// Provides tab-completion for -ReasoningEffort parameters with known effort levels.
/// </summary>
public sealed class ReasoningEffortCompleter : IArgumentCompleter
{
    private static readonly string[] Levels = { "low", "medium", "high" };

    public IEnumerable<CompletionResult> CompleteArgument(
        string commandName,
        string parameterName,
        string wordToComplete,
        CommandAst commandAst,
        IDictionary fakeBoundParameters)
    {
        var prefix = wordToComplete ?? string.Empty;

        foreach (var level in Levels)
        {
            if (level.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                yield return new CompletionResult(level, level, CompletionResultType.ParameterValue, level);
            }
        }
    }
}

/// <summary>
/// Provides tab-completion for -LogLevel parameters with standard log levels.
/// </summary>
public sealed class LogLevelCompleter : IArgumentCompleter
{
    private static readonly string[] Levels = { "trace", "debug", "info", "warn", "error" };

    public IEnumerable<CompletionResult> CompleteArgument(
        string commandName,
        string parameterName,
        string wordToComplete,
        CommandAst commandAst,
        IDictionary fakeBoundParameters)
    {
        var prefix = wordToComplete ?? string.Empty;

        foreach (var level in Levels)
        {
            if (level.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                yield return new CompletionResult(level, level, CompletionResultType.ParameterValue, level);
            }
        }
    }
}
