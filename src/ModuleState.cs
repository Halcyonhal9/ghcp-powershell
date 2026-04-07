using System.Management.Automation;
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
