using System.Management.Automation;
using System.Management.Automation.Internal;
using System.Management.Automation.Runspaces;

namespace CopilotCmdlets;

internal sealed class PowerShellCallbackRunner
{
    private readonly string scriptText;
    private readonly PSLanguageMode languageMode;

    internal PowerShellCallbackRunner(ScriptBlock scriptBlock, PSLanguageMode languageMode)
    {
        ArgumentNullException.ThrowIfNull(scriptBlock);

        scriptText = scriptBlock.ToString();
        this.languageMode = languageMode;
    }

    internal async Task<T> InvokeRequiredAsync<T>(params object?[] arguments)
    {
        var output = await InvokeAsync(arguments, null, formatOutput: false, CancellationToken.None)
            .ConfigureAwait(false);
        var value = GetSingleResult(output, allowNull: false);

        if (value is T result)
            return result;

        throw new InvalidOperationException(
            $"Callback must return a single {typeof(T).FullName} value, got {value?.GetType().FullName ?? "null"}.");
    }

    internal async Task<T?> InvokeOptionalAsync<T>(params object?[] arguments)
        where T : class
    {
        var output = await InvokeAsync(arguments, null, formatOutput: false, CancellationToken.None)
            .ConfigureAwait(false);
        var value = GetSingleResult(output, allowNull: true);

        if (value is null)
            return null;
        if (value is T result)
            return result;

        throw new InvalidOperationException(
            $"Callback must return zero values or a single {typeof(T).FullName} value, got {value.GetType().FullName}.");
    }

    internal async Task InvokeVoidAsync(params object?[] arguments)
    {
        await InvokeAsync(arguments, null, formatOutput: false, CancellationToken.None)
            .ConfigureAwait(false);
    }

    internal async Task<string> InvokeTextAsync(
        IEnumerable<KeyValuePair<string, object?>> namedArguments,
        CancellationToken cancellationToken)
    {
        var output = await InvokeAsync(
                Array.Empty<object?>(),
                namedArguments,
                formatOutput: true,
                cancellationToken)
            .ConfigureAwait(false);

        return string.Concat(output.Select(item => item?.ToString())).Trim();
    }

    internal static object? UnwrapResult(object? value)
    {
        while (value is PSObject psObject)
        {
            value = psObject.BaseObject;
        }

        return ReferenceEquals(value, AutomationNull.Value) ? null : value;
    }

    private async Task<PSDataCollection<PSObject>> InvokeAsync(
        IReadOnlyList<object?> positionalArguments,
        IEnumerable<KeyValuePair<string, object?>>? namedArguments,
        bool formatOutput,
        CancellationToken cancellationToken)
    {
        var initialSessionState = InitialSessionState.CreateDefault2();
        initialSessionState.LanguageMode = languageMode;

        using var runspace = RunspaceFactory.CreateRunspace(initialSessionState);
        runspace.Open();

        using var ps = PowerShell.Create();
        ps.Runspace = runspace;
        ps.AddScript(scriptText);

        foreach (var argument in positionalArguments)
        {
            ps.AddArgument(argument);
        }

        if (namedArguments is not null)
        {
            foreach (var (name, value) in namedArguments)
            {
                ps.AddParameter(name, value);
            }
        }

        if (formatOutput)
            ps.AddCommand("Out-String");

        using var registration = cancellationToken.Register(ps.Stop);
        var output = await ps.InvokeAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (ps.HadErrors)
        {
            var errors = string.Join(
                Environment.NewLine,
                ps.Streams.Error.ReadAll().Select(error => error.ToString()));
            throw new InvalidOperationException(
                errors.Length > 0 ? errors : "PowerShell callback failed.");
        }

        return output;
    }

    private static object? GetSingleResult(PSDataCollection<PSObject> output, bool allowNull)
    {
        if (output.Count == 0)
        {
            if (allowNull)
                return null;

            throw new InvalidOperationException("Callback must return exactly one value, but returned no values.");
        }

        if (output.Count != 1)
        {
            throw new InvalidOperationException(
                $"Callback must return exactly one value, but returned {output.Count} values.");
        }

        var value = UnwrapResult(output[0]);
        if (value is null && !allowNull)
        {
            throw new InvalidOperationException("Callback must return exactly one non-null value.");
        }

        return value;
    }
}
