#pragma warning disable GHCP001 // experimental SDK members: CommandsListRequest
using System.Management.Automation;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using Microsoft.Extensions.AI;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.EndToEnd;

[Trait("Category", "EndToEnd")]
[Collection("EndToEnd")]
public class CallbackBuilderTests : IAsyncLifetime
{
    private readonly string id = Guid.NewGuid().ToString("N");
    private readonly string sessionId = $"callback-e2e-{Guid.NewGuid():N}";
    private PowerShell ps = null!;
    private CopilotSession? session;
    private string workDirectory = null!;

    public Task InitializeAsync()
    {
        workDirectory = Path.Combine(AppContext.BaseDirectory, $"callback-e2e-{id}");
        Directory.CreateDirectory(workDirectory);

        ps = PowerShell.Create();
        var assembly = Path.ChangeExtension(E2eModule.ResolveManifest(), ".dll");
        ps.AddCommand("Import-Module").AddParameter("Name", assembly);
        ps.Invoke();
        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        ResetPowerShell();

        ps.AddCommand("New-CopilotClient");
        ps.Invoke();
        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        ResetPowerShell();

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (ps is not null)
        {
            try
            {
                CloseSession();
            }
            catch { }

            try
            {
                ResetPowerShell();
                ps.AddCommand("Remove-CopilotSession")
                    .AddParameter("SessionId", sessionId)
                    .AddParameter("Confirm", false);
                ps.Invoke();
            }
            catch { }

            try
            {
                ResetPowerShell();
                ps.AddCommand("Stop-CopilotClient").AddParameter("Force", true);
                ps.Invoke();
            }
            catch { }

            ps.Dispose();
        }

        try
        {
            if (Directory.Exists(workDirectory))
                Directory.Delete(workDirectory, recursive: true);
        }
        catch { }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Command_ExecutesThroughSessionRpcAndForwardsHandlerErrors()
    {
        var commandName = $"callback_command_{id}";
        var failureText = $"command-failure-{id}";
        var outputPath = Path.Combine(workDirectory, "command.txt");
        var script = ScriptBlock.Create($$"""
            param($context)
            if ($context.Args -eq 'fail') { throw '{{failureText}}' }
            [System.IO.File]::WriteAllText(
                '{{EscapePowerShellLiteral(outputPath)}}',
                "$($context.SessionId)|$($context.Command)|$($context.CommandName)|$($context.Args)")
            """);

        ps.AddCommand("New-CopilotCommand")
            .AddParameter("Name", commandName)
            .AddParameter("Description", "Callback E2E command")
            .AddParameter("ScriptBlock", script);
        var commandResults = ps.Invoke();
        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        var command = Assert.IsType<CommandDefinition>(
            Assert.Single(commandResults).BaseObject);
        ResetPowerShell();

        ps.AddCommand("New-CopilotSession")
            .AddParameter("SessionId", sessionId)
            .AddParameter("AutoApprove", true)
            .AddParameter("AvailableTools", Array.Empty<string>())
            .AddParameter("MaxAiCredits", 50)
            .AddParameter("Commands", new[] { command });
        var sessionResults = ps.Invoke();
        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        session = Assert.IsType<CopilotSession>(Assert.Single(sessionResults).BaseObject);
        ResetPowerShell();

        await WaitForAsync(
            async cancellationToken =>
            {
                var commands = await session.Rpc.Commands.ListAsync(
                    new CommandsListRequest
                    {
                        IncludeBuiltins = false,
                        IncludeClientCommands = true,
                        IncludeSkills = false
                    },
                    cancellationToken);
                return commands.Commands.Any(command =>
                    string.Equals(
                        command.Name,
                        commandName,
                        StringComparison.OrdinalIgnoreCase));
            },
            TimeSpan.FromSeconds(30),
            "Timed out waiting for the custom command to register.");

        using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
        {
            var result = await session.Rpc.Commands.ExecuteAsync(
                commandName, "production", cancellation.Token);
            Assert.Null(result.Error);
        }

        await WaitForAsync(
            _ => Task.FromResult(File.Exists(outputPath)),
            TimeSpan.FromSeconds(10),
            "Timed out waiting for the command callback output.");
        Assert.Equal(
            $"{session.SessionId}|/{commandName} production|{commandName}|production",
            await File.ReadAllTextAsync(outputPath));

        using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
        {
            var result = await session.Rpc.Commands.ExecuteAsync(
                commandName, "fail", cancellation.Token);
            Assert.Contains(failureText, result.Error);
        }
    }

    [Fact]
    public async Task Hooks_RunForSessionLifecycleAndSuccessfulCustomTool()
    {
        var toolName = $"callback_tool_{id}";
        var toolResult = $"callback-result-{id}";
        var startPath = Path.Combine(workDirectory, "start.txt");
        var prePath = Path.Combine(workDirectory, "pre.txt");
        var postPath = Path.Combine(workDirectory, "post.txt");
        var endPath = Path.Combine(workDirectory, "end.txt");

        ps.AddCommand("New-CopilotSessionHooks")
            .AddParameter("OnSessionStart", ScriptBlock.Create($$"""
                param($hookInput, $invocation)
                [System.IO.File]::WriteAllText(
                    '{{EscapePowerShellLiteral(startPath)}}',
                    "$($hookInput.SessionId)|$($invocation.SessionId)")
                $null
                """))
            .AddParameter("OnPreToolUse", ScriptBlock.Create($$"""
                param($hookInput, $invocation)
                if ($hookInput.ToolName -eq '{{toolName}}') {
                    [System.IO.File]::WriteAllText(
                        '{{EscapePowerShellLiteral(prePath)}}',
                        "$($hookInput.ToolName)|$($invocation.SessionId)")
                }
                [GitHub.Copilot.PreToolUseHookOutput]@{ PermissionDecision = 'allow' }
                """))
            .AddParameter("OnPostToolUse", ScriptBlock.Create($$"""
                param($hookInput, $invocation)
                if ($hookInput.ToolName -eq '{{toolName}}') {
                    [System.IO.File]::WriteAllText(
                        '{{EscapePowerShellLiteral(postPath)}}',
                        "$($hookInput.ToolName)|$($invocation.SessionId)")
                }
                $null
                """))
            .AddParameter("OnSessionEnd", ScriptBlock.Create($$"""
                param($hookInput, $invocation)
                [System.IO.File]::WriteAllText(
                    '{{EscapePowerShellLiteral(endPath)}}',
                    "$($hookInput.SessionId)|$($invocation.SessionId)")
                $null
                """))
            .AddParameter(
                "OnPostToolUseFailure",
                ScriptBlock.Create("param($hookInput, $invocation) $null"))
            .AddParameter(
                "OnErrorOccurred",
                ScriptBlock.Create("param($hookInput, $invocation) $null"));
        var hookResults = ps.Invoke();
        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        var hooks = Assert.IsType<SessionHooks>(Assert.Single(hookResults).BaseObject);
        ResetPowerShell();

        ps.AddCommand("New-CopilotTool")
            .AddParameter("Name", toolName)
            .AddParameter("Description", "Returns a deterministic callback marker")
            .AddParameter("ScriptBlock", ScriptBlock.Create($"'{toolResult}'"))
            .AddParameter("SkipPermission", true);
        var toolResults = ps.Invoke();
        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        var tool = Assert.IsAssignableFrom<AIFunction>(
            Assert.Single(toolResults).BaseObject);
        ResetPowerShell();

        ps.AddCommand("New-CopilotSession")
            .AddParameter("SessionId", sessionId)
            .AddParameter("AutoApprove", true)
            .AddParameter("MaxAiCredits", 50)
            .AddParameter("Hooks", hooks)
            .AddParameter("Tool", new[] { tool });
        var sessionResults = ps.Invoke();
        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        session = Assert.IsType<CopilotSession>(Assert.Single(sessionResults).BaseObject);
        ResetPowerShell();

        await WaitForAsync(
            _ => Task.FromResult(File.Exists(startPath)),
            TimeSpan.FromSeconds(10),
            "Timed out waiting for the session-start hook.");

        ps.AddCommand("Send-CopilotMessage")
            .AddParameter(
                "Prompt",
                $"Call the {toolName} tool and reply with only the exact text it returns.")
            .AddParameter("Timeout", TimeSpan.FromSeconds(120));
        var messageResults = ps.Invoke();
        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        var message = Assert.IsType<CopilotMessageResult>(
            messageResults[^1].BaseObject);
        Assert.Contains(toolResult, message.Content);
        ResetPowerShell();

        await WaitForAsync(
            _ => Task.FromResult(File.Exists(prePath) && File.Exists(postPath)),
            TimeSpan.FromSeconds(15),
            "Timed out waiting for pre/post tool hooks.");

        Assert.Equal(
            $"{session.SessionId}|{session.SessionId}",
            await File.ReadAllTextAsync(startPath));
        Assert.Equal(
            $"{toolName}|{session.SessionId}",
            await File.ReadAllTextAsync(prePath));
        Assert.Equal(
            $"{toolName}|{session.SessionId}",
            await File.ReadAllTextAsync(postPath));

        CloseSession(assertSuccess: true);

        await WaitForAsync(
            _ => Task.FromResult(File.Exists(endPath)),
            TimeSpan.FromSeconds(10),
            "Timed out waiting for the session-end hook.");
        Assert.Equal(
            $"{sessionId}|{sessionId}",
            await File.ReadAllTextAsync(endPath));
    }

    [Fact]
    public void Hooks_RegisterNondeterministicFailureCallbacks()
    {
        ps.AddCommand("New-CopilotSessionHooks")
            .AddParameter(
                "OnPostToolUseFailure",
                ScriptBlock.Create("param($hookInput, $invocation) $null"))
            .AddParameter(
                "OnErrorOccurred",
                ScriptBlock.Create("param($hookInput, $invocation) $null"));
        var hookResults = ps.Invoke();
        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        var hooks = Assert.IsType<SessionHooks>(Assert.Single(hookResults).BaseObject);
        ResetPowerShell();

        ps.AddCommand("New-CopilotSession")
            .AddParameter("SessionId", sessionId)
            .AddParameter("AutoApprove", true)
            .AddParameter("AvailableTools", Array.Empty<string>())
            .AddParameter("MaxAiCredits", 50)
            .AddParameter("Hooks", hooks);
        var sessionResults = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        session = Assert.IsType<CopilotSession>(Assert.Single(sessionResults).BaseObject);
        Assert.NotNull(hooks.OnPostToolUseFailure);
        Assert.NotNull(hooks.OnErrorOccurred);
    }

    private void CloseSession(bool assertSuccess = false)
    {
        if (session is null)
            return;

        ResetPowerShell();
        ps.AddCommand("Close-CopilotSession").AddParameter("Session", session);
        ps.Invoke();
        if (assertSuccess)
            Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        session = null;
        ResetPowerShell();
    }

    private void ResetPowerShell()
    {
        ps.Commands.Clear();
        ps.Streams.Error.Clear();
        ps.Streams.Warning.Clear();
    }

    private static string EscapePowerShellLiteral(string value)
        => value.Replace("'", "''");

    private static async Task WaitForAsync(
        Func<CancellationToken, Task<bool>> condition,
        TimeSpan timeout,
        string timeoutMessage)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            while (!await condition(cancellation.Token))
            {
                await Task.Delay(100, cancellation.Token);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw new TimeoutException(timeoutMessage);
        }
    }
}
