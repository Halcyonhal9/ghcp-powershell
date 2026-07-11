using System.Collections.ObjectModel;
using System.Management.Automation;
using GitHub.Copilot;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.EndToEnd;

public sealed class NavigationGitHubTokenFactAttribute : FactAttribute
{
    public NavigationGitHubTokenFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GITHUB_TOKEN")))
        {
            Skip = "GITHUB_TOKEN is required for an authenticated navigation E2E session.";
        }
    }
}

[Trait("Category", "EndToEnd")]
[Collection("EndToEnd")]
public class NavigationCmdletTests : IAsyncLifetime
{
    private readonly List<string> sessionIds = [];
    private PowerShell ps = null!;
    private CopilotClient client = null!;

    public Task InitializeAsync()
    {
        ps = PowerShell.Create();
        ps.AddCommand("Import-Module")
            .AddParameter("Name", NavigationE2eModule.ResolveAssembly())
            .AddParameter("Force", true);
        ps.Invoke();
        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));

        ResetCommand();
        ps.AddCommand("New-CopilotClient");
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
            ps.AddParameter("GitHubToken", token);
        var results = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        client = Assert.IsType<CopilotClient>(Assert.Single(results).BaseObject);
        ResetCommand();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        try
        {
            ps.Commands.Clear();
            ps.AddCommand("Close-CopilotSession");
            ps.Invoke();
        }
        catch { }

        foreach (var sessionId in sessionIds.Distinct(StringComparer.Ordinal))
        {
            try
            {
                ps.Commands.Clear();
                ps.AddCommand("Remove-CopilotSession")
                    .AddParameter("SessionId", sessionId)
                    .AddParameter("Client", client)
                    .AddParameter("Confirm", false);
                ps.Invoke();
            }
            catch { }
        }

        try
        {
            ps.Commands.Clear();
            ps.AddCommand("Stop-CopilotClient")
                .AddParameter("Client", client)
                .AddParameter("Force", true);
            ps.Invoke();
        }
        catch { }

        ps.Dispose();
        return Task.CompletedTask;
    }

    [NavigationGitHubTokenFact]
    public async Task RegisterLifecycle_ReceivesOwnedSessionCreatedEventAndDisposes()
    {
        var firstSessionId = $"navigation-lifecycle-{Guid.NewGuid():N}";
        var secondSessionId = $"navigation-lifecycle-{Guid.NewGuid():N}";
        var firstCreated = new TaskCompletionSource<SessionLifecycleEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var disposedCallbackCount = 0;
        Action<SessionLifecycleEvent> callback = lifecycleEvent =>
        {
            if (lifecycleEvent is not SessionCreatedEvent)
                return;

            if (string.Equals(lifecycleEvent.SessionId, firstSessionId, StringComparison.Ordinal))
            {
                Interlocked.Increment(ref disposedCallbackCount);
                firstCreated.TrySetResult(lifecycleEvent);
            }
            else if (string.Equals(lifecycleEvent.SessionId, secondSessionId, StringComparison.Ordinal))
            {
                Interlocked.Increment(ref disposedCallbackCount);
            }
        };

        var subscription = RegisterLifecycle(callback);
        IDisposable? activeSubscription = null;
        CopilotSession? firstSession = null;
        CopilotSession? secondSession = null;
        try
        {
            firstSession = CreateSession(firstSessionId);
            var firstEvent = await firstCreated.Task.WaitAsync(TimeSpan.FromSeconds(15));

            Assert.IsType<SessionCreatedEvent>(firstEvent);
            Assert.Equal(firstSessionId, firstEvent.SessionId);
            Assert.Equal(1, Volatile.Read(ref disposedCallbackCount));

            subscription.Dispose();
            CloseSession(firstSession);
            firstSession = null;

            var secondCreated = new TaskCompletionSource<SessionLifecycleEvent>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            activeSubscription = RegisterLifecycle(lifecycleEvent =>
            {
                if (lifecycleEvent is SessionCreatedEvent
                    && string.Equals(
                        lifecycleEvent.SessionId,
                        secondSessionId,
                        StringComparison.Ordinal))
                {
                    secondCreated.TrySetResult(lifecycleEvent);
                }
            });

            secondSession = CreateSession(secondSessionId);
            await secondCreated.Task.WaitAsync(TimeSpan.FromSeconds(15));

            Assert.Equal(1, Volatile.Read(ref disposedCallbackCount));
        }
        finally
        {
            subscription.Dispose();
            activeSubscription?.Dispose();
            if (firstSession is not null)
                CloseSession(firstSession);
            if (secondSession is not null)
                CloseSession(secondSession);
        }
    }

    [NavigationGitHubTokenFact]
    public void GetLastSessionId_ReturnsSeededOwnedSession()
    {
        var sessionId = $"navigation-last-{Guid.NewGuid():N}";
        var session = CreateSession(sessionId);

        InvokeCommand(
            "Send-CopilotMessage",
            ("Session", session),
            ("Prompt", "Say exactly: navigation-last-ok"),
            ("Timeout", TimeSpan.FromSeconds(120)));
        CloseSession(session);

        var results = InvokeCommand(
            "Get-CopilotLastSessionId",
            ("Client", client));

        Assert.Equal(sessionId, Assert.IsType<string>(Assert.Single(results).BaseObject));
    }

    [Fact]
    public void GetForegroundSessionId_ReturnsNoOutputInHeadlessMode()
    {
        var results = InvokeCommand(
            "Get-CopilotForegroundSessionId",
            ("Client", client));

        Assert.Empty(results);
    }

    [Fact]
    public void SetForegroundSessionId_SurfacesHeadlessSdkError()
    {
        var sessionId = $"navigation-foreground-{Guid.NewGuid():N}";

        var error = InvokeExpectingError(
            "Set-CopilotForegroundSessionId",
            ("SessionId", sessionId),
            ("Client", client));

        Assert.StartsWith(
            "ForegroundSessionIdSetFailed",
            error.FullyQualifiedErrorId,
            StringComparison.Ordinal);
        Assert.Contains(
            "Not running in TUI+server mode",
            error.Exception.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    private IDisposable RegisterLifecycle(Action<SessionLifecycleEvent> callback)
    {
        var results = InvokeCommand(
            "Register-CopilotSessionLifecycleEvent",
            ("Client", client),
            ("ActionDelegate", callback));

        return Assert.IsAssignableFrom<IDisposable>(Assert.Single(results).BaseObject);
    }

    private CopilotSession CreateSession(string sessionId)
    {
        sessionIds.Add(sessionId);
        var results = InvokeCommand(
            "New-CopilotSession",
            ("Client", client),
            ("SessionId", sessionId),
            ("AutoApprove", true),
            ("AvailableTools", Array.Empty<string>()),
            ("MaxAiCredits", 50));

        return Assert.IsType<CopilotSession>(Assert.Single(results).BaseObject);
    }

    private void CloseSession(CopilotSession session)
    {
        InvokeCommand("Close-CopilotSession", ("Session", session));
    }

    private Collection<PSObject> InvokeCommand(
        string commandName,
        params (string Name, object Value)[] parameters)
    {
        ResetCommand();
        ps.AddCommand(commandName);
        foreach (var (name, value) in parameters)
            ps.AddParameter(name, value);

        var results = ps.Invoke();
        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        return results;
    }

    private ErrorRecord InvokeExpectingError(
        string commandName,
        params (string Name, object Value)[] parameters)
    {
        ResetCommand();
        ps.AddCommand(commandName);
        foreach (var (name, value) in parameters)
            ps.AddParameter(name, value);

        try
        {
            ps.Invoke();
        }
        catch (RuntimeException ex)
        {
            return ex.ErrorRecord;
        }

        Assert.True(ps.HadErrors, $"Expected {commandName} to fail.");
        return Assert.Single(ps.Streams.Error);
    }

    private void ResetCommand()
    {
        ps.Commands.Clear();
        ps.Streams.Error.Clear();
        ps.Streams.Warning.Clear();
    }
}

internal static class NavigationE2eModule
{
    internal static string ResolveAssembly()
    {
        var repoRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var assembly = Path.Combine(repoRoot, "out", "CopilotCmdlets.dll");
        const string publishHint =
            "Publish first: dotnet publish src/CopilotCmdlets.csproj -c Release -o out";

        if (!File.Exists(assembly))
        {
            throw new InvalidOperationException(
                $"Published assembly not found at {assembly}. {publishHint}");
        }

        var publishedAt = File.GetLastWriteTimeUtc(assembly);
        var newerSource = Directory
            .EnumerateFiles(Path.Combine(repoRoot, "src"), "*.cs")
            .FirstOrDefault(path => File.GetLastWriteTimeUtc(path) > publishedAt);
        if (newerSource is not null)
        {
            throw new InvalidOperationException(
                $"Published assembly is stale: {Path.GetFileName(newerSource)} is newer than out/CopilotCmdlets.dll. {publishHint}");
        }

        return assembly;
    }
}
