using System.Management.Automation;
using System.Management.Automation.Runspaces;
using GitHub.Copilot;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.Unit;

/// <summary>
/// Loads CopilotCmdlets.format.ps1xml into a live runspace and formats real
/// SDK objects through it, catching property-path drift that static XML
/// checks cannot (e.g. SDK renames like Context.Cwd → Context.WorkingDirectory).
/// </summary>
[Trait("Category", "Unit")]
public class FormatRuntimeBindingTests
{
    private static string FormatObject(object target, string formatCommand)
    {
        var formatFile = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "CopilotCmdlets.format.ps1xml"));
        Assert.True(File.Exists(formatFile), $"format file not found at {formatFile}");

        var state = InitialSessionState.CreateDefault2();
        state.Formats.Add(new SessionStateFormatEntry(formatFile));
        using var ps = PowerShell.Create(state);

        ps.AddCommand(formatCommand).AddParameter("InputObject", target)
          .AddCommand("Out-String").AddParameter("Width", 200);
        var output = string.Join("", ps.Invoke().Select(o => o.ToString()));

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error.ReadAll().Select(e => e.ToString())));
        return output;
    }

    [Fact]
    public void SessionMetadata_ListViewBindsAllProperties()
    {
        var metadata = new SessionMetadata
        {
            SessionId = "session-abc",
            StartTime = DateTimeOffset.Parse("2026-01-02T03:04:05Z"),
            ModifiedTime = DateTimeOffset.Parse("2026-01-02T04:05:06Z"),
            Summary = "test summary",
            IsRemote = false,
            Context = new SessionContext
            {
                WorkingDirectory = "/work/dir",
                GitRoot = "/work/dir",
                Repository = "owner/repo",
                Branch = "feature-branch"
            }
        };

        var output = FormatObject(metadata, "Format-List");

        Assert.Contains("session-abc", output);
        Assert.Contains("test summary", output);
        Assert.Contains("/work/dir", output);
        Assert.Contains("owner/repo", output);
        Assert.Contains("feature-branch", output);
    }

    [Fact]
    public void ModelInfo_TableViewBindsAllColumns()
    {
        var model = new ModelInfo
        {
            Id = "model-id-1",
            Name = "Test Model",
            Capabilities = new ModelCapabilities
            {
                Supports = new ModelSupports { Vision = true, ReasoningEffort = true },
                Limits = new ModelLimits { MaxContextWindowTokens = 128000 }
            },
            Billing = new ModelBilling { Multiplier = 1.5f },
            Policy = new ModelPolicy { State = "enabled", Terms = "terms" }
        };

        var output = FormatObject(model, "Format-Table");

        Assert.Contains("Test Model", output);
        Assert.Contains("model-id-1", output);
        Assert.Contains("enabled", output);
    }

    [Fact]
    public void CopilotMessageResult_ListViewBindsComputedProperties()
    {
        var result = new CopilotMessageResult
        {
            MessageId = "msg-1",
            SessionId = "sess-1",
            Content = "hello world",
            UsageEvents =
            {
                new AssistantUsageData { Model = "m", InputTokens = 10, OutputTokens = 5 }
            },
            ContextWindow = new SessionUsageInfoData
            {
                CurrentTokens = 1234,
                TokenLimit = 200000,
                MessagesLength = 2
            }
        };

        var output = FormatObject(result, "Format-List");

        Assert.Contains("msg-1", output);
        Assert.Contains("hello world", output);
        Assert.Contains("1234", output);
        Assert.Contains("200000", output);
        Assert.Contains("10", output);
    }
}
