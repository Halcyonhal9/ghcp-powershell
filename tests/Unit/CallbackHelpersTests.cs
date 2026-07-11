using System.Management.Automation;
using Xunit;

using CopilotCmdlets;

namespace CopilotCmdlets.Tests.Unit;

[Trait("Category", "Unit")]
public class CallbackHelpersTests
{
    [Fact]
    public async Task InvokeRequiredAsync_ReturnsExpectedType()
    {
        var runner = Create("param($value) [int]$value");

        var result = await runner.InvokeRequiredAsync<int>(42);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task InvokeRequiredAsync_RejectsMissingResult()
    {
        var runner = Create("$null");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.InvokeRequiredAsync<string>());

        Assert.Contains("non-null", error.Message);
    }

    [Fact]
    public async Task InvokeRequiredAsync_RejectsMultipleResults()
    {
        var runner = Create("1; 2");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.InvokeRequiredAsync<int>());

        Assert.Contains("2 values", error.Message);
    }

    [Fact]
    public async Task InvokeRequiredAsync_RejectsWrongType()
    {
        var runner = Create("'not-an-int'");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.InvokeRequiredAsync<int>());

        Assert.Contains(typeof(int).FullName!, error.Message);
        Assert.Contains(typeof(string).FullName!, error.Message);
    }

    [Fact]
    public async Task InvokeOptionalAsync_AllowsMissingResult()
    {
        var runner = Create("$null");

        var result = await runner.InvokeOptionalAsync<string>();

        Assert.Null(result);
    }

    [Fact]
    public async Task InvokeOptionalAsync_ReturnsExpectedType()
    {
        var runner = Create("'value'");

        var result = await runner.InvokeOptionalAsync<string>();

        Assert.Equal("value", result);
    }

    [Fact]
    public async Task InvokeVoidAsync_DiscardsOutput()
    {
        var runner = Create("1; 2; 3");

        await runner.InvokeVoidAsync();
    }

    [Theory]
    [InlineData("throw 'terminating failure'")]
    [InlineData("Write-Error 'nonterminating failure'")]
    public async Task Invocation_SurfacesPowerShellErrors(string script)
    {
        var runner = Create(script);

        var error = await Assert.ThrowsAnyAsync<Exception>(
            () => runner.InvokeVoidAsync());

        Assert.Contains("failure", error.Message);
    }

    [Fact]
    public async Task Invocation_UsesFreshRunspaceForEveryCall()
    {
        var runner = Create("$global:count++; $global:count");

        var first = await runner.InvokeRequiredAsync<int>();
        var second = await runner.InvokeRequiredAsync<int>();

        Assert.Equal(1, first);
        Assert.Equal(1, second);
    }

    [Fact]
    public async Task Invocation_SupportsConcurrentCalls()
    {
        var runner = Create("param($value) Start-Sleep -Milliseconds 10; [int]$value");
        var tasks = Enumerable.Range(1, 8)
            .Select(value => runner.InvokeRequiredAsync<int>(value));

        var results = await Task.WhenAll(tasks);

        Assert.Equal(Enumerable.Range(1, 8), results);
    }

    [Fact]
    public async Task Invocation_PreservesConstrainedLanguageMode()
    {
        var runner = Create(
            "[System.IO.File]::Exists('.')",
            PSLanguageMode.ConstrainedLanguage);

        var error = await Assert.ThrowsAnyAsync<Exception>(
            () => runner.InvokeRequiredAsync<bool>());

        Assert.Contains("language mode", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnwrapResult_RecursivelyUnwrapsPowerShellObjects()
    {
        var value = new PSObject(new PSObject("inner"));

        Assert.Equal("inner", PowerShellCallbackRunner.UnwrapResult(value));
    }

    private static PowerShellCallbackRunner Create(
        string script,
        PSLanguageMode languageMode = PSLanguageMode.FullLanguage)
        => new(ScriptBlock.Create(script), languageMode);
}
