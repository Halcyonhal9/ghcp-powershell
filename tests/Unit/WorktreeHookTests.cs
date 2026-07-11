using System.Diagnostics;
using Xunit;

namespace CopilotCmdlets.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class WorktreeHookTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(
        Path.GetTempPath(),
        $"copilot-worktree-hook-{Guid.NewGuid():N}");

    [Fact]
    public void WorktreeUp_CopiesIgnoredSkillBundleWithoutOverwriting()
    {
        var source = CreateRepository(
            """
            .github/skills/
            .github/prompts/
            .claude/
            """);
        WriteFile(source, ".github/skills/run-pr/SKILL.md", "source skill");
        WriteFile(source, ".github/prompts/run-pr.prompt.md", "source prompt");
        WriteFile(source, ".claude/skills/run-pr/SKILL.md", "source pointer");

        var worktree = AddWorktree(source);
        WriteFile(worktree, ".github/skills/run-pr/SKILL.md", "keep local");

        RunHook(worktree);

        Assert.Equal(
            "keep local",
            File.ReadAllText(Path.Combine(
                worktree,
                ".github",
                "skills",
                "run-pr",
                "SKILL.md")));
        Assert.Equal(
            "source prompt",
            File.ReadAllText(Path.Combine(
                worktree,
                ".github",
                "prompts",
                "run-pr.prompt.md")));
        Assert.Equal(
            "source pointer",
            File.ReadAllText(Path.Combine(
                worktree,
                ".claude",
                "skills",
                "run-pr",
                "SKILL.md")));
    }

    [Fact]
    public void WorktreeUp_RefusesPathsNotIgnoredByCommittedBranch()
    {
        var source = CreateRepository(".github/skills/\n");
        WriteFile(source, ".github/skills/run-pr/SKILL.md", "source skill");
        WriteFile(source, ".github/prompts/run-pr.prompt.md", "source prompt");

        var worktree = AddWorktree(source);

        RunHook(worktree);

        Assert.True(File.Exists(Path.Combine(
            worktree,
            ".github",
            "skills",
            "run-pr",
            "SKILL.md")));
        Assert.False(File.Exists(Path.Combine(
            worktree,
            ".github",
            "prompts",
            "run-pr.prompt.md")));
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
            Directory.Delete(tempRoot, recursive: true);
    }

    private string CreateRepository(string gitignore)
    {
        var source = Path.Combine(tempRoot, "source");
        Directory.CreateDirectory(Path.Combine(source, ".tmuxapp"));

        var hookSource = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            ".tmuxapp",
            "worktree-up"));
        File.Copy(
            hookSource,
            Path.Combine(source, ".tmuxapp", "worktree-up"));
        File.WriteAllText(Path.Combine(source, ".gitignore"), gitignore);
        File.WriteAllText(Path.Combine(source, "README.md"), "test");

        Run("git", source, "init", "-q", "-b", "main");
        Run("git", source, "config", "user.email", "test@example.com");
        Run("git", source, "config", "user.name", "Test");
        Run("git", source, "add", ".gitignore", ".tmuxapp/worktree-up", "README.md");
        Run("git", source, "commit", "-qm", "init");
        return source;
    }

    private string AddWorktree(string source)
    {
        var worktree = Path.Combine(tempRoot, $"worktree-{Guid.NewGuid():N}");
        Run("git", source, "worktree", "add", "-q", "-b", "feature", worktree);
        return worktree;
    }

    private static void RunHook(string worktree)
    {
        Run("bash", worktree, Path.Combine(worktree, ".tmuxapp", "worktree-up"), worktree);
    }

    private static void WriteFile(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static void Run(string fileName, string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start {fileName}.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(
            process.ExitCode == 0,
            $"{fileName} {string.Join(' ', arguments)} failed ({process.ExitCode}).\n{stdout}\n{stderr}");
    }
}
