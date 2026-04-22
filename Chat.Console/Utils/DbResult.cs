using System.Data;

namespace Chat.Console.Utils;

public class DbResult
{
    public DataTable DataTable { get; set; }
    public string? ErrorMessage { get; set; }
    public string Sql { get; set; }
    public string UserQuery { get; set; }
    public bool Success => string.IsNullOrEmpty(ErrorMessage);
    public bool Fails => !Success;

    public DbResult(string sql, string userQuery, string errorMessage)
    {
        Sql = sql;
        UserQuery = userQuery;
        ErrorMessage = errorMessage;
    }

    public DbResult(string sql, string userQuery, DataTable dt)
    {
        Sql = sql;
        UserQuery = userQuery;
        DataTable = dt;
    }
}