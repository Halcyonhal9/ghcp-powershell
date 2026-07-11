using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using GitHub.Copilot;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.Unit;

/// <summary>
/// Verifies that config-building failures surface as terminating errors with
/// the module's error IDs instead of raw uncaught exceptions.
/// </summary>
[Trait("Category", "Unit")]
[Collection("ModuleState")]
public class ConfigErrorHandlingTests : IDisposable
{
    public ConfigErrorHandlingTests()
    {
        ModuleState.Client = null;
        ModuleState.CurrentSession = null;
    }

    public void Dispose()
    {
        ModuleState.Client = null;
        ModuleState.CurrentSession = null;
    }

    private static PowerShell CreateShell(params (string Name, Type Type)[] cmdlets)
    {
        var state = InitialSessionState.CreateDefault2();
        foreach (var (name, type) in cmdlets)
        {
            state.Commands.Add(new SessionStateCmdletEntry(name, type, null));
        }
        return PowerShell.Create(state);
    }

    private static ErrorRecord InvokeExpectingTerminatingError(PowerShell ps)
    {
        var ex = Assert.ThrowsAny<RuntimeException>(() => ps.Invoke());
        return ex.ErrorRecord;
    }

    [Fact]
    public void NewCopilotClient_InvalidOptionCombination_ThrowsClientOptionsInvalid()
    {
        using var ps = CreateShell(("New-CopilotClient", typeof(NewCopilotClientCmdlet)));
        ps.AddCommand("New-CopilotClient")
            .AddParameter("CliUrl", "localhost:9999")
            .AddParameter("GitHubToken", "placeholder");

        var error = InvokeExpectingTerminatingError(ps);

        Assert.StartsWith("ClientOptionsInvalid", error.FullyQualifiedErrorId);
        Assert.Equal(ErrorCategory.InvalidArgument, error.CategoryInfo.Category);
    }

    [Fact]
    public void NewCopilotSession_MalformedMcpServers_ThrowsInvalidSessionConfig()
    {
        var client = new CopilotClient(new CopilotClientOptions());
        try
        {
            ModuleState.Client = client;

            using var ps = CreateShell(("New-CopilotSession", typeof(NewCopilotSessionCmdlet)));
            ps.AddCommand("New-CopilotSession")
                .AddParameter("McpServers", new Hashtable
                {
                    ["bad"] = new Hashtable { ["Command"] = "cmd", ["Url"] = "https://x" }
                });

            var error = InvokeExpectingTerminatingError(ps);

            Assert.StartsWith("InvalidSessionConfig", error.FullyQualifiedErrorId);
            Assert.Equal(ErrorCategory.InvalidArgument, error.CategoryInfo.Category);
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public void ResumeCopilotSession_InvalidSystemMessageMode_ThrowsInvalidSessionConfig()
    {
        var client = new CopilotClient(new CopilotClientOptions());
        try
        {
            ModuleState.Client = client;

            using var ps = CreateShell(("Resume-CopilotSession", typeof(ResumeCopilotSessionCmdlet)));
            ps.AddCommand("Resume-CopilotSession")
                .AddParameter("SessionId", "some-session")
                .AddParameter("SystemMessageMode", "NotARealMode");

            var error = InvokeExpectingTerminatingError(ps);

            Assert.StartsWith("InvalidSessionConfig", error.FullyQualifiedErrorId);
            Assert.Equal(ErrorCategory.InvalidArgument, error.CategoryInfo.Category);
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public void McpServerHelper_UnwrapReturnsPSObjectBaseObject()
    {
        Assert.Equal("inner", McpServerHelper.Unwrap(new PSObject("inner")));
        Assert.Equal(42, McpServerHelper.Unwrap(42));
        Assert.Null(McpServerHelper.Unwrap(null));
    }
}
