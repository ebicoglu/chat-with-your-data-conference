using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Chat.Console.Services;

public static class AiService
{
    private const string AiModel = "gpt-5.4";
    private static readonly Kernel Kernel = Kernel.CreateBuilder().AddOpenAIChatCompletion(modelId: AiModel, apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY")).Build();
    private static readonly IChatCompletionService ChatCompletionService = Kernel.GetRequiredService<IChatCompletionService>();
    public static ChatHistory ChatHistory = [];

    private const string SystemPromptTemplate = @"You are a SQL expert for a {{$DatabaseType}} database.
User will ask you a query and you will generate a SELECT query only. Never write INSERT, UPDATE, or DELETE.
Do not include any markdown formatting or code blocks in your response.
Just return the raw SQL query.

Example format:
SELECT count(*) FROM ""TableName""

Database Schema:
```sql
{{$DbSchema}}
```";

    public static async Task<string> GenerateSqlQuery(string userQuery, string dbSchema, string connectionString)
    {
        var dbType = DbService.GetDbType(connectionString);
        var systemPrompt = SystemPromptTemplate
            .Replace("{{$DbSchema}}", dbSchema)
            .Replace("{{$DatabaseType}}", dbType);

        ChatHistory.Clear();
        ChatHistory.AddSystemMessage(systemPrompt);
        ChatHistory.AddUserMessage(userQuery);
        return await AskAi();
    }

    public static async Task<string> AskAi(string? prompt = null)
    {
        var result = await ChatCompletionService.GetChatMessageContentsAsync(ChatHistory);
        ChatHistory.AddAssistantMessage(result.First().Content);
        return result.First().Content;
    }
}
