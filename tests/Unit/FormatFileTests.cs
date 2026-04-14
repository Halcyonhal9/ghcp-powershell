using System.IO;
using System.Xml.Linq;
using Xunit;

namespace CopilotCmdlets.Tests.Unit;

[Trait("Category", "Unit")]
public class FormatFileTests
{
    private static string? TryFindFormatFile()
    {
        var dir = AppContext.BaseDirectory;
        for (var depth = 0; depth < 5 && dir is not null; depth++)
        {
            var candidate = Path.Combine(dir, "CopilotCmdlets.format.ps1xml");
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static string RequireFormatFile()
    {
        var path = TryFindFormatFile();
        Assert.NotNull(path);
        return path!;
    }

    private static XDocument LoadFormatDoc()
    {
        return XDocument.Load(RequireFormatFile());
    }

    [Fact]
    public void FormatFile_Exists()
    {
        var path = TryFindFormatFile();
        Assert.True(path is not null, "CopilotCmdlets.format.ps1xml not found relative to test assembly.");
    }

    [Fact]
    public void FormatFile_IsValidXml()
    {
        var doc = LoadFormatDoc();
        Assert.NotNull(doc.Root);
        Assert.Equal("Configuration", doc.Root!.Name.LocalName);
    }

    [Theory]
    [InlineData("ModelInfo_Table", "GitHub.Copilot.SDK.ModelInfo")]
    [InlineData("ModelInfo_List", "GitHub.Copilot.SDK.ModelInfo")]
    [InlineData("SessionMetadata_Table", "GitHub.Copilot.SDK.SessionMetadata")]
    [InlineData("SessionMetadata_List", "GitHub.Copilot.SDK.SessionMetadata")]
    public void FormatFile_ContainsExpectedView(string viewName, string typeName)
    {
        var views = LoadFormatDoc().Descendants("View");
        Assert.Contains(views, v =>
            v.Element("Name")?.Value == viewName &&
            v.Descendants("TypeName").Any(t => t.Value == typeName));
    }

    [Fact]
    public void FormatFile_ModelInfoTableFlattensCapabilities()
    {
        var modelTable = LoadFormatDoc().Descendants("View")
            .First(v => v.Element("Name")?.Value == "ModelInfo_Table");

        var scriptBlocks = modelTable.Descendants("ScriptBlock").Select(s => s.Value).ToList();
        Assert.Contains(scriptBlocks, s => s.Contains("Capabilities.Supports.Vision"));
        Assert.Contains(scriptBlocks, s => s.Contains("Capabilities.Supports.ReasoningEffort"));
        Assert.Contains(scriptBlocks, s => s.Contains("Billing.Multiplier"));
        Assert.Contains(scriptBlocks, s => s.Contains("Policy.State"));
    }

    [Fact]
    public void FormatFile_SessionMetadataTableFlattensContext()
    {
        var sessionTable = LoadFormatDoc().Descendants("View")
            .First(v => v.Element("Name")?.Value == "SessionMetadata_Table");

        var scriptBlocks = sessionTable.Descendants("ScriptBlock").Select(s => s.Value).ToList();
        Assert.Contains(scriptBlocks, s => s.Contains("Context.Repository"));
        Assert.Contains(scriptBlocks, s => s.Contains("Context.Branch"));
    }

    [Fact]
    public void ModuleManifest_ReferencesFormatFile()
    {
        var formatPath = RequireFormatFile();
        var dir = Path.GetDirectoryName(formatPath)!;
        var psd1Path = Path.Combine(dir, "CopilotCmdlets.psd1");
        Assert.True(File.Exists(psd1Path), $"Module manifest not found at {psd1Path}");

        var content = File.ReadAllText(psd1Path);
        Assert.Contains("FormatsToProcess", content);
        Assert.Contains("CopilotCmdlets.format.ps1xml", content);
    }
}
