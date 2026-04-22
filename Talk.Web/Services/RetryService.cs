namespace Talk.Web.Services;

public class RetryService(AiService aiService)
{
    /// <summary>
    /// After a failed SQL execution, asks the model to fix the query and re-runs until success or <paramref name="maxRetry"/> attempts.
    /// </summary>
    /// <param name="onProgress">Optional callback for UI logs (e.g. conversation history).</param>
    public async Task<DbResult> TryToFindWorkingSqlAsync(
        DbResult dbResult,
        int maxRetry,
        CancellationToken cancellationToken = default,
        Action<string>? onProgress = null)
    {
        onProgress?.Invoke($"SQL correction: up to {maxRetry} attempt(s) will be tried.");

        for (var attempt = 1; attempt <= maxRetry; attempt++)
        {
            onProgress?.Invoke($"SQL retry {attempt}/{maxRetry}: asking AI to fix the query...");

            var newSql = await aiService.RegenerateSqlAfterErrorAsync(
                dbResult.UserQuery,
                dbResult.Sql,
                dbResult.ErrorMessage ?? "Unknown error",
                cancellationToken);

            onProgress?.Invoke($"SQL retry {attempt}/{maxRetry}: running corrected SQL...");

            dbResult = await DbService.RunSqlQuery(dbResult.UserQuery, newSql, cancellationToken);
            if (dbResult.Success)
            {
                onProgress?.Invoke($"SQL retry {attempt}/{maxRetry}: query executed successfully.");
                return dbResult;
            }

            onProgress?.Invoke($"SQL retry {attempt}/{maxRetry}: still failed — {dbResult.ErrorMessage}");
        }

        onProgress?.Invoke("SQL correction: all retry attempts exhausted.");
        return dbResult;
    }
}
