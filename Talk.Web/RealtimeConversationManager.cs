using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using OpenAI.RealtimeConversation;
using Talk.Web.Elements;
using Talk.Web.Services;

namespace Talk.Web;

public class RealtimeConversationManager<TModel> : IDisposable
{
    private readonly string dbSchema;
    private readonly RealtimeConversationClient realtimeConversationClient;
    private readonly Stream micStream;
    private readonly Speaker speaker;
    private readonly Action<UserInteraction> updateCallback;
    private readonly Action<string> addMessage;
    private readonly RetryService? retryService;
    private readonly Func<Task>? onQueryStartedAsync;
    private readonly Func<Task>? onQueryCompletedAsync;
    private readonly Func<DataTable, Task>? onQueryResultAsync;
    private CancellationToken sessionCancellationToken;
    private RealtimeConversationSession? session;
    private string? prevModelJson;

    // Call back into the UI layer to update the data in the form
    private readonly AIFunction[] tools;

    public RealtimeConversationManager(
        string dbSchema,
        RealtimeConversationClient realtimeConversationClient,
        Stream micStream,
        Speaker speaker,
        Action<UserInteraction> updateCallback,
        Action<string> addMessage,
        RetryService? retryService = null,
        Func<Task>? onQueryStartedAsync = null,
        Func<Task>? onQueryCompletedAsync = null,
        Func<DataTable, Task>? onQueryResultAsync = null)
    {
        this.dbSchema = dbSchema;
        this.realtimeConversationClient = realtimeConversationClient;
        this.micStream = micStream;
        this.speaker = speaker;
        this.updateCallback = updateCallback;
        this.addMessage = addMessage;
        this.retryService = retryService;
        this.onQueryStartedAsync = onQueryStartedAsync;
        this.onQueryCompletedAsync = onQueryCompletedAsync;
        this.onQueryResultAsync = onQueryResultAsync;
        tools = [AIFunctionFactory.Create(ExecuteSpokenQueryAsync)];

    }

    /// <summary>
    /// Single tool: persist transcript + SQL, run SELECT, return formatted results (and optional UI refresh).
    /// </summary>
    async Task<string> ExecuteSpokenQueryAsync(string transcribedUserQuestion, string sqliteSelectQuery)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(sqliteSelectQuery);
        Console.ResetColor();

        updateCallback(new UserInteraction
        {
            UserInput = transcribedUserQuestion,
            SqlQuery = sqliteSelectQuery,
        });

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(sqliteSelectQuery);
        Console.ResetColor();

        var userQuery = transcribedUserQuestion;
        if (onQueryStartedAsync is not null)
        {
            await onQueryStartedAsync();
        }

