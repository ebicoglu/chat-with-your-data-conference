using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using OpenAI.Realtime;
using Talk.Web.Elements;
using Talk.Web.Services;

namespace Talk.Web;

public class RealtimeConversationManager<TModel> : IDisposable
{
    const string RealtimeModel = "gpt-realtime";

    private readonly string dbSchema;
    private readonly RealtimeClient realtimeClient;
    private readonly Stream micStream;
    private readonly Speaker speaker;
    private readonly Action<UserInteraction> updateCallback;
    private readonly Action<string> addMessage;
    private readonly RetryService? retryService;
    private readonly Func<Task>? onQueryStartedAsync;
    private readonly Func<Task>? onQueryCompletedAsync;
    private readonly Func<DataTable, Task>? onQueryResultAsync;
    private CancellationToken sessionCancellationToken;
    private RealtimeSessionClient? session;
    private string? prevModelJson;
    private string? lastUserTranscript;
    private string? lastUserQuestion;
    private bool isConnected;
    private Task? inputAudioTask;
    private bool disposed;

    public bool IsSessionActive => session is not null && !disposed;

    private readonly AIFunction[] tools;

    public RealtimeConversationManager(
        string dbSchema,
        RealtimeClient realtimeClient,
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
        this.realtimeClient = realtimeClient;
        this.micStream = micStream;
        this.speaker = speaker;
        this.updateCallback = updateCallback;
        this.addMessage = addMessage;
        this.retryService = retryService;
        this.onQueryStartedAsync = onQueryStartedAsync;
        this.onQueryCompletedAsync = onQueryCompletedAsync;
        this.onQueryResultAsync = onQueryResultAsync;
        tools = [AIFunctionFactory.Create(
            ExecuteSpokenQueryAsync,
            name: nameof(ExecuteSpokenQueryAsync),
            description: "Runs a SQLite SELECT for the user's data question and returns formatted results.")];
    }

