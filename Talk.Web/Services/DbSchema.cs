namespace Talk.Web.Services;

public static class DbSchema
{
    private static string? _cachedSchema;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);

    // Fetches the SQLite database schema once and caches it in memory.
    public static async Task<string> GetSchemaAsync()
    {
        if (_cachedSchema is not null)
        {
            return _cachedSchema;
        }

        await SchemaLock.WaitAsync();
        try
        {
            _cachedSchema ??= await DbService.GetDbSchema();
            return _cachedSchema;
        }
        finally
        {
            SchemaLock.Release();
        }
    }
}
