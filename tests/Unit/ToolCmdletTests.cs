using System.Collections;
using System.Management.Automation;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.Unit;

[Trait("Category", "Unit")]
public class ToolCmdletTests
{
    [Fact]
    public void NewCopilotTool_HasCorrectCmdletAttribute()
    {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(
            typeof(NewCopilotToolCmdlet), typeof(CmdletAttribute))!;
        Assert.Equal(VerbsCommon.New, attr.VerbName);
        Assert.Equal("CopilotTool", attr.NounName);
    }

    [Fact]
    public void NewCopilotTool_HasOutputType()
    {
        var attrs = Attribute.GetCustomAttributes(
            typeof(NewCopilotToolCmdlet), typeof(OutputTypeAttribute));
        Assert.Single(attrs);
        var outputType = (OutputTypeAttribute)attrs[0];
        Assert.Contains(outputType.Type, t => t.Type == typeof(AIFunction));
    }

    [Theory]
    [InlineData("Name", 0)]
    [InlineData("Description", 1)]
    [InlineData("ScriptBlock", 2)]
    public void NewCopilotTool_HasMandatoryPositionalParameters(string name, int position)
    {
        var prop = typeof(NewCopilotToolCmdlet).GetProperty(name)!;
        var paramAttr = (ParameterAttribute)Attribute.GetCustomAttribute(prop, typeof(ParameterAttribute))!;
        Assert.True(paramAttr.Mandatory);
        Assert.Equal(position, paramAttr.Position);
    }

    [Fact]
    public void ScriptBlockToolFunction_ExposesNameAndDescription()
    {
        var tool = new ScriptBlockToolFunction("my_tool", "Does things", ScriptBlock.Create("param($x) $x"));

        Assert.Equal("my_tool", tool.Name);
        Assert.Equal("Does things", tool.Description);
    }

    [Fact]
    public void ScriptBlockToolFunction_SkipPermissionSetsAdditionalProperty()
    {
        var tool = new ScriptBlockToolFunction("t", "d", ScriptBlock.Create("1"), skipPermission: true);

        Assert.True(tool.AdditionalProperties.TryGetValue("skip_permission", out var value));
        Assert.Equal(true, value);
    }

    [Fact]
    public void ScriptBlockToolFunction_NoSkipPermissionByDefault()
    {
        var tool = new ScriptBlockToolFunction("t", "d", ScriptBlock.Create("1"));

        Assert.False(tool.AdditionalProperties.ContainsKey("skip_permission"));
    }

    [Fact]
    public void BuildSchema_EmptyForNoParamBlock()
    {
        var schema = ScriptBlockToolFunction.BuildSchema(ScriptBlock.Create("'hello'"));

        Assert.Equal(JsonValueKind.Object, schema.ValueKind);
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.Empty(schema.GetProperty("properties").EnumerateObject());
        Assert.False(schema.TryGetProperty("required", out _));
    }

    [Fact]
    public void BuildSchema_MapsParameterTypes()
    {
        var schema = ScriptBlockToolFunction.BuildSchema(ScriptBlock.Create(
            "param([string]$Name, [int]$Count, [double]$Ratio, [bool]$Flag, [switch]$Toggle, [string[]]$Items, [hashtable]$Extra, $Untyped) $Name"));

        var properties = schema.GetProperty("properties");
        Assert.Equal("string", properties.GetProperty("Name").GetProperty("type").GetString());
        Assert.Equal("integer", properties.GetProperty("Count").GetProperty("type").GetString());
        Assert.Equal("number", properties.GetProperty("Ratio").GetProperty("type").GetString());
        Assert.Equal("boolean", properties.GetProperty("Flag").GetProperty("type").GetString());
        Assert.Equal("boolean", properties.GetProperty("Toggle").GetProperty("type").GetString());
        Assert.Equal("array", properties.GetProperty("Items").GetProperty("type").GetString());
        Assert.Equal("string", properties.GetProperty("Items").GetProperty("items").GetProperty("type").GetString());
        Assert.Equal("object", properties.GetProperty("Extra").GetProperty("type").GetString());
        Assert.Equal("string", properties.GetProperty("Untyped").GetProperty("type").GetString());
    }

    [Fact]
    public void BuildSchema_MarksMandatoryParametersRequired()
    {
        var schema = ScriptBlockToolFunction.BuildSchema(ScriptBlock.Create(
            "param([Parameter(Mandatory)][string]$City, [Parameter(Mandatory = $true)][int]$Days, [string]$Units) $City"));

        var required = schema.GetProperty("required").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        Assert.Equal(["City", "Days"], required);
    }

