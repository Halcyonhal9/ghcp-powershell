using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace CopilotCmdlets;

/// <summary>
/// New-CopilotTool — wraps a PowerShell ScriptBlock as an SDK custom tool
/// (Microsoft.Extensions.AI AIFunction) for use with New-CopilotSession -Tool.
/// The tool's JSON schema is derived from the ScriptBlock's param() block.
/// </summary>
[Cmdlet(VerbsCommon.New, "CopilotTool")]
[OutputType(typeof(AIFunction))]
public sealed class NewCopilotToolCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    [ValidatePattern("^[a-zA-Z0-9_-]+$")]
    public string Name { get; set; } = null!;

    [Parameter(Mandatory = true, Position = 1)]
    public string Description { get; set; } = null!;

    [Parameter(Mandatory = true, Position = 2)]
    public ScriptBlock ScriptBlock { get; set; } = null!;

    /// <summary>Run the tool without a permission prompt.</summary>
    [Parameter]
    public SwitchParameter SkipPermission { get; set; }

    protected override void EndProcessing()
    {
        try
        {
            WriteObject(new ScriptBlockToolFunction(
                Name,
                Description,
                ScriptBlock,
                SkipPermission.IsPresent,
                SessionState.LanguageMode));
        }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex, "ToolCreateFailed", ErrorCategory.InvalidArgument, ScriptBlock));
        }
    }
}

/// <summary>
/// AIFunction implementation backed by a PowerShell ScriptBlock. Each invocation
/// runs in a fresh runspace, so tools are safe to call from SDK background threads.
/// </summary>
public sealed class ScriptBlockToolFunction : AIFunction
{
    // Wire keys the SDK reads from AdditionalProperties when registering tools
    // (see CopilotTool.SkipPermissionKey in the SDK).
    private const string SkipPermissionKey = "skip_permission";

    private readonly JsonElement _schema;
    private readonly IReadOnlyDictionary<string, object?> _additionalProperties;
    private readonly PowerShellCallbackRunner _runner;

    public ScriptBlockToolFunction(
        string name,
        string description,
        ScriptBlock scriptBlock,
        bool skipPermission = false)
        : this(
            name,
            description,
            scriptBlock,
            skipPermission,
            PSLanguageMode.FullLanguage)
    {
    }

    public ScriptBlockToolFunction(
        string name,
        string description,
        ScriptBlock scriptBlock,
        bool skipPermission,
        PSLanguageMode languageMode)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(scriptBlock);

        Name = name;
        Description = description;
        _schema = BuildSchema(scriptBlock);
        _runner = new PowerShellCallbackRunner(scriptBlock, languageMode);
        _additionalProperties = skipPermission
            ? new Dictionary<string, object?> { [SkipPermissionKey] = true }
            : new Dictionary<string, object?>();
    }

    public override string Name { get; }

    public override string Description { get; }

    public override JsonElement JsonSchema => _schema;

    public override IReadOnlyDictionary<string, object?> AdditionalProperties => _additionalProperties;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await _runner.InvokeTextAsync(
                arguments.Select(argument =>
                    new KeyValuePair<string, object?>(
                        argument.Key,
                        ConvertArgument(argument.Value))),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Converts incoming JSON argument values to native PowerShell-friendly types.</summary>
    internal static object? ConvertArgument(object? value)
    {
        if (value is not JsonElement element)
            return value;

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : (object)element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.Array => element.EnumerateArray().Select(e => ConvertArgument(e)).ToArray(),
            JsonValueKind.Object => ConvertObject(element),
            _ => element.GetRawText()
        };

        static Hashtable ConvertObject(JsonElement element)
        {
            var table = new Hashtable();
            foreach (var property in element.EnumerateObject())
            {
                table[property.Name] = ConvertArgument(property.Value);
            }
            return table;
        }
    }

    /// <summary>Builds a JSON schema from the ScriptBlock's param() block.</summary>
    internal static JsonElement BuildSchema(ScriptBlock scriptBlock)
    {
        var properties = new JsonObject();
        var required = new JsonArray();

        var paramBlock = ((ScriptBlockAst)scriptBlock.Ast).ParamBlock;
        if (paramBlock is not null)
        {
            foreach (var parameter in paramBlock.Parameters)
            {
                var name = parameter.Name.VariablePath.UserPath;
                var property = new JsonObject
                {
                    ["type"] = MapType(parameter.StaticType)
                };

                if (GetHelpMessage(parameter) is { } helpMessage)
                    property["description"] = helpMessage;

                if (parameter.StaticType.IsArray)
                {
                    var elementType = parameter.StaticType.GetElementType();
                    if (elementType is not null && elementType != typeof(object))
                        property["items"] = new JsonObject { ["type"] = MapType(elementType) };
                }

                properties[name] = property;

                if (IsMandatory(parameter))
                    required.Add(name);
            }
        }

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties
        };
        if (required.Count > 0)
            schema["required"] = required;

        return JsonSerializer.SerializeToElement(schema, JsonContext.Default.JsonObject);
    }

    private static string MapType(Type type)
    {
        if (type == typeof(SwitchParameter)) return "boolean";
        if (type.IsArray || typeof(IList).IsAssignableFrom(type)) return "array";
        if (typeof(IDictionary).IsAssignableFrom(type)) return "object";

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => "boolean",
            TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or
            TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 => "integer",
            TypeCode.Single or TypeCode.Double or TypeCode.Decimal => "number",
            _ => "string"
        };
    }

    /// <summary>Returns the ScriptBlock param's [Parameter(...)] attributes (there may be more than one, e.g. per parameter set).</summary>
    private static IEnumerable<AttributeAst> GetParameterAttributes(ParameterAst parameter)
        => parameter.Attributes.OfType<AttributeAst>()
            .Where(a => a.TypeName.GetReflectionAttributeType() == typeof(ParameterAttribute));

    private static bool IsMandatory(ParameterAst parameter)
    {
        foreach (var attribute in GetParameterAttributes(parameter))
        {
            foreach (var argument in attribute.NamedArguments)
            {
                if (!argument.ArgumentName.Equals("Mandatory", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (argument.ExpressionOmitted)
                    return true;

                return argument.Argument is VariableExpressionAst variable &&
                       variable.VariablePath.UserPath.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
        }
        return false;
    }

    private static string? GetHelpMessage(ParameterAst parameter)
    {
        foreach (var attribute in GetParameterAttributes(parameter))
        {
            foreach (var argument in attribute.NamedArguments)
            {
                if (argument.ArgumentName.Equals("HelpMessage", StringComparison.OrdinalIgnoreCase) &&
                    argument.Argument is StringConstantExpressionAst constant)
                {
                    return constant.Value;
                }
            }
        }
        return null;
    }
}

[System.Text.Json.Serialization.JsonSerializable(typeof(JsonObject))]
internal sealed partial class JsonContext : System.Text.Json.Serialization.JsonSerializerContext;
