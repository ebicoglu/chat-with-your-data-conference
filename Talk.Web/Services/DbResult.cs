using System.Data;

namespace Talk.Web.Services;

public sealed class DbResult
{
    public string Sql { get; }
    public string UserQuery { get; }
    public DataTable? Data { get; }
    public string? ErrorMessage { get; }

    public bool Success => Data is not null;
    public bool Fails => !Success;

    public DbResult(string sql, string userQuery, DataTable data)
    {
        Sql = sql;
        UserQuery = userQuery;
        Data = data;
    }

    public DbResult(string sql, string userQuery, string errorMessage)
    {
        Sql = sql;
        UserQuery = userQuery;
        ErrorMessage = errorMessage;
        Data = null;
    }
}
