using System.Data;
using System.Text;
using OpenAI.Chat;

namespace Talk.Web.Services.ChartService;

/// <summary>
/// Recommends the best Vega chart type for the given data.
/// Uses the OpenAI chat client when available, with a deterministic fallback.
/// </summary>
public class ChartRecommendationService(ChatClient? chatClient = null)
{
    private static readonly HashSet<string> SupportedChartTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "bar", "line", "scatter", "pie", "area"
    };

    public async Task<string> RecommendChartType(DataTable data, string? userQuery = null, CancellationToken cancellationToken = default)
    {
        if (data.Columns.Count < 2 || data.Rows.Count == 0)
        {
            return "bar";
        }

        var aiRecommendation = await TryRecommendChartTypeWithAi(data, userQuery, cancellationToken);
        if (!string.IsNullOrWhiteSpace(aiRecommendation))
        {
            return aiRecommendation;
        }

        return RecommendChartTypeWithRules(data, userQuery);
    }

    private async Task<string?> TryRecommendChartTypeWithAi(DataTable data, string? userQuery, CancellationToken cancellationToken)
    {
        if (chatClient is null)
        {
            return null;
        }

        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(
                    """
                    You are a data visualization expert.
                    Select the single best chart type for the given data and user question.
                    Allowed chart types: bar, line, scatter, pie, area.
                    Return only one word from the allowed chart types.
                    Do not include punctuation, explanation, markdown, or code blocks.
                    """),
                new UserChatMessage(BuildChartRecommendationPrompt(data, userQuery))
            };

            var options = new ChatCompletionOptions { Temperature = 0.0f };
            var completion = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
            var rawResponse = completion.Value.Content[0].Text ?? string.Empty;

            var sanitized = SanitizeChartType(rawResponse);
            if (!string.IsNullOrWhiteSpace(sanitized))
            {
                return sanitized;
            }
        }
        catch
        {
            // AI recommendation is optional; fall back to deterministic rules.
        }

        return null;
    }

    private static string? SanitizeChartType(string? aiResponse)
    {
        if (string.IsNullOrWhiteSpace(aiResponse))
        {
            return null;
        }

        var firstToken = aiResponse
            .Trim()
            .Split(new[] { ' ', '\n', '\r', '\t', '.', ',', ':', ';', '-', '_', '"' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (firstToken is null)
        {
            return null;
        }

        return SupportedChartTypes.Contains(firstToken) ? firstToken.ToLowerInvariant() : null;
    }

    private static string BuildChartRecommendationPrompt(DataTable data, string? userQuery)
    {
        var sb = new StringBuilder();
        sb.AppendLine("User question:");
        sb.AppendLine(string.IsNullOrWhiteSpace(userQuery) ? "(none)" : userQuery);
        sb.AppendLine();
        sb.AppendLine("Dataset summary:");
        sb.AppendLine($"- Rows: {data.Rows.Count}");
        sb.AppendLine($"- Columns: {data.Columns.Count}");
        sb.AppendLine("- Column details:");

        foreach (DataColumn column in data.Columns)
        {
            sb.AppendLine($"  - {column.ColumnName} ({column.DataType.Name})");
        }

        sb.AppendLine();
        sb.AppendLine("Sample rows (max 5):");
        var sampleCount = Math.Min(data.Rows.Count, 5);
        for (var i = 0; i < sampleCount; i++)
        {
            var row = data.Rows[i];
            var values = data.Columns.Cast<DataColumn>()
                .Select(c => $"{c.ColumnName}={row[c]}");
            sb.AppendLine($"  - {string.Join(", ", values)}");
        }

        return sb.ToString();
    }

    private static string RecommendChartTypeWithRules(DataTable data, string? userQuery)
    {
        var columns = data.Columns.Cast<DataColumn>().ToList();

        var numericColumns = columns.Count(c => IsNumericType(c.DataType));
        var dateColumns = columns.Count(c => c.DataType == typeof(DateTime));
        var categoricalColumns = columns.Count(c => !IsNumericType(c.DataType) && c.DataType != typeof(DateTime));

        if (userQuery != null)
        {
            var query = userQuery.ToLowerInvariant();
            if (query.Contains("trend") || query.Contains("over time") || dateColumns > 0)
                return "line";
            if (query.Contains("distribution") || query.Contains("proportion"))
                return "pie";
            if (query.Contains("correlation") || query.Contains("relationship"))
                return "scatter";
        }

        if (categoricalColumns == 1 && numericColumns == 1)
            return "bar";
        if (dateColumns > 0 && numericColumns > 0)
            return "line";
        if (numericColumns >= 2)
            return "scatter";
        if (categoricalColumns > 0 && numericColumns == 1)
            return "pie";

        return "bar";
    }

    private static bool IsNumericType(Type type)
    {
        return type == typeof(int) || type == typeof(long) || type == typeof(float) ||
               type == typeof(double) || type == typeof(decimal) || type == typeof(short) ||
               type == typeof(byte) || type == typeof(uint) || type == typeof(ulong);
    }
}
