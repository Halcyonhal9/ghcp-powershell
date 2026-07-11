using System.Collections;
using GitHub.Copilot;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.Unit;

[Trait("Category", "Unit")]
public class McpServerHelperTests
{
    [Fact]
    public void Build_StdioServerFromCommand()
    {
        var servers = new Hashtable
        {
            ["everything"] = new Hashtable
            {
                ["Command"] = "npx",
                ["Args"] = new[] { "-y", "@modelcontextprotocol/server-everything" },
                ["Env"] = new Hashtable { ["FOO"] = "bar" },
                ["WorkingDirectory"] = "/tmp",
                ["Tools"] = "*",
                ["Timeout"] = 5000
            }
        };

        var result = McpServerHelper.Build(servers);

        var config = Assert.IsType<McpStdioServerConfig>(result["everything"]);
        Assert.Equal("npx", config.Command);
        Assert.Equal(["-y", "@modelcontextprotocol/server-everything"], config.Args);
        Assert.Equal("bar", config.Env!["FOO"]);
        Assert.Equal("/tmp", config.WorkingDirectory);
        Assert.Equal(["*"], config.Tools);
        Assert.Equal(5000, config.Timeout);
    }

    [Fact]
    public void Build_HttpServerFromUrl()
    {
        var servers = new Hashtable
        {
            ["remote"] = new Hashtable
            {
                ["Url"] = "https://mcp.example.com/sse",
                ["Headers"] = new Hashtable { ["Authorization"] = "Bearer token" }
            }
        };

        var result = McpServerHelper.Build(servers);

        var config = Assert.IsType<McpHttpServerConfig>(result["remote"]);
        Assert.Equal("https://mcp.example.com/sse", config.Url);
        Assert.Equal("Bearer token", config.Headers!["Authorization"]);
        Assert.Null(config.Tools);
    }

    [Fact]
    public void Build_MultipleServers()
    {
        var servers = new Hashtable
        {
            ["a"] = new Hashtable { ["Command"] = "cmd-a" },
            ["b"] = new Hashtable { ["Url"] = "https://b.example.com" }
        };

        var result = McpServerHelper.Build(servers);

        Assert.Equal(2, result.Count);
        Assert.IsType<McpStdioServerConfig>(result["a"]);
        Assert.IsType<McpHttpServerConfig>(result["b"]);
    }

    [Fact]
    public void Build_ThrowsWhenBothCommandAndUrl()
    {
        var servers = new Hashtable
        {
            ["bad"] = new Hashtable { ["Command"] = "cmd", ["Url"] = "https://x" }
        };

        var ex = Assert.Throws<ArgumentException>(() => McpServerHelper.Build(servers));
        Assert.Contains("exactly one of", ex.Message);
    }

    [Fact]
    public void Build_ThrowsWhenNeitherCommandNorUrl()
    {
        var servers = new Hashtable { ["bad"] = new Hashtable() };

        var ex = Assert.Throws<ArgumentException>(() => McpServerHelper.Build(servers));
        Assert.Contains("exactly one of", ex.Message);
    }

    [Fact]
    public void Build_ThrowsWhenValueNotHashtable()
    {
        var servers = new Hashtable { ["bad"] = "not a hashtable" };

        var ex = Assert.Throws<ArgumentException>(() => McpServerHelper.Build(servers));
        Assert.Contains("must be a hashtable", ex.Message);
    }

    [Fact]
    public void Build_ThrowsArgumentExceptionForNonNumericTimeout()
    {
        var servers = new Hashtable
        {
            ["s"] = new Hashtable { ["Command"] = "cmd", ["Timeout"] = "not-a-number" }
        };

        var ex = Assert.Throws<ArgumentException>(() => McpServerHelper.Build(servers));
        Assert.Contains("Timeout", ex.Message);
        Assert.Contains("'s'", ex.Message);
        Assert.IsType<FormatException>(ex.InnerException);
    }

    [Fact]
    public void Build_ThrowsArgumentExceptionForUnconvertibleTimeout()
    {
        var servers = new Hashtable
        {
            ["s"] = new Hashtable { ["Command"] = "cmd", ["Timeout"] = new Hashtable() }
        };

        var ex = Assert.Throws<ArgumentException>(() => McpServerHelper.Build(servers));
        Assert.Contains("Timeout", ex.Message);
        Assert.IsType<InvalidCastException>(ex.InnerException);
    }

    [Fact]
    public void Build_AcceptsNumericStringTimeout()
    {
        var servers = new Hashtable
        {
            ["s"] = new Hashtable { ["Command"] = "cmd", ["Timeout"] = "2500" }
        };

        var result = McpServerHelper.Build(servers);

        Assert.Equal(2500, result["s"].Timeout);
    }

    [Fact]
    public void Build_SingleArgStringBecomesList()
    {
        var servers = new Hashtable
        {
            ["s"] = new Hashtable { ["Command"] = "cmd", ["Args"] = "--only-arg" }
        };

        var result = McpServerHelper.Build(servers);

        var config = Assert.IsType<McpStdioServerConfig>(result["s"]);
        Assert.Equal(["--only-arg"], config.Args);
    }
}
