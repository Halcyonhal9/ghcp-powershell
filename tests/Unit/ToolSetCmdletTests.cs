#pragma warning disable GHCP001 // experimental SDK members: ToolSet and BuiltInTools
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using GitHub.Copilot;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.Unit;

[Trait("Category", "Unit")]
public class ToolSetCmdletTests
{
    [Fact]
    public void NewCopilotToolSet_HasExpectedMetadataAndParameters()
    {
        var cmdlet = Assert.IsType<CmdletAttribute>(Attribute.GetCustomAttribute(
            typeof(NewCopilotToolSetCmdlet), typeof(CmdletAttribute)));
        Assert.Equal(VerbsCommon.New, cmdlet.VerbName);
        Assert.Equal("CopilotToolSet", cmdlet.NounName);

        var output = Assert.Single(Attribute.GetCustomAttributes(
            typeof(NewCopilotToolSetCmdlet), typeof(OutputTypeAttribute))
            .Cast<OutputTypeAttribute>());
        Assert.Contains(output.Type, type => type.Type == typeof(ToolSet));

        foreach (var name in new[] { "BuiltIn", "Custom", "Mcp" })
        {
            var property = typeof(NewCopilotToolSetCmdlet).GetProperty(name)!;
            Assert.Equal(typeof(string[]), property.PropertyType);
            Assert.NotNull(Attribute.GetCustomAttribute(
                property, typeof(ParameterAttribute)));
            Assert.Null(Attribute.GetCustomAttribute(
                property, typeof(ValidatePatternAttribute)));
        }

        var isolated = typeof(NewCopilotToolSetCmdlet).GetProperty("Isolated")!;
        Assert.Equal(typeof(SwitchParameter), isolated.PropertyType);
        Assert.NotNull(Attribute.GetCustomAttribute(
            isolated, typeof(ParameterAttribute)));
    }

    [Fact]
    public void BuildToolSet_PreservesSdkQualificationOrderAndWildcards()
    {
        var result = new NewCopilotToolSetCmdlet
        {
            BuiltIn = ["bash", "*"],
            Custom = ["my_tool", "*"],
            Mcp = ["github-list_issues", "*"]
        }.BuildToolSet();

        Assert.Equal(
        [
            "builtin:bash",
            "builtin:*",
            "custom:my_tool",
            "custom:*",
            "mcp:github-list_issues",
            "mcp:*",
        ], result);
    }

    [Fact]
    public void BuildToolSet_IsolatedUsesSdkCuratedSet()
    {
        var result = new NewCopilotToolSetCmdlet { Isolated = true }
            .BuildToolSet();

        Assert.Equal(
            BuiltInTools.Isolated.Select(name => $"builtin:{name}"),
            result);
    }

    [Theory]
    [InlineData("BuiltIn", "has:colon", "builtin")]
    [InlineData("Custom", "has space", "custom")]
    [InlineData("Mcp", "", "mcp")]
    public void NewCopilotToolSet_InvalidNamesSurfaceSdkValidation(
        string parameter,
        string invalidName,
        string kind)
    {
        using var ps = CreateShell();
        ps.AddCommand("New-CopilotToolSet")
            .AddParameter(parameter, new[] { invalidName });

        var exception = Assert.ThrowsAny<RuntimeException>(() => ps.Invoke());
        var error = exception.ErrorRecord;

        Assert.StartsWith("ToolSetCreateFailed", error.FullyQualifiedErrorId);
        Assert.Equal(ErrorCategory.InvalidArgument, error.CategoryInfo.Category);
        Assert.Contains($"Invalid {kind} tool name", error.Exception.Message);
    }

    [Fact]
    public void NewCopilotToolSet_WritesOneRawNonEnumeratedToolSet()
    {
        using var ps = CreateShell();
        ps.AddCommand("New-CopilotToolSet")
            .AddParameter("BuiltIn", new[] { "bash", "*" })
            .AddParameter("Custom", new[] { "my_tool" })
            .AddParameter("Mcp", new[] { "github-list_issues" });

        var output = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        var toolSet = Assert.IsType<ToolSet>(Assert.Single(output).BaseObject);
        Assert.Equal(
        [
            "builtin:bash",
            "builtin:*",
            "custom:my_tool",
            "mcp:github-list_issues",
        ], toolSet);
    }

    [Fact]
    public void NewCopilotToolSet_EmptySetStillWritesOneRawObject()
    {
        using var ps = CreateShell();
        ps.AddCommand("New-CopilotToolSet");

        var output = ps.Invoke();

        Assert.False(ps.HadErrors, string.Join("; ", ps.Streams.Error));
        var toolSet = Assert.IsType<ToolSet>(Assert.Single(output).BaseObject);
        Assert.Empty(toolSet);
    }

    private static PowerShell CreateShell()
    {
        var state = InitialSessionState.CreateDefault2();
        state.Commands.Add(new SessionStateCmdletEntry(
            "New-CopilotToolSet", typeof(NewCopilotToolSetCmdlet), null));
        return PowerShell.Create(state);
    }
}
