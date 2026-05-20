using System.Text.Json;
using Microsoft.Extensions.AI;
using OpenAI.Realtime;

namespace Talk.Web.Elements;

public static class AIFunctionExtensions
{
    /// <summary>
    /// Converts a <see cref="AIFunction"/> into a <see cref="RealtimeFunctionTool"/> for the GA Realtime API.
    /// </summary>
    public static RealtimeFunctionTool ToRealtimeFunctionTool(this AIFunction aiFunction)
    {
        var schema = aiFunction.JsonSchema;
        var parametersJson = schema.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? """{"type":"object","properties":{}}"""
            : schema.GetRawText();

        return new RealtimeFunctionTool(aiFunction.Name)
        {
            FunctionDescription = aiFunction.Description,
            FunctionParameters = BinaryData.FromString(parametersJson),
        };
    }

    public static async Task<string?> InvokeToolAsync(this RealtimeFunctionCallItem functionCall, IReadOnlyList<AIFunction> tools)
    {
        if (string.IsNullOrEmpty(functionCall.FunctionName))
        {
            return null;
        }

        var aiFunction = tools.FirstOrDefault(t => t.Name == functionCall.FunctionName);
        if (aiFunction is null)
        {
            return $"Unknown tool: {functionCall.FunctionName}";
        }

        try
        {
            var argsJson = functionCall.FunctionArguments.ToString();
            var jsonArgs = JsonSerializer.Deserialize<Dictionary<string, object?>>(argsJson)!;
            var output = await aiFunction.InvokeAsync(new AIFunctionArguments(jsonArgs!));
            return output?.ToString() ?? "";
        }
        catch (JsonException)
        {
            return "Invalid JSON";
        }
        catch (Exception ex)
        {
            return $"Error calling tool: {ex.Message}";
        }
    }
}