        DbResult dbResult;
        try
        {
            dbResult = await DbService.RunSqlQuery(userQuery, sqliteSelectQuery, sessionCancellationToken);

            if (dbResult.Fails && retryService is not null)
            {
                addMessage($"SQL error: {dbResult.ErrorMessage}");
                dbResult = await retryService.TryToFindWorkingSqlAsync(dbResult, maxRetry: 5, sessionCancellationToken, addMessage);
                if (dbResult.Success)
                {
                    updateCallback(new UserInteraction { UserInput = userQuery, SqlQuery = dbResult.Sql });
                }
            }

            if (dbResult.Fails)
            {
                return $"Error running SQL: {dbResult.ErrorMessage}";
            }

            var dt = dbResult.Data!;
            var str = StringOutput.Print(dt);
            ExcelService.GenerateExcel(dt, @"c:\temp\report.xlsx");

            if (onQueryResultAsync is not null)
            {
                await onQueryResultAsync(dt);
            }

            return str;
        }
        finally
        {
            if (onQueryCompletedAsync is not null)
            {
                await onQueryCompletedAsync();
            }
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        sessionCancellationToken = cancellationToken;

        var sessionOptions = new ConversationSessionOptions
        {
            Instructions =
                "The user's speech is transcribed for you. For each completed user utterance about the data: " +
                "(1) Set transcribedUserQuestion to the user's intent in clear natural language (same language they spoke). " +
                "(2) Produce exactly one SQLite-compatible SELECT query using only tables/columns from the schema you received. " +
                "(3) You MUST call ExecuteSpokenQueryAsync once with those two arguments. That tool saves the text, runs the query, and returns row data. " +
                "Never skip the tool call for data questions; do not end the turn with only assistant text when the user asked for data.",
            Voice = ConversationVoice.Alloy,
            ContentModalities = ConversationContentModalities.Text,
            TurnDetectionOptions = ConversationTurnDetectionOptions.CreateServerVoiceActivityTurnDetectionOptions(detectionThreshold: 0.4f, silenceDuration: TimeSpan.FromMilliseconds(150)),
        };
        
        foreach (var tool in tools)
        {
            sessionOptions.Tools.Add(tool.ToConversationFunctionTool());
        }

        addMessage("Connecting...");
        session = await realtimeConversationClient.StartConversationSessionAsync(cancellationToken);
        await session.ConfigureSessionAsync(sessionOptions, cancellationToken);

        // Split dbSchema into chunks and send them as separate messages
        const int chunkSize = 4000; // Adjust this value based on your needs
        for (int i = 0; i < dbSchema.Length; i += chunkSize)
        {
            int length = Math.Min(chunkSize, dbSchema.Length - i);
            string chunk = dbSchema.Substring(i, length);
            await session.AddItemAsync(ConversationItem.CreateUserMessage([$"Database schema chunk {i/chunkSize + 1}:\n{chunk}"]));
        }

        var outputStringBuilder = new StringBuilder();

        await foreach (ConversationUpdate update in session.ReceiveUpdatesAsync(cancellationToken))
        {
            switch (update)
            {
                case ConversationSessionStartedUpdate:
                    addMessage("Connected");
                    _ = Task.Run(async () => await session.SendInputAudioAsync(micStream, cancellationToken));
                    break;

                case ConversationInputSpeechStartedUpdate:
                    addMessage("Speech started");
                    await speaker.ClearPlaybackAsync(); // If the user interrupts, stop talking
                    break;

                case ConversationInputSpeechFinishedUpdate:
                    addMessage("Speech finished");
                    break;

                case ConversationItemStreamingPartDeltaUpdate outputDelta:
                    // Happens each time a chunk of output is received
                    await speaker.EnqueueAsync(outputDelta.AudioBytes?.ToArray());
                    outputStringBuilder.Append(outputDelta.Text ?? outputDelta.AudioTranscript);
                    break;

                case ConversationResponseFinishedUpdate responseFinished:
                    // Happens when a "response turn" is finished
                    addMessage(outputStringBuilder.ToString());
                    outputStringBuilder.Clear();
                    break;
            }

            await HandleToolCallsAsync(update, tools);
        }
    }

    public void Dispose()
    {
        session?.Dispose();
    }

    // Called by the UI when the user manually edits the form. This lets the AI know
    // the latest state in case it needs to make further updates.
    public async Task SetModelData(TModel modelData)
    {
        if (session is not null)
        {
            var newJson = JsonSerializer.Serialize(modelData);
            if (newJson != prevModelJson)
            {
                prevModelJson = newJson;
                await session.AddItemAsync(ConversationItem.CreateUserMessage([$"The current modelData value is {newJson}. When updating this later, include all these same values if they are unchanged (or they will be overwritten with nulls)."]));
            }
        }
    }

    private async Task HandleToolCallsAsync(ConversationUpdate update, AIFunction[] tools)
    {
        switch (update)
        {
            case ConversationItemStreamingFinishedUpdate itemFinished:
                // If we need to call a tool to update the model, do so
                if (!string.IsNullOrEmpty(itemFinished.FunctionName) && await itemFinished.GetFunctionCallOutputAsync(tools) is { } output)
                {
                    await session!.AddItemAsync(output);
                }
                break;

            case ConversationResponseFinishedUpdate responseFinished:
                // If we added one or more function call results, instruct the model to respond to them
                if (responseFinished.CreatedItems.Any(item => !string.IsNullOrEmpty(item.FunctionName)))
                {
                    await session!.StartResponseAsync();
                }
                break;
        }
    }
}