    async Task<string> ExecuteSpokenQueryAsync(string transcribedUserQuestion, string sqliteSelectQuery)
    {
        // Captured synchronously with the tool call (unlike the async audio transcription), so it is the
        // most reliable signal of the language the user actually used for this specific turn.
        if (!string.IsNullOrWhiteSpace(transcribedUserQuestion))
        {
            lastUserQuestion = transcribedUserQuestion.Trim();
        }

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

        var sessionOptions = new RealtimeConversationSessionOptions
        {
            Instructions =
                "You help users query a SQLite database by voice. " +
                "Always reply in EXACTLY the same language the user just spoke in. " +
                "Mirror the user's language for every word you speak or write, including summaries of query results, confirmations, and any clarifying remarks. " +
                "Never translate the user's language into another language. Never switch languages on your own—only unless the user wants to change the active language. " +
                "Examples: if the user speaks English, answer in English; if Spanish, answer in Spanish. " +
                "When the user asks for data (counts, lists, sales, totals, charts), call ExecuteSpokenQueryAsync immediately—do not ask clarifying questions in another language. " +
                "For each data question: " +
                "(1) transcribedUserQuestion = user intent in the language they used; " +
                "(2) one SQLite SELECT using only schema tables/columns; " +
                "(3) call ExecuteSpokenQueryAsync with those two strings; " +
                "(4) summarize tool results in the user's language.",
            AudioOptions = new RealtimeConversationSessionAudioOptions
            {
                InputAudioOptions = new RealtimeConversationSessionInputAudioOptions
                {
                    AudioTranscriptionOptions = new RealtimeAudioTranscriptionOptions
                    {
                        Model = "gpt-4o-transcribe",
                    },
                    TurnDetection = new RealtimeSemanticVadTurnDetection
                    {
                        EagernessLevel = RealtimeSemanticVadEagernessLevel.Low,
                        InterruptResponseEnabled = false,
                    },
                },
                OutputAudioOptions = new RealtimeConversationSessionOutputAudioOptions
                {
                    Voice = RealtimeVoice.Alloy,
                },
            },
        };

        foreach (var tool in tools)
        {
            sessionOptions.Tools.Add(tool.ToRealtimeFunctionTool());
        }

        addMessage("Connecting...");
        session = await realtimeClient.StartConversationSessionAsync(RealtimeModel, cancellationToken: cancellationToken);

        var setupTask = SetupSessionAsync(session, sessionOptions, cancellationToken);
        var outputStringBuilder = new StringBuilder();

        try
        {
            try
            {
                await foreach (RealtimeServerUpdate update in session.ReceiveUpdatesAsync(cancellationToken))
                {
                    switch (update)
                    {
                        case RealtimeServerUpdateSessionCreated:
                        case RealtimeServerUpdateSessionUpdated:
                            MarkConnected();
                            break;

                        case RealtimeServerUpdateError errorUpdate:
                            addMessage($"Realtime error [{errorUpdate.Error.Code}]: {errorUpdate.Error.Message}");
                            break;

                        case RealtimeServerUpdateInputAudioBufferSpeechStarted:
                            addMessage("Speech started");
                            await speaker.ClearPlaybackAsync();
                            break;

                        case RealtimeServerUpdateInputAudioBufferSpeechStopped:
                            addMessage("Speech finished");
                            break;

                        case RealtimeServerUpdateConversationItemInputAudioTranscriptionCompleted transcriptionDone:
                            if (!string.IsNullOrWhiteSpace(transcriptionDone.Transcript))
                            {
                                lastUserTranscript = transcriptionDone.Transcript.Trim();
                                addMessage($"Heard: {transcriptionDone.Transcript}");
                            }
                            break;

                        case RealtimeServerUpdateResponseOutputAudioDelta audioDelta:
                            await speaker.EnqueueAsync(audioDelta.Delta.ToArray());
                            break;

                        case RealtimeServerUpdateResponseOutputAudioTranscriptDone transcriptDone:
                            outputStringBuilder.Append(transcriptDone.Transcript);
                            break;

                        case RealtimeServerUpdateResponseOutputTextDone textDone:
                            outputStringBuilder.Append(textDone.Text);
                            break;

                        case RealtimeServerUpdateResponseDone responseDone:
                            if (outputStringBuilder.Length > 0)
                            {
                                addMessage(outputStringBuilder.ToString());
                                outputStringBuilder.Clear();
                            }

                            await HandleToolCallsAsync(responseDone);
                            break;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                addMessage("Voice session ended");
            }
            catch (ObjectDisposedException)
            {
                // WebSocket closed during shutdown (navigation, dispose, or reconnect).
            }
        }
        finally
        {
            isConnected = false;

            try
            {
                await setupTask;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                addMessage($"Session setup failed: {ex.Message}");
            }

            if (inputAudioTask is not null)
            {
                try
                {
                    await inputAudioTask;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }

            DisposeSession();
        }
    }

    private void MarkConnected()
    {
        if (isConnected || session is null || disposed)
        {
            return;
        }

        isConnected = true;
        addMessage("Connected");
        inputAudioTask = session.SendInputAudioAsync(micStream, sessionCancellationToken);
    }

    private async Task SetupSessionAsync(
        RealtimeSessionClient session,
        RealtimeConversationSessionOptions sessionOptions,
        CancellationToken cancellationToken)
    {
        await session.ConfigureConversationSessionAsync(sessionOptions, cancellationToken);

        const int chunkSize = 4000;
        var chunkCount = (dbSchema.Length + chunkSize - 1) / chunkSize;
        for (int i = 0; i < dbSchema.Length; i += chunkSize)
        {
            int length = Math.Min(chunkSize, dbSchema.Length - i);
            string chunk = dbSchema.Substring(i, length);
            await session.AddItemAsync(
                RealtimeItem.CreateUserMessageItem($"Database schema chunk {i / chunkSize + 1}:\n{chunk}"),
                cancellationToken);
        }

        addMessage($"Schema loaded ({chunkCount} chunks)");
    }

    public void Dispose() => DisposeSession();

    private void DisposeSession()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        session?.Dispose();
        session = null;
    }

    public async Task SetModelData(TModel modelData)
    {
        if (session is null || disposed)
        {
            return;
        }

        try
        {
            var newJson = JsonSerializer.Serialize(modelData);
            if (newJson != prevModelJson)
            {
                prevModelJson = newJson;
                await session.AddItemAsync(RealtimeItem.CreateUserMessageItem(
                    $"The current modelData value is {newJson}. When updating this later, include all these same values if they are unchanged (or they will be overwritten with nulls)."),
                    sessionCancellationToken);
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (OperationCanceledException) when (sessionCancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task HandleToolCallsAsync(RealtimeServerUpdateResponseDone responseDone)
    {
        if (session is null || disposed)
        {
            return;
        }

        var functionCalls = responseDone.Response.OutputItems.OfType<RealtimeFunctionCallItem>().ToList();
        if (functionCalls.Count == 0)
        {
            return;
        }

        foreach (var functionCall in functionCalls)
        {
            var output = await functionCall.InvokeToolAsync(tools);
            if (output is not null)
            {
                await session.AddItemAsync(RealtimeItem.CreateFunctionCallOutputItem(functionCall.CallId, output), sessionCancellationToken);
            }
        }

        // Re-ground the language right before the summary so the model can't drift to English (or any other language).
        // The session-level instructions are not always enough once the (English) schema and tool output dominate the context,
        // so we restate the user's most recent utterance and force a matching reply language for this specific response.
        var responseOptions = new RealtimeResponseOptions
        {
            Instructions = BuildSummaryInstructions(),
        };

        await session.StartResponseAsync(responseOptions, sessionCancellationToken);
    }

    private string BuildSummaryInstructions()
    {
        // Prefer the question text captured at tool-call time; the async audio transcription can lag behind
        // (or belong to an earlier turn), which previously let the reply drift to an unrelated language.
        var userMessage = !string.IsNullOrWhiteSpace(lastUserQuestion)
            ? lastUserQuestion
            : lastUserTranscript;

        var instructions =
            "Summarize the SQL query results for the user in a concise, natural, spoken style. " +
            "LANGUAGE RULE (highest priority): reply in EXACTLY the same language as the user's question below. " +
            "Do not default to English. Do not default to German. Do not translate. Do not switch languages. " +
            "The language of your entire answer MUST match the language of the user's question, word for word.";

        if (!string.IsNullOrWhiteSpace(userMessage))
        {
            instructions += $" The user's question was: \"{userMessage}\". Detect the language of that exact sentence and write your answer only in that same language.";
        }

        return instructions;
    }
}
