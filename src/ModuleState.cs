using System.Management.Automation;
using GitHub.Copilot.SDK;

namespace CopilotPS;

internal static class ModuleState
{
    private static CopilotClient? client;
    private static CopilotSession? currentSession;

    internal static CopilotClient? Client
    {
        get => client;
        set => client = value;
    }

    internal static CopilotSession? CurrentSession
    {
        get => currentSession;
        set => currentSession = value;
    }

    internal static CopilotClient RequireClient(CopilotClient? explicitClient)
    {
        return explicitClient ?? client ?? throw new PSInvalidOperationException(
            "No Copilot client available. Run New-CopilotClient first, or pass -Client explicitly.");
    }

    internal static CopilotSession RequireSession(CopilotSession? explicitSession)
    {
        return explicitSession ?? currentSession ?? throw new PSInvalidOperationException(
            "No Copilot session available. Run New-CopilotSession first, or pass -Session explicitly.");
    }

    internal static async Task CleanupAsync()
    {
        if (currentSession is not null)
        {
            await currentSession.DisposeAsync();
            currentSession = null;
        }

        if (client is not null)
        {
            await client.StopAsync();
            client.Dispose();
            client = null;
        }
    }
}

internal static class PermissionHandlers
{
    internal static PermissionRequestHandler Interactive => (request, invocation) =>
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Error.WriteLine($"[Permission] {request.Kind}");
        Console.ForegroundColor = originalColor;

        if (request.ExtensionData is not null)
        {
            foreach (var kvp in request.ExtensionData)
            {
                Console.Error.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }
        }

        Console.Error.Write("Allow? (y/n): ");
        var response = Console.ReadLine();
        var approved = string.Equals(response?.Trim(), "y", StringComparison.OrdinalIgnoreCase);

        return Task.FromResult(new PermissionRequestResult
        {
            Kind = new PermissionRequestResultKind(approved ? "approve" : "deny")
        });
    };

    internal static PermissionRequestHandler AutoApprove => (request, invocation) =>
    {
        return Task.FromResult(new PermissionRequestResult
        {
            Kind = new PermissionRequestResultKind("approve")
        });
    };
}

internal static class UserInputHandlers
{
    internal static UserInputHandler Interactive => (request, invocation) =>
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
            WasFreeform = request.Choices is null || request.Choices.Count == 0
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
