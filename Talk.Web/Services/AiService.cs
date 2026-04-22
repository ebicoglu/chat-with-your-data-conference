using OpenAI.Chat;

namespace Talk.Web.Services;

public class AiService(ChatClient chatClient)
{
    private const string SystemPrompt = @"You are a SQL expert for an SQLite database.
Generate a SELECT query only. Never write INSERT, UPDATE, DELETE, DROP, ALTER, CREATE, TRUNCATE, EXEC or MERGE.
Return ONLY the raw SQL query, without markdown formatting, code fences, explanations, or trailing commentary.
Use the exact table and column names from the provided schema.
End the query with a semicolon.";

    public async Task<string> GenerateSqlAsync(string userInput, CancellationToken cancellationToken = default)
    {
        var dbSchema = await DbSchema.GetSchemaAsync();

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(SystemPrompt),
            new SystemChatMessage($"Database schema:\n{dbSchema}"),
            new UserChatMessage($"Write a SQL query for: \"{userInput}\""),
        };

        var options = new ChatCompletionOptions
        {
            Temperature = 0.0f,
        };

        var completion = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
        var raw = completion.Value.Content[0].Text ?? string.Empty;

        return CleanSql(raw);
    }

    /// <summary>
    /// Follow-up generation after a failed execution; uses higher temperature so the model is less likely to repeat the same mistake.
    /// </summary>
    public async Task<string> RegenerateSqlAfterErrorAsync(
        string userInput,
        string failedSql,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var dbSchema = await DbSchema.GetSchemaAsync();

        var fixMessage =
            "The previous SQLite SQL query was invalid. " +
            "Analyze the error and regenerate a valid SELECT query that matches the original intent.\n" +
            "Error:\n" +
            "```\n" +
            $"{errorMessage}\n" +
            "```\n" +
            "Original human query:\n" +
            $"{userInput}\n" +
            "Failed SQL:\n" +
            "```\n" +
            $"{failedSql}\n" +
            "```";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(SystemPrompt),
            new SystemChatMessage($"Database schema:\n{dbSchema}"),
            new UserChatMessage(fixMessage),
        };

        var options = new ChatCompletionOptions
        {
            Temperature = 0.7f,
        };

        var completion = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
        var raw = completion.Value.Content[0].Text ?? string.Empty;

        return CleanSql(raw);
    }

    private static string CleanSql(string raw)
    {
        var sql = raw.Trim();

        // Strip markdown code fences if the model included them anyway.
        if (sql.StartsWith("```"))
        {
            var firstNewline = sql.IndexOf('\n');
            if (firstNewline >= 0)
            {
                sql = sql[(firstNewline + 1)..];
            }

            if (sql.EndsWith("```"))
            {
                sql = sql[..^3];
            }

            sql = sql.Trim();
        }

        return sql;
    }
}
