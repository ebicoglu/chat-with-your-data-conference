using System.Data;
using System.Data.SQLite;
using System.Text;

namespace Talk.Web.Services;

public static class DbService
{
    public const string ConnectionString = "Data Source=Db/chinook.db;Version=3;Read Only=True;";

    public static async Task<DbResult> RunSqlQuery(string userQuery, string sql, CancellationToken cancellationToken = default)
    {
        if (!IsSqlSafe(sql))
        {
            return new DbResult(sql, userQuery, "Dangerous SQL query!");
        }

        try
        {
            var dt = new DataTable();
            await using var connection = new SQLiteConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SQLiteCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            dt.Load(reader);
            return new DbResult(sql, userQuery, dt);
        }
        catch (Exception ex)
        {
            return new DbResult(sql, userQuery, ex.Message);
        }
    }

    private static bool IsSqlSafe(string sql)
    {
        sql = sql.Trim().ToLowerInvariant();

        // Check if it starts with "select" and doesn't contain any modification keywords
        return sql.StartsWith("select") &&
               !sql.Contains("insert") &&
               !sql.Contains("update") &&
               !sql.Contains("delete") &&
               !sql.Contains("drop") &&
               !sql.Contains("alter") &&
               !sql.Contains("create") &&
               !sql.Contains("truncate") &&
               !sql.Contains("exec") &&
               !sql.Contains("merge");
    }

    public static async Task<string> GetDbSchema()
    {
        var ddlScript = new StringBuilder();

        await using var connection = new SQLiteConnection(ConnectionString);
        await connection.OpenAsync();

        // SQLite exposes the original CREATE TABLE statements in its schema.
        var tables = await connection.GetSchemaAsync("Tables");
        foreach (DataRow row in tables.Rows)
        {
            var definition = row["TABLE_DEFINITION"]?.ToString();
            if (!string.IsNullOrWhiteSpace(definition))
            {
                ddlScript.AppendLine(definition);
                ddlScript.AppendLine();
            }
        }

        return ddlScript.ToString();
    }
}
