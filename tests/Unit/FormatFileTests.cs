using System.IO;
using System.Xml.Linq;
using Xunit;

namespace CopilotCmdlets.Tests.Unit;

[Trait("Category", "Unit")]
public class FormatFileTests
{
    private static readonly string FormatFilePath = FindFormatFile();
    private static readonly Lazy<XDocument> FormatDoc = new(() => XDocument.Load(FormatFilePath));

    private static string FindFormatFile()
    {
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
        var doc = FormatDoc.Value;
        Assert.NotNull(doc.Root);
        Assert.Equal("Configuration", doc.Root!.Name.LocalName);
    }

    [Fact]
    public void FormatFile_ContainsModelInfoTableView()
    {
        var views = FormatDoc.Value.Descendants("View");
        Assert.Contains(views, v =>
            v.Element("Name")?.Value == "ModelInfo_Table" &&
            v.Descendants("TypeName").Any(t => t.Value == "GitHub.Copilot.SDK.ModelInfo"));
    }

    [Fact]
    public void FormatFile_ContainsModelInfoListView()
    {
        var views = FormatDoc.Value.Descendants("View");
        Assert.Contains(views, v =>
            v.Element("Name")?.Value == "ModelInfo_List" &&
            v.Descendants("TypeName").Any(t => t.Value == "GitHub.Copilot.SDK.ModelInfo"));
    }

    [Fact]
    public void FormatFile_ContainsSessionMetadataTableView()
    {
        var views = FormatDoc.Value.Descendants("View");
        Assert.Contains(views, v =>
            v.Element("Name")?.Value == "SessionMetadata_Table" &&
            v.Descendants("TypeName").Any(t => t.Value == "GitHub.Copilot.SDK.SessionMetadata"));
    }

    [Fact]
    public void FormatFile_ContainsSessionMetadataListView()
    {
        var views = FormatDoc.Value.Descendants("View");
        Assert.Contains(views, v =>
            v.Element("Name")?.Value == "SessionMetadata_List" &&
            v.Descendants("TypeName").Any(t => t.Value == "GitHub.Copilot.SDK.SessionMetadata"));
    }

    [Fact]
    public void FormatFile_ModelInfoTableFlattensCapabilities()
    {
        var modelTable = FormatDoc.Value.Descendants("View")
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
        var sessionTable = FormatDoc.Value.Descendants("View")
            .First(v => v.Element("Name")?.Value == "SessionMetadata_Table");

        var scriptBlocks = sessionTable.Descendants("ScriptBlock").Select(s => s.Value).ToList();
        Assert.Contains(scriptBlocks, s => s.Contains("Context.Repository"));
        Assert.Contains(scriptBlocks, s => s.Contains("Context.Branch"));
    }

    [Fact]
    public void ModuleManifest_ReferencesFormatFile()
    {
        var dir = Path.GetDirectoryName(FormatFilePath)!;
        var psd1Path = Path.Combine(dir, "CopilotCmdlets.psd1");
        Assert.True(File.Exists(psd1Path), $"Module manifest not found at {psd1Path}");

        var content = File.ReadAllText(psd1Path);
        Assert.Contains("FormatsToProcess", content);
        Assert.Contains("CopilotCmdlets.format.ps1xml", content);
    }
}
