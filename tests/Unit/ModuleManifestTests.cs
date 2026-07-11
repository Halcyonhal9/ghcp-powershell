using System.Collections;
using System.Management.Automation;
using Xunit;

namespace CopilotCmdlets.Tests.Unit;

[Trait("Category", "Unit")]
public class ModuleManifestTests
{
    [Fact]
    public void ManifestExportsEveryPublicCmdletExactlyOnce()
    {
        var manifestPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "CopilotCmdlets.psd1"));
        Assert.True(File.Exists(manifestPath), $"Manifest not found at {manifestPath}");

        using var ps = PowerShell.Create();
        ps.AddCommand("Import-PowerShellDataFile")
            .AddParameter("Path", manifestPath);
        var output = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        var manifest = Assert.IsType<Hashtable>(Assert.Single(output).BaseObject);
        var exported = Assert.IsAssignableFrom<IEnumerable>(manifest["CmdletsToExport"])
            .Cast<object>()
            .Select(item => item.ToString()!)
            .ToList();

        var expected = typeof(NewCopilotClientCmdlet).Assembly
            .GetTypes()
            .Where(type =>
                type.IsPublic &&
                !type.IsAbstract &&
                typeof(PSCmdlet).IsAssignableFrom(type))
            .Select(type => (
                Type: type,
                Attribute: Attribute.GetCustomAttribute(type, typeof(CmdletAttribute))
                    as CmdletAttribute))
            .Where(item => item.Attribute is not null)
            .Select(item => $"{item.Attribute!.VerbName}-{item.Attribute.NounName}")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(exported.Count, exported.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(expected, exported.OrderBy(name => name, StringComparer.Ordinal));
    }
}
