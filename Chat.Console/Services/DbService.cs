using System.Data;
using System.Data.SQLite;
using Chat.Console.Utils;

namespace Chat.Console.Services;

public static class DbService
{
    // Runs the SQL query against the database and simply returns the result as a DataTable
    public static async Task<DbResult> RunSqlQuery(string userQuery, string sql, string connectionString)
    {
        if (!SqlSafetyChecker.IsSafe(sql))
        {
            return new DbResult(sql, userQuery, errorMessage: "Dangerous SQL query!!!"); // Returns empty DataTable
        }

        try
        {
            var dt = new DataTable();
            await using var connection = new SQLiteConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SQLiteCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            dt.Load(reader);
            return new DbResult(sql, userQuery, dt);
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError("ERROR RUNNING THE QUERY:\n" + ex.Message);
            return new DbResult(sql, userQuery, ex.Message);
        }
    }

    //returns the database type as a string, e.g., "SQLite"
    public static string GetDbType(string connectionString)
    {
        using var connection = new SQLiteConnection(connectionString);
        return connection.GetType().Name.Replace("Connection", string.Empty);
    }
}