    [Fact]
    public void BuildSchema_MandatoryFalseIsOptional()
    {
        var schema = ScriptBlockToolFunction.BuildSchema(ScriptBlock.Create(
            "param([Parameter(Mandatory = $false)][string]$Opt) $Opt"));

        Assert.False(schema.TryGetProperty("required", out _));
    }

    [Fact]
    public void BuildSchema_UsesHelpMessageAsDescription()
    {
        var schema = ScriptBlockToolFunction.BuildSchema(ScriptBlock.Create(
            "param([Parameter(HelpMessage = 'The city name')][string]$City) $City"));

        Assert.Equal("The city name",
            schema.GetProperty("properties").GetProperty("City").GetProperty("description").GetString());
    }

    [Fact]
    public void ConvertArgument_ConvertsJsonPrimitives()
    {
        Assert.Equal("text", ScriptBlockToolFunction.ConvertArgument(Parse("\"text\"")));
        Assert.Equal(42L, ScriptBlockToolFunction.ConvertArgument(Parse("42")));
        Assert.Equal(2.5, ScriptBlockToolFunction.ConvertArgument(Parse("2.5")));
        Assert.Equal(true, ScriptBlockToolFunction.ConvertArgument(Parse("true")));
        Assert.Equal(false, ScriptBlockToolFunction.ConvertArgument(Parse("false")));
        Assert.Null(ScriptBlockToolFunction.ConvertArgument(Parse("null")));
    }

    [Fact]
    public void ConvertArgument_ConvertsArraysAndObjects()
    {
        var array = Assert.IsType<object?[]>(ScriptBlockToolFunction.ConvertArgument(Parse("[1, \"two\"]")));
        Assert.Equal([1L, "two"], array);

        var table = Assert.IsType<Hashtable>(ScriptBlockToolFunction.ConvertArgument(Parse("{\"a\": 1, \"b\": {\"c\": true}}")));
        Assert.Equal(1L, table["a"]);
        var nested = Assert.IsType<Hashtable>(table["b"]);
        Assert.Equal(true, nested["c"]);
    }

    [Fact]
    public void ConvertArgument_PassesThroughNonJsonValues()
    {
        Assert.Equal("plain", ScriptBlockToolFunction.ConvertArgument("plain"));
        Assert.Null(ScriptBlockToolFunction.ConvertArgument(null));
    }

    [Fact]
    public async Task InvokeAsync_RunsScriptBlockWithArguments()
    {
        var tool = new ScriptBlockToolFunction("greet", "Greets",
            ScriptBlock.Create("param([string]$Name) \"Hello, $Name!\""));

        var arguments = new AIFunctionArguments { ["Name"] = Parse("\"World\"") };
        var result = await tool.InvokeAsync(arguments);

        Assert.Equal("Hello, World!", result?.ToString());
    }

    [Fact]
    public async Task InvokeAsync_OmittedParametersUseDefaults()
    {
        var tool = new ScriptBlockToolFunction("count", "Counts",
            ScriptBlock.Create("param([string]$City, [int]$Days = 3) \"$City for $Days days\""));

        var arguments = new AIFunctionArguments { ["City"] = Parse("\"Oslo\"") };
        var result = await tool.InvokeAsync(arguments);

        Assert.Equal("Oslo for 3 days", result?.ToString());
    }

    [Fact]
    public async Task InvokeAsync_ThrowsOnTerminatingScriptError()
    {
        var tool = new ScriptBlockToolFunction("boom", "Fails",
            ScriptBlock.Create("throw 'kaboom'"));

        var ex = await Assert.ThrowsAnyAsync<Exception>(
            async () => await tool.InvokeAsync(new AIFunctionArguments()));
        Assert.Contains("kaboom", ex.Message);
    }

    [Fact]
    public async Task InvokeAsync_ThrowsOnNonTerminatingScriptError()
    {
        var tool = new ScriptBlockToolFunction("warn", "Fails softly",
            ScriptBlock.Create("Write-Error 'soft failure'"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await tool.InvokeAsync(new AIFunctionArguments()));
        Assert.Contains("soft failure", ex.Message);
    }

    [Fact]
    public async Task InvokeAsync_FormatsComplexOutput()
    {
        var tool = new ScriptBlockToolFunction("list", "Lists",
            ScriptBlock.Create("1..3"));

        var result = await tool.InvokeAsync(new AIFunctionArguments());

        Assert.Equal("1\n2\n3", result?.ToString()?.ReplaceLineEndings("\n"));
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;
}
