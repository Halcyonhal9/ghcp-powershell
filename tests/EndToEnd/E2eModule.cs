namespace CopilotCmdlets.Tests.EndToEnd;

/// <summary>
/// Resolves the published module under out/ and fails fast with an actionable
/// message when it is missing or older than the sources — otherwise every
/// end-to-end test fails with an opaque import error.
/// </summary>
internal static class E2eModule
{
    internal static string ResolveManifest()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var manifest = Path.Combine(repoRoot, "out", "CopilotCmdlets.psd1");
        var assembly = Path.Combine(repoRoot, "out", "CopilotCmdlets.dll");

        const string publishHint =
            "Publish the module first: dotnet publish src/CopilotCmdlets.csproj -c Release -o out";

        if (!File.Exists(manifest) || !File.Exists(assembly))
        {
            throw new InvalidOperationException(
                $"Published module not found under {Path.Combine(repoRoot, "out")}. {publishHint}");
        }

        var publishedAt = File.GetLastWriteTimeUtc(assembly);
        var newerSource = Directory.EnumerateFiles(Path.Combine(repoRoot, "src"), "*.cs")
            .Concat([Path.Combine(repoRoot, "CopilotCmdlets.psd1"), Path.Combine(repoRoot, "CopilotCmdlets.format.ps1xml")])
            .FirstOrDefault(f => File.GetLastWriteTimeUtc(f) > publishedAt);
        if (newerSource is not null)
        {
            throw new InvalidOperationException(
                $"Published module is stale: {Path.GetFileName(newerSource)} is newer than out/CopilotCmdlets.dll. {publishHint}");
        }

        return manifest;
    }
}
