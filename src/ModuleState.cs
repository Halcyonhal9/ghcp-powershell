using System.Management.Automation;
using System.Runtime.InteropServices;
using GitHub.Copilot.SDK;

namespace CopilotCmdlets;

internal static class ModuleState
{
    private static volatile CopilotClient? _client;
    private static volatile CopilotSession? _currentSession;

    internal static CopilotClient? Client
    {
        get => _client;
        set => _client = value;
    }

    internal static CopilotSession? CurrentSession
    {
        get => _currentSession;
        set => _currentSession = value;
    }

    internal static CopilotClient RequireClient(CopilotClient? explicitClient)
    {
        return explicitClient ?? Client ?? throw new PSInvalidOperationException(
            "No Copilot client available. Run New-CopilotClient first, or pass -Client explicitly.");
    }

    internal static CopilotSession RequireSession(CopilotSession? explicitSession)
    {
        return explicitSession ?? CurrentSession ?? throw new PSInvalidOperationException(
            "No Copilot session available. Run New-CopilotSession first, or pass -Session explicitly.");
    }

    internal static bool TryRequireClient(CopilotClient? explicitClient, out CopilotClient client, out ErrorRecord? error)
    {
        try
        {
            client = RequireClient(explicitClient);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            client = null!;
            error = new ErrorRecord(ex, "NoClient", ErrorCategory.InvalidOperation, null);
            return false;
        }
    }

    internal static bool TryRequireSession(CopilotSession? explicitSession, out CopilotSession session, out ErrorRecord? error)
    {
        try
        {
            session = RequireSession(explicitSession);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            session = null!;
            error = new ErrorRecord(ex, "NoSession", ErrorCategory.InvalidOperation, null);
            return false;
        }
    }

    // RIDs shipped in the release pipeline. Keep in sync with the $runtimes list
    // in build.ps1 and .github/workflows/release.yml.
    private static readonly string[] SupportedRids = { "win-x64", "osx-arm64" };

    private const string ReleasesUrl = "https://github.com/Halcyonhal9/ghcp-powershell/releases/latest";

    internal static string? ResolveBundledCliPath()
    {
        var asmDir = Path.GetDirectoryName(typeof(ModuleState).Assembly.Location);
        if (asmDir is null) return null;

        var rid = RuntimeInformation.RuntimeIdentifier;
        var candidate = Path.Combine(
            asmDir, "runtimes", rid, "native",
            "copilot" + (OperatingSystem.IsWindows() ? ".exe" : ""));
        if (!File.Exists(candidate)) return null;

        // Gallery/zip packaging can strip the execute bit on non-Windows.
        // Set it if missing so the CLI can actually be launched.
        if (!OperatingSystem.IsWindows())
        {
            var mode = File.GetUnixFileMode(candidate);
            if ((mode & UnixFileMode.UserExecute) == 0)
            {
                File.SetUnixFileMode(candidate, mode | UnixFileMode.UserExecute);
            }
        }

        return candidate;
    }

    /// <summary>
    /// Builds a human-readable error message for callers that need the bundled CLI but
    /// could not find it. Distinguishes between a missing-from-payload case (supported RID
    /// but the file isn't on disk — usually means the gallery package was built incorrectly)
    /// and an unsupported-platform case (RID not shipped at all — point users at the
    /// per-platform release zips).
    /// </summary>
    internal static string BuildMissingCliMessage() =>
        BuildMissingCliMessage(RuntimeInformation.RuntimeIdentifier);

    internal static string BuildMissingCliMessage(string rid)
    {
        var isSupported = SupportedRids.Contains(rid, StringComparer.OrdinalIgnoreCase);

        if (isSupported)
        {
            return $"Could not locate the bundled Copilot CLI for runtime '{rid}'. " +
                   $"The module appears to be installed without its native payload. " +
                   $"Reinstall from {ReleasesUrl} (download the {rid} zip), or pass -CliPath to specify one explicitly.";
        }

        var supported = string.Join(", ", SupportedRids);
        return $"The bundled Copilot CLI is not shipped for runtime '{rid}'. " +
               $"Supported runtimes: {supported}. " +
               $"Install the GitHub Copilot CLI separately and pass its path with -CliPath, " +
               $"or download a per-platform release that matches your system from {ReleasesUrl}.";
    }

    internal static async Task CleanupAsync()
    {
        var session = CurrentSession;
        if (session is not null)
        {
            CurrentSession = null;
            try { await session.DisposeAsync(); } catch { }
        }

        var client = Client;
        if (client is not null)
        {
            Client = null;
            try { await client.StopAsync(); } catch { }
            try { client.Dispose(); } catch { }
        }
    }
}

internal static class PermissionHandlers
{
    internal static readonly PermissionRequestHandler Interactive = (request, invocation) =>
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Error.WriteLine($"[Permission] {request.Kind}");
        Console.ForegroundColor = originalColor;

        Console.Error.Write("Allow? (y/n): ");
        var response = Console.ReadLine();
        var approved = string.Equals(response?.Trim(), "y", StringComparison.OrdinalIgnoreCase);

        return Task.FromResult(new PermissionRequestResult
        {
            Kind = approved
                ? PermissionRequestResultKind.Approved
                : PermissionRequestResultKind.DeniedInteractivelyByUser
        });
    };

    internal static readonly PermissionRequestHandler AutoApprove = PermissionHandler.ApproveAll;
}

internal static class UserInputHandlers
{
    internal static readonly UserInputHandler Interactive = (request, invocation) =>
    {
        Console.Error.WriteLine($"[Input] {request.Question}");

        if (request.Choices is { Count: > 0 })
        {
            for (int i = 0; i < request.Choices.Count; i++)
            {
                Console.Error.WriteLine($"  [{i + 1}] {request.Choices[i]}");
            }
        }

        Console.Error.Write("> ");
        var answer = Console.ReadLine() ?? string.Empty;

        return Task.FromResult(new UserInputResponse
        {
            Answer = answer,
            WasFreeform = request.Choices is not { Count: > 0 }
        });
    };
}

public class ModuleCleanup : IModuleAssemblyCleanup
{
    public void OnRemove(PSModuleInfo psModuleInfo)
    {
        ModuleState.CleanupAsync().GetAwaiter().GetResult();
    }
}
