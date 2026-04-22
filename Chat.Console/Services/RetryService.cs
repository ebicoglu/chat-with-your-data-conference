using Chat.Console.Utils;

namespace Chat.Console.Services;

public static class RetryService
{
    public static async Task<DbResult> TryToFindWorkingSql(DbResult dbResult, int maxRetry, string connectionString)
    {
        for (var currentTry = 1; currentTry < maxRetry + 1; currentTry++)
        {
            ConsoleHelper.WriteInfo($"3.{currentTry}) INVALID SQL, ASKING AI TO FIX THE QUERY ({currentTry}/{maxRetry})...");

            // Create a prompt for the AI to fix the SQL error
            AiService.ChatHistory.AddUserMessage(
               $"The previous " + DbService.GetDbType(connectionString) + "SQL query was invalid. " +
               "Analyze the error and regenerate a valid SQL query that matches the original intent.\n" +
               $"Error:\n" +
               $"```\n" +
               $"{dbResult.ErrorMessage}\n" +
               $"```\n" +
               $"Original human query:\n" +
               $"{dbResult.UserQuery}"
            );

            //This time run the AI on creative mode so that it'll not give the same invalid SQL
            var newSql = await AiService.AskAi();
            newSql = Helpers.ExtractSqlQuery(newSql);

            ConsoleHelper.WriteInfo($"3.1.{currentTry}) NEW SQL QUERY from AI:");
            System.Console.WriteLine(newSql);

            // Execute the corrected query
            dbResult = await DbService.RunSqlQuery(dbResult.UserQuery, newSql, connectionString);

            // If successful, return the result
            if (dbResult.Success)
            {
                ConsoleHelper.WriteInfo("✓ QUERY SUCCESSFULLY EXECUTED.");
                return dbResult;
            }
        }

        // Exhausted all retries, return the last failed result
        return dbResult;
    }
}