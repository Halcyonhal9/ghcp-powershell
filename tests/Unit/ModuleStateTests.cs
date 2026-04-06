using System.Management.Automation;
using GitHub.Copilot.SDK;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.Unit;

[Trait("Category", "Unit")]
public class ModuleStateTests : IDisposable
{
    public ModuleStateTests()
    {
        // Reset state before each test
        ModuleState.Client = null;
        ModuleState.CurrentSession = null;
    }

    public void Dispose()
    {
        ModuleState.Client = null;
        ModuleState.CurrentSession = null;
    }

    [Fact]
    public void RequireClient_ThrowsWhenNoClientSet()
    {
        var ex = Assert.Throws<PSInvalidOperationException>(
            () => ModuleState.RequireClient(null));
        Assert.Contains("New-CopilotClient", ex.Message);
    }

    [Fact]
    public void RequireClient_ReturnsExplicitClientWhenProvided()
    {
        var explicitClient = new CopilotClient(new CopilotClientOptions { AutoStart = false });
        try
        {
            var result = ModuleState.RequireClient(explicitClient);
            Assert.Same(explicitClient, result);
        }
        finally
        {
            explicitClient.Dispose();
        }
    }

    [Fact]
    public void RequireClient_ReturnsModuleDefaultWhenExplicitIsNull()
    {
        var defaultClient = new CopilotClient(new CopilotClientOptions { AutoStart = false });
        try
        {
            ModuleState.Client = defaultClient;
            var result = ModuleState.RequireClient(null);
            Assert.Same(defaultClient, result);
        }
        finally
        {
            defaultClient.Dispose();
        }
    }

    [Fact]
    public void RequireClient_PrefersExplicitOverDefault()
    {
        var defaultClient = new CopilotClient(new CopilotClientOptions { AutoStart = false });
        var explicitClient = new CopilotClient(new CopilotClientOptions { AutoStart = false });
        try
        {
            ModuleState.Client = defaultClient;
            var result = ModuleState.RequireClient(explicitClient);
            Assert.Same(explicitClient, result);
        }
        finally
        {
            defaultClient.Dispose();
            explicitClient.Dispose();
        }
    }

    [Fact]
    public void RequireSession_ThrowsWhenNoSessionSet()
    {
        var ex = Assert.Throws<PSInvalidOperationException>(
            () => ModuleState.RequireSession(null));
        Assert.Contains("New-CopilotSession", ex.Message);
    }

    [Fact]
    public void Client_SetAndGet()
    {
        var client = new CopilotClient(new CopilotClientOptions { AutoStart = false });
        try
        {
            Assert.Null(ModuleState.Client);
            ModuleState.Client = client;
            Assert.Same(client, ModuleState.Client);
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public void Client_CanBeReplacedWithNewInstance()
    {
        var first = new CopilotClient(new CopilotClientOptions { AutoStart = false });
        var second = new CopilotClient(new CopilotClientOptions { AutoStart = false });
        try
        {
            ModuleState.Client = first;
            ModuleState.Client = second;
            Assert.Same(second, ModuleState.Client);
        }
        finally
        {
            first.Dispose();
            second.Dispose();
        }
    }

    [Fact]
    public void CurrentSession_DefaultsToNull()
    {
        Assert.Null(ModuleState.CurrentSession);
    }

    [Fact]
    public void TryRequireClient_ReturnsFalseWithErrorWhenNoClient()
    {
        var success = ModuleState.TryRequireClient(null, out var client, out var error);
        Assert.False(success);
        Assert.NotNull(error);
        Assert.Equal("NoClient", error!.FullyQualifiedErrorId);
    }

    [Fact]
    public void TryRequireClient_ReturnsTrueWithExplicitClient()
    {
        var explicitClient = new CopilotClient(new CopilotClientOptions { AutoStart = false });
        try
        {
            var success = ModuleState.TryRequireClient(explicitClient, out var client, out var error);
            Assert.True(success);
            Assert.Null(error);
            Assert.Same(explicitClient, client);
        }
        finally
        {
            explicitClient.Dispose();
        }
    }

    [Fact]
    public void TryRequireSession_ReturnsFalseWithErrorWhenNoSession()
    {
        var success = ModuleState.TryRequireSession(null, out var session, out var error);
        Assert.False(success);
        Assert.NotNull(error);
        Assert.Equal("NoSession", error!.FullyQualifiedErrorId);
    }

    [Fact]
    public async Task CleanupAsync_HandlesNullStateGracefully()
    {
        ModuleState.Client = null;
        ModuleState.CurrentSession = null;

        // Should not throw
        await ModuleState.CleanupAsync();

        Assert.Null(ModuleState.Client);
        Assert.Null(ModuleState.CurrentSession);
    }
}
