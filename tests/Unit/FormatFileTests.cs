using System.IO;
using System.Xml.Linq;
using Xunit;

namespace CopilotCmdlets.Tests.Unit;

[Trait("Category", "Unit")]
public class FormatFileTests
{
    private static readonly string FormatFilePath = FindFormatFile();

    private static string FindFormatFile()
    {
        // Walk up from the test assembly directory to find the repo root.
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "CopilotCmdlets.format.ps1xml");
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException("CopilotCmdlets.format.ps1xml not found relative to test assembly.");
    }

    [Fact]
    public void FormatFile_Exists()
    {
        Assert.True(File.Exists(FormatFilePath), $"Format file not found at {FormatFilePath}");
    }

    [Fact]
    public void FormatFile_IsValidXml()
    {
        var doc = XDocument.Load(FormatFilePath);
        Assert.NotNull(doc.Root);
        Assert.Equal("Configuration", doc.Root!.Name.LocalName);
    }

    [Fact]
    public void FormatFile_ContainsModelInfoTableView()
    {
        var doc = XDocument.Load(FormatFilePath);
        var views = doc.Descendants("View");
        Assert.Contains(views, v =>
            v.Element("Name")?.Value == "ModelInfo_Table" &&
            v.Descendants("TypeName").Any(t => t.Value == "GitHub.Copilot.SDK.ModelInfo"));
    }

    [Fact]
    public void FormatFile_ContainsModelInfoListView()
    {
        var doc = XDocument.Load(FormatFilePath);
        var views = doc.Descendants("View");
        Assert.Contains(views, v =>
            v.Element("Name")?.Value == "ModelInfo_List" &&
            v.Descendants("TypeName").Any(t => t.Value == "GitHub.Copilot.SDK.ModelInfo"));
    }

    [Fact]
    public void FormatFile_ContainsSessionMetadataTableView()
    {
        var doc = XDocument.Load(FormatFilePath);
        var views = doc.Descendants("View");
        Assert.Contains(views, v =>
            v.Element("Name")?.Value == "SessionMetadata_Table" &&
            v.Descendants("TypeName").Any(t => t.Value == "GitHub.Copilot.SDK.SessionMetadata"));
    }

    [Fact]
    public void FormatFile_ContainsSessionMetadataListView()
    {
        var doc = XDocument.Load(FormatFilePath);
        var views = doc.Descendants("View");
        Assert.Contains(views, v =>
            v.Element("Name")?.Value == "SessionMetadata_List" &&
            v.Descendants("TypeName").Any(t => t.Value == "GitHub.Copilot.SDK.SessionMetadata"));
    }

    [Fact]
    public void FormatFile_ModelInfoTableFlattensCapabilities()
    {
        var doc = XDocument.Load(FormatFilePath);
        var modelTable = doc.Descendants("View")
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
        var doc = XDocument.Load(FormatFilePath);
        var sessionTable = doc.Descendants("View")
            .First(v => v.Element("Name")?.Value == "SessionMetadata_Table");

        var scriptBlocks = sessionTable.Descendants("ScriptBlock").Select(s => s.Value).ToList();
        Assert.Contains(scriptBlocks, s => s.Contains("Context.Repository"));
        Assert.Contains(scriptBlocks, s => s.Contains("Context.Branch"));
    }

    [Fact]
    public void ModuleManifest_ReferencesFormatFile()
    {
        // Find the psd1 near the format file
        var dir = Path.GetDirectoryName(FormatFilePath)!;
        var psd1Path = Path.Combine(dir, "CopilotCmdlets.psd1");
        Assert.True(File.Exists(psd1Path), $"Module manifest not found at {psd1Path}");

        var content = File.ReadAllText(psd1Path);
        Assert.Contains("FormatsToProcess", content);
        Assert.Contains("CopilotCmdlets.format.ps1xml", content);
    }
}